using UnityEngine;

/// <summary>
/// 월드에 배치된 기계 하나의 런타임 인스턴스. 자기 인벤토리(<see cref="MachineInventory"/>)를 보유하며,
/// 청크의 <see cref="PlaceableRecord"/> 와 <see cref="LoadFrom"/>/<see cref="WriteBack"/> 로 동기화된다.
/// 슬롯 개수/가스/에너지 설정은 blockId 로 ItemDictionary(MachineBlock)에서 가져온다.
/// </summary>
public class MachineInstance : MonoBehaviour
{
    // 기계 정보 미입력(딕셔너리 미구성) 상태에서도 현행 기계가 동작하도록 하는 폴백.
    private const int DefaultInputCount = 3;
    private const int DefaultOutputCount = 6;

    private int inputSlotCount;
    private int outputSlotCount;
    private int fuelSlotCount;
    private float fuelBurnRate;
    private int InputGasSlotCount;
    private int OutputGasSlotCount;
    private float MaxGasAmount;   // 모든 가스 슬롯(입력/출력)이 공유하는 최대치
    private float MaxEnergyAmount;
    private bool IsUseEnergy;
    private bool isGenerator;
    private int powerRange;
    private float energyUseRate;

    public string blockId;
    public Vector2Int worldCell;
    public MachineInventory inventory { get; private set; }
    public PlaceableRecord Record { get; private set; }

    /// <summary>이 기계의 블록 정보(딕셔너리 미구성 시 null). 조합대 티어 등 파생 설정을 읽는 데 쓴다.</summary>
    public MachineBlock Info { get; private set; }

    // ── 가스 · 에너지 보유량 ────────────────────────────────────
    private float currentEnergy;
    private Gas[] inputGas = System.Array.Empty<Gas>();
    private Gas[] outputGas = System.Array.Empty<Gas>();

    public float CurrentEnergy => currentEnergy;
    public float EnergyRatio => MaxEnergyAmount > 0f ? Mathf.Clamp01(currentEnergy / MaxEnergyAmount) : 0f;

    // ── 전력 링크 (발전기만 보유) ───────────────────────────────
    // 전력을 보내는 쪽만 상대를 안다. 받는 기계는 누가 먹여주는지 알 필요가 없어
    // 발전기를 캐면 레코드와 함께 링크도 한꺼번에 사라진다.
    private readonly System.Collections.Generic.List<Vector2Int> links = new();
    private int roundRobinCursor;

    public bool IsGenerator => isGenerator;
    public int PowerRange => powerRange;
    public System.Collections.Generic.IReadOnlyList<Vector2Int> Links => links;
    public bool IsLinkedTo(Vector2Int cell) => links.Contains(cell);

    /// <summary>이 셀이 전송 범위(체비셰프 거리) 안인가.</summary>
    public bool IsInPowerRange(Vector2Int cell)
        => Mathf.Max(Mathf.Abs(cell.x - worldCell.x), Mathf.Abs(cell.y - worldCell.y)) <= powerRange;

    /// <summary>전송 대상을 추가한다. 발전기가 아니거나 자기 자신·중복·범위 밖이면 false.</summary>
    public bool AddLink(Vector2Int cell)
    {
        if (!isGenerator || cell == worldCell || links.Contains(cell) || !IsInPowerRange(cell)) return false;

        links.Add(cell);
        Flush();   // 청크 언로드·저장 사이에 잃지 않도록 즉시 레코드에 반영
        return true;
    }

    /// <summary>전송 대상을 끊는다.</summary>
    public bool RemoveLink(Vector2Int cell)
    {
        if (!links.Remove(cell)) return false;

        if (roundRobinCursor >= links.Count) roundRobinCursor = 0;
        Flush();
        return true;
    }

    // ── 연료 연소 ──────────────────────────────────────────────
    // 화로처럼 연료를 태우는 기계는 지금 타고 있는 연료가 남아 있어야 가공이 진행된다.
    private float burnRemaining;   // 남은 에너지
    private float burnTotal;       // 이번에 넣은 연료의 총 에너지(잔량 바 기준)

    public bool UsesFuel => fuelSlotCount > 0;
    public float BurnRemaining => burnRemaining;
    public float BurnRatio => burnTotal > 0f ? Mathf.Clamp01(burnRemaining / burnTotal) : 0f;

