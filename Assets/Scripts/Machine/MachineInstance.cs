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

    public string blockId;
    public Vector2Int worldCell;
    public MachineInventory inventory { get; private set; }
    public PlaceableRecord Record { get; private set; }

    /// <summary>이 기계의 블록 정보(딕셔너리 미구성 시 null). 조합대 티어 등 파생 설정을 읽는 데 쓴다.</summary>
    public MachineBlock Info { get; private set; }

    // ── 가스 · 에너지 보유량 ────────────────────────────────────
    // 아직 생산/소비 로직이 없어 항상 0 이지만, UI(툴팁·바)가 읽을 실체를 먼저 만들어 둔다.
    // PlaceableRecord 에는 넣지 않는다(세이브 버전을 올려야 하므로 로직을 붙일 때 함께 처리).
    private float currentEnergy;
    private Gas[] inputGas = System.Array.Empty<Gas>();
    private Gas[] outputGas = System.Array.Empty<Gas>();

    public float CurrentEnergy => currentEnergy;
    public float EnergyRatio => MaxEnergyAmount > 0f ? Mathf.Clamp01(currentEnergy / MaxEnergyAmount) : 0f;

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
        currentEnergy = 0f;

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

        if (info != null && (info.AllowsZeroSlots || info.inputSlotCount > 0 || info.outputSlotCount > 0))
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
        if (Info != null && !Info.AutoProcess) return;   // 조합대는 버튼을 눌러야 만든다
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
    private bool BurnFuel(float deltaTime)
    {
        if (burnRemaining <= 0f && !Ignite()) return false;

        burnRemaining -= fuelBurnRate * deltaTime;
        if (burnRemaining <= 0f)
        {
            // 이번 프레임 분은 태웠으니 진행은 허용하고, 다음 프레임에 다시 불을 붙인다.
            burnRemaining = 0f;
            burnTotal = 0f;
        }
        PushFuel();
        return true;
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
        if (boundUI != null) boundUI.SetEnergy(EnergyRatio);
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