    // ── 가공 상태 ──────────────────────────────────────────────
    private Recipe activeRecipe;
    private float progress;                 // 초
    private bool recipeDirty = true;        // 입력이 바뀌어 레시피를 다시 찾아야 함
    private DefaultMachineUI boundUI;       // 현재 이 기계를 표시 중인 패널(없으면 null)

    /// <summary>진행 중인 레시피(없으면 null).</summary>
    public Recipe ActiveRecipe => activeRecipe;

    /// <summary>진행도 0~1. 진행 중이 아니면 0.</summary>
    public float ProgressRatio => activeRecipe != null && activeRecipe.craftTime > 0f
        ? Mathf.Clamp01(progress / activeRecipe.craftTime)
        : 0f;

    // UI가 읽는 공개 설정값
    public int InputCount => inputSlotCount;
    public int OutputCount => outputSlotCount;
    public int FuelCount => fuelSlotCount;
    public int InputGasCount => InputGasSlotCount;
    public int OutputGasCount => OutputGasSlotCount;
    public bool UsesEnergy => IsUseEnergy;
    /// <summary>모든 가스 슬롯(입력/출력)이 공유하는 최대 저장량.</summary>
    public float MaxGas => MaxGasAmount;
    public float MaxEnergy => MaxEnergyAmount;

    public void Bind(PlaceableRecord record, Vector2Int worldCell)
    {
        Record = record;
        this.worldCell = worldCell;
        blockId = record.blockId;

        ApplyConfig(ItemDictionary.Instance != null ? ItemDictionary.Instance.GetMachineInfo(blockId) : null);

        if (inventory != null) inventory.OnChanged -= HandleInventoryChanged;
        inventory = new MachineInventory(inputSlotCount, outputSlotCount, fuelSlotCount);
        inventory.OnChanged += HandleInventoryChanged;

        LoadFrom(record);

        inputGas = CreateGasSlots(InputGasSlotCount);
        outputGas = CreateGasSlots(OutputGasSlotCount);
        // 전력은 여기서 0으로 리셋하지 않는다 — LoadFrom 이 레코드에서 복원한 값을 지워 버리게 된다.

        activeRecipe = null;
        progress = 0f;
        recipeDirty = true;
    }

    private static Gas[] CreateGasSlots(int count)
    {
        if (count <= 0) return System.Array.Empty<Gas>();
        Gas[] slots = new Gas[count];
        for (int i = 0; i < count; i++) slots[i] = new Gas();
        return slots;
    }

    private void OnDestroy()
    {
        if (inventory != null) inventory.OnChanged -= HandleInventoryChanged;
        if (boundUI != null) { boundUI.DetachInstance(this); boundUI = null; }
    }

    /// <summary>슬롯 내용이 바뀌면 레시피를 다시 찾고, 열려 있는 UI 도 갱신한다.</summary>
    private void HandleInventoryChanged()
    {
        recipeDirty = true;
        if (boundUI != null) boundUI.RefreshSlots();
    }

    /// <summary>MachineBlock 설정을 적용한다. 없거나 카운트 미설정이면 기본값으로 폴백.</summary>
    private void ApplyConfig(MachineBlock info)
    {
        Info = info;

        // 연료 칸만 있고 입출력이 0인 기계(발전기)도 "미설정"으로 보면 안 된다.
        // 폴백으로 넘어가면 fuelSlotCount 까지 0 으로 덮여 발전기가 조용히 죽는다.
        if (info != null && (info.AllowsZeroSlots || info.inputSlotCount > 0 || info.outputSlotCount > 0 || info.fuelSlotCount > 0))
        {
            inputSlotCount = info.inputSlotCount;
            outputSlotCount = info.outputSlotCount;
            fuelSlotCount = info.fuelSlotCount;
            fuelBurnRate = info.fuelBurnRate;
            InputGasSlotCount = info.inputGasSlotCount;
            OutputGasSlotCount = info.outputGasSlotCount;
            MaxGasAmount = info.maxGasAmount;
            MaxEnergyAmount = info.maxEnergyAmount;
            IsUseEnergy = info.isUseEnergy;
            isGenerator = info.IsGenerator;
            powerRange = info.powerRange;
            energyUseRate = info.EnergyUseRate;
        }
        else
        {
            inputSlotCount = DefaultInputCount;
            outputSlotCount = DefaultOutputCount;
            fuelSlotCount = 0;
            fuelBurnRate = 0f;
            InputGasSlotCount = 0;
            OutputGasSlotCount = 0;
            MaxGasAmount = 0f;
            MaxEnergyAmount = 0f;
            IsUseEnergy = false;
            isGenerator = false;
            powerRange = 0;
            energyUseRate = 0f;
        }
    }

    /// <summary>레코드 → 런타임 인벤토리로 복원(아이템은 ItemDictionary 로 이름 조회).</summary>
    public void LoadFrom(PlaceableRecord record)
    {
        LoadSlots(inventory.inputSlots, record.inputItemNames, record.inputCounts, record.inputInstances);
        LoadSlots(inventory.outputSlots, record.outputItemNames, record.outputCounts, record.outputInstances);
        LoadSlots(inventory.fuelSlots, record.fuelItemNames, record.fuelCounts, record.fuelInstances);

        burnRemaining = record.burnRemaining;
        burnTotal = record.burnTotal;

        currentEnergy = Mathf.Clamp(record.energy, 0f, MaxEnergyAmount);
        links.Clear();
        if (record.links != null) links.AddRange(record.links);
        roundRobinCursor = record.roundRobinCursor;
        if (roundRobinCursor >= links.Count) roundRobinCursor = 0;
    }

    private static void LoadSlots(System.Collections.Generic.List<ItemStack> slots,
        string[] names, int[] counts, ItemInstance[] instances)
    {
        int cap = names != null ? names.Length : 0;
        for (int i = 0; i < slots.Count; i++)
        {
            ItemStack stack = slots[i];
            if (i < cap && !string.IsNullOrEmpty(names[i]) && counts[i] > 0)
            {
                stack.item = ItemDictionary.Instance != null ? ItemDictionary.Instance.GetItem(names[i]) : null;
                stack.count = stack.item != null ? counts[i] : 0;
                stack.instance = stack.item != null && instances != null && i < instances.Length ? instances[i] : null;
            }
            else
            {
                stack.Clear();
            }
        }
    }

    /// <summary>런타임 인벤토리 → 레코드로 직렬화(저장/디스폰 전 호출).</summary>
    public void WriteBack(PlaceableRecord record)
    {
        record.blockId = blockId;
        WriteSlots(inventory.inputSlots, ref record.inputItemNames, ref record.inputCounts, ref record.inputInstances);
        WriteSlots(inventory.outputSlots, ref record.outputItemNames, ref record.outputCounts, ref record.outputInstances);
        WriteSlots(inventory.fuelSlots, ref record.fuelItemNames, ref record.fuelCounts, ref record.fuelInstances);

        record.burnRemaining = burnRemaining;
        record.burnTotal = burnTotal;

        record.energy = currentEnergy;
        record.roundRobinCursor = roundRobinCursor;
        record.links = links.Count > 0 ? links.ToArray() : System.Array.Empty<Vector2Int>();
    }

    private static void WriteSlots(System.Collections.Generic.List<ItemStack> slots,
        ref string[] names, ref int[] counts, ref ItemInstance[] instances)
    {
        int cap = slots.Count;
        if (names == null || names.Length != cap)
        {
            names = new string[cap];
            counts = new int[cap];
        }
        if (instances == null || instances.Length != cap) instances = new ItemInstance[cap];

        for (int i = 0; i < cap; i++)
        {
            ItemStack stack = slots[i];
            bool has = stack.item != null && stack.count > 0;
            names[i] = has ? stack.item.itemName : "";
            counts[i] = has ? stack.count : 0;
            instances[i] = has ? stack.instance : null;
        }
    }

    /// <summary>보유 레코드로 즉시 동기화.</summary>
    public void Flush()
    {
        if (Record != null) WriteBack(Record);
    }

    // ── 가공 (input → progress → output) ────────────────────────
    private void Update()
    {
        if (inventory == null) return;
        if (isGenerator) { TickGenerator(Time.deltaTime); return; }   // 발전기는 레시피를 보지 않는다
        if (Info != null && !Info.AutoProcess) return;                // 조합대는 버튼을 눌러야 만든다
        Tick(Time.deltaTime);
    }

    private void Tick(float deltaTime)
    {
        if (activeRecipe == null)
        {
            if (!recipeDirty) return;      // 입력이 그대로면 매 프레임 다시 훑지 않는다
            recipeDirty = false;

            activeRecipe = SelectRecipe();
            progress = 0f;
            PushProgress();
            if (activeRecipe == null) return;
        }

        // 진행 중에 재료나 도구를 빼가면 취소하고 처음부터
        if (!RecipeSolver.CanCraft(inventory.inputSlots, activeRecipe))
        {
            activeRecipe = null;
            progress = 0f;
            recipeDirty = true;
            PushProgress();
            return;
        }

        // 연료를 쓰는 기계는 불이 붙어 있는 동안에만 진행한다.
        if (UsesFuel && !BurnFuel(deltaTime))
        {
            PushProgress();
            return;
        }

        // 전력을 쓰는 기계는 전력이 남아 있는 동안에만 진행한다.
        // 여기까지 왔다는 건 레시피가 잡혀 있고 재료도 있다는 뜻이라, 놀고 있는 기계는 전기를 먹지 않는다.
        if (IsUseEnergy && !ConsumeEnergy(deltaTime))
        {
            PushProgress();
            return;
        }

        progress += deltaTime;
        if (progress < activeRecipe.craftTime)
        {
            PushProgress();
            return;
        }

        // 완료 시점에 출력이 가득 차 있으면 진행도를 유지한 채 자리가 날 때까지 대기한다.
        if (!RecipeSolver.CanStoreOutputs(inventory.outputSlots, activeRecipe))
        {
            progress = activeRecipe.craftTime;
            PushProgress();
            return;
        }

        // 재료 소모는 완료 시점에 한다(가공 도중 기계가 디스폰돼도 재료가 사라지지 않도록).
        RecipeSolver.ConsumeInputs(inventory.inputSlots, activeRecipe);
        RecipeSolver.ConsumeTools(inventory.inputSlots, activeRecipe);   // 도구는 내구도만 닳는다
        RecipeSolver.StoreOutputs(inventory.outputSlots, activeRecipe);

        activeRecipe = null;
        progress = 0f;
        recipeDirty = true;

        Flush();                     // 레코드 동기화(중간 저장/디스폰 대비)
        inventory.NotifyChanged();   // 슬롯 뷰 갱신 + recipeDirty 재설정
        PushProgress();
    }

    /// <summary>
    /// 이 프레임만큼 연료를 태운다. 불이 꺼져 있으면 연료 칸에서 하나 집어 불을 붙인다.
    /// 태울 연료가 없으면 false(진행 정지).
    /// </summary>
    private bool BurnFuel(float deltaTime) => BurnFuel(deltaTime, out _);

    /// <summary>
    /// <see cref="BurnFuel(float)"/> 와 같되 <paramref name="burned"/> 로 <b>실제로 태운 양</b>을 돌려준다.
    /// 연료가 다 타는 마지막 프레임에는 요청량보다 적게 타므로, 발전기가 없는 전력을 만들지 않으려면 이 값을 써야 한다.
    /// </summary>
    private bool BurnFuel(float deltaTime, out float burned)
    {
        burned = 0f;
        if (burnRemaining <= 0f && !Ignite()) return false;

        float want = fuelBurnRate * deltaTime;
        burned = Mathf.Min(want, burnRemaining);

        burnRemaining -= want;
        if (burnRemaining <= 0f)
        {
            // 이번 프레임 분은 태웠으니 진행은 허용하고, 다음 프레임에 다시 불을 붙인다.
            burnRemaining = 0f;
            burnTotal = 0f;
        }
        PushFuel();
        return true;
    }

    /// <summary>이 프레임 분의 전력을 쓴다. 모자라면 false(진행 정지).</summary>
    private bool ConsumeEnergy(float deltaTime)
    {
        float need = energyUseRate * deltaTime;
        if (need <= 0f) return true;           // 소비량이 설정되지 않은 기계는 막지 않는다
        if (currentEnergy < need) return false;

        SetEnergy(currentEnergy - need);
        return true;
    }

    // ── 발전 (연료 → 버퍼 → 연결된 기계) ────────────────────────
    // 매 프레임 새로 할당하지 않도록 재사용하는 작업용 목록.
    private readonly System.Collections.Generic.List<MachineInstance> receivers = new();
    private readonly System.Collections.Generic.List<Vector2Int> deadLinks = new();

    /// <summary>
    /// 발전기 한 프레임: 버퍼에 자리가 있을 때만 연료를 태워 채우고, 연결된 기계에 나눠 준다.
    /// 버퍼가 가득 차면 태우지 않는다 — 아무도 안 쓰는데 석탄이 녹아 없어지면 안 되기 때문.
    /// </summary>
    private void TickGenerator(float deltaTime)
    {
        if (currentEnergy < MaxEnergyAmount && BurnFuel(deltaTime, out float burned) && burned > 0f)
            SetEnergy(currentEnergy + burned);   // SetEnergy 가 클램프와 UI 반영까지 맡는다

        Distribute();
    }

    /// <summary>
    /// 연결된 기계에 전력을 나눠 준다. 커서에서 시작해 한 바퀴 돌며(라운드로빈)
    /// 꽉 찬 곳과 사라진 곳은 빼고, 받을 수 있는 대상들에게 균등하게 분배한다.
    /// 전송 자체에는 비용이 없다 — 받은 기계가 가공하며 쓸 뿐이다.
    /// </summary>
    private void Distribute()
    {
        if (links.Count == 0 || currentEnergy <= 0f) return;

        MapGenerator map = MapGenerator.Active;
        if (map == null) return;

        receivers.Clear();
        deadLinks.Clear();

        for (int i = 0; i < links.Count; i++)
        {
            Vector2Int cell = links[(roundRobinCursor + i) % links.Count];

            if (!map.TryGetMachineAt(cell, out MachineInstance target) || target == null)
            {
                // 청크는 로드돼 있는데 기계가 없다 = 캐서 사라진 것이므로 링크를 지운다.
                // 언로드된 청크라면 있는지 알 수 없으니 그대로 둔다(돌아왔을 때 연결이 살아 있어야 한다).
                if (map.IsCellLoaded(cell)) deadLinks.Add(cell);
                continue;
            }

            if (!target.UsesEnergy) continue;                        // 전력을 안 쓰는 기계
            if (target.CurrentEnergy >= target.MaxEnergy) continue;   // 꽉 참 — 이번 바퀴에서 제외
            receivers.Add(target);
        }

        if (deadLinks.Count > 0)
        {
            for (int i = 0; i < deadLinks.Count; i++) links.Remove(deadLinks[i]);
            if (roundRobinCursor >= links.Count) roundRobinCursor = 0;
            Flush();   // 링크가 실제로 바뀔 때만 레코드에 반영한다
        }

        if (receivers.Count == 0) return;

        // 남은 전력을 균등 분배한다. 자리가 모자란 대상은 덜 받고, 남은 몫은 다음 대상에게 넘어간다.
        float remaining = currentEnergy;
        for (int i = 0; i < receivers.Count && remaining > 0f; i++)
        {
            MachineInstance target = receivers[i];
            float share = remaining / (receivers.Count - i);
            float give = Mathf.Min(share, target.MaxEnergy - target.CurrentEnergy);
            if (give <= 0f) continue;

            target.SetEnergy(target.CurrentEnergy + give);
            remaining -= give;
        }
        SetEnergy(remaining);

        if (links.Count > 0) roundRobinCursor = (roundRobinCursor + 1) % links.Count;   // 다음 프레임은 다음 대상부터
    }

    /// <summary>연료 칸에서 하나를 소모해 불을 붙인다. 연료가 없으면 false.</summary>
    private bool Ignite()
    {
        for (int i = 0; i < inventory.fuelSlots.Count; i++)
        {
            ItemStack stack = inventory.fuelSlots[i];
            if (stack.item == null || stack.count <= 0 || !stack.item.IsFuel) continue;

            burnTotal = stack.item.burnEnergy;
            burnRemaining = burnTotal;

            stack.count--;
            if (stack.count <= 0) stack.Clear();
            inventory.NotifyChanged();
            return true;
        }
        return false;
    }

    /// <summary>레시피를 찾을 때 쓰는 키. 0/1/2티어 화로처럼 업그레이드 관계면 같은 목록을 본다.</summary>
    public string RecipeKey => Info != null ? Info.RecipeGroupId : blockId;

    /// <summary>이 기계가 처리할 수 있는 최대 레시피 티어.</summary>
    public int Tier => Info != null ? Info.tier : 0;

    /// <summary>입력 슬롯으로 지금 만들 수 있는 첫 레시피를 고른다. 티어가 모자란 레시피는 건너뛴다.</summary>
    private Recipe SelectRecipe()
    {
        RecipeDictionary dictionary = RecipeDictionary.Instance;
        if (dictionary == null) return null;

        System.Collections.Generic.IReadOnlyList<Recipe> candidates = dictionary.GetRecipesFor(RecipeKey);
        for (int i = 0; i < candidates.Count; i++)
        {
            Recipe recipe = candidates[i];
            if (recipe == null || recipe.tier > Tier) continue;
            if (RecipeSolver.CanCraft(inventory.inputSlots, recipe)) return recipe;
        }
        return null;
    }

    // ── UI 연동 (기계가 자기 UI를 직접 구동한다) ─────────────────
    /// <summary>이 기계를 표시하기 시작한 패널을 연결한다(패널이 열릴 때 호출).</summary>
    public void AttachUI(DefaultMachineUI ui)
    {
        boundUI = ui;
        PushProgress();
        PushFuel();
        PushEnergy();
    }

    /// <summary>표시가 끝난 패널의 연결을 해제한다.</summary>
    public void DetachUI(DefaultMachineUI ui)
    {
        if (boundUI == ui) boundUI = null;
    }

    private void PushProgress()
    {
        if (boundUI == null) return;
        boundUI.SetProgress(ProgressRatio);
    }

    private void PushFuel()
    {
        if (boundUI == null) return;
        boundUI.SetFuel(BurnRatio);
    }

    private void PushEnergy()
    {
        if (boundUI == null) return;
        boundUI.SetEnergy(EnergyRatio);
    }

    // ── 가스 · 에너지 접근 ──────────────────────────────────────
    /// <summary>입력 가스 슬롯(범위 밖이면 null).</summary>
    public Gas GetInputGas(int index)
        => index >= 0 && index < inputGas.Length ? inputGas[index] : null;

    /// <summary>출력 가스 슬롯(범위 밖이면 null).</summary>
    public Gas GetOutputGas(int index)
        => index >= 0 && index < outputGas.Length ? outputGas[index] : null;

    /// <summary>가스 보유량을 최대치 기준 0~1 로 환산한다(모든 가스 슬롯이 MaxGas 를 공유).</summary>
    public float GasRatio(Gas gas)
        => gas != null && MaxGasAmount > 0f ? Mathf.Clamp01(gas.amount / MaxGasAmount) : 0f;

    /// <summary>에너지 보유량을 설정하고 열려 있는 UI 에 반영한다.</summary>
    public void SetEnergy(float amount)
    {
        currentEnergy = Mathf.Clamp(amount, 0f, MaxEnergyAmount);
        PushEnergy();
    }

    /// <summary>입력 가스 슬롯의 종류/보유량을 설정하고 열려 있는 UI 에 반영한다.</summary>
    public void SetInputGas(int index, GasDefine gas, float amount)
    {
        if (!AssignGas(inputGas, index, gas, amount)) return;
        if (boundUI != null) boundUI.SetInputGas(index, GasRatio(inputGas[index]));
    }

    /// <summary>출력 가스 슬롯의 종류/보유량을 설정하고 열려 있는 UI 에 반영한다.</summary>
    public void SetOutputGas(int index, GasDefine gas, float amount)
    {
        if (!AssignGas(outputGas, index, gas, amount)) return;
        if (boundUI != null) boundUI.SetOutputGas(index, GasRatio(outputGas[index]));
    }

    private bool AssignGas(Gas[] slots, int index, GasDefine gas, float amount)
    {
        if (index < 0 || index >= slots.Length) return false;

        float clamped = Mathf.Clamp(amount, 0f, MaxGasAmount);
        slots[index].gas = clamped > 0f ? gas : null;   // 비면 종류도 지운다
        slots[index].amount = clamped;
        return true;
    }
}
