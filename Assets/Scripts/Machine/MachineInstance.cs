using UnityEngine;

/// <summary>
/// 월드에 배치된 기계 하나의 런타임 인스턴스. 자기 인벤토리(<see cref="MachineInventory"/>)를 보유하며,
/// 청크의 <see cref="PlaceableRecord"/> 와 <see cref="LoadFrom"/>/<see cref="WriteBack"/> 로 동기화된다.
/// 슬롯 개수·유체 탱크·에너지 설정은 blockId 로 ItemDictionary(MachineBlock)에서 가져온다.
/// </summary>
public class MachineInstance : MonoBehaviour
{
    // 기계 정보 미입력(딕셔너리 미구성) 상태에서도 현행 기계가 동작하도록 하는 폴백.
    private const int DefaultInputCount = 3;
    private const int DefaultOutputCount = 6;

    private int inputSlotCount;
    private int outputSlotCount;
    private int fuelSlotCount;
    private int upgradeSlotCount;
    private float fuelBurnRate;
    private int inputTankCount;
    private int outputTankCount;
    private int maxFluidAmount;   // 탱크 <b>한 칸</b>의 최대치(1 양동이 = 1000 규약)
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

    // ── 유체 탱크 · 에너지 보유량 ───────────────────────────────
    private float currentEnergy;

    // 탱크는 <see cref="ItemStack"/> 슬롯과 같은 자리다 — 레시피 소모·산출, 파이프 입출력, 세이브를 모두 탄다.
    private readonly System.Collections.Generic.List<FluidStack> inputTanks = new();
    private readonly System.Collections.Generic.List<FluidStack> outputTanks = new();

    /// <summary>입력 탱크 목록(파이프·RecipeSolver 가 직접 다룬다).</summary>
    public System.Collections.Generic.IList<FluidStack> InputTanks => inputTanks;

    /// <summary>출력 탱크 목록(파이프가 여기서 퍼 간다).</summary>
    public System.Collections.Generic.IList<FluidStack> OutputTanks => outputTanks;

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

    // ── 인스턴스별 티어 (코어 조합기 업그레이드) ─────────────────
    private int recordTier;

    // ── 가공 상태 ──────────────────────────────────────────────
    private Recipe activeRecipe;
    private float progress;                 // 초
    private bool recipeDirty = true;        // 입력이 바뀌어 레시피를 다시 찾아야 함
    private DefaultMachineUI boundUI;       // 현재 이 기계를 표시 중인 패널(없으면 null)

    /// <summary>진행 중인 레시피(없으면 null).</summary>
    public Recipe ActiveRecipe => activeRecipe;

    /// <summary>
    /// 이 기계에서 지금 레시피가 실제로 걸리는 시간(초). <see cref="MachineBlock.speedMultiplier"/> 로 나눈 값이다.
    ///
    /// <b>진행도 비교·진행률 표시·수동 한 걸음 크기가 모두 이 하나를 봐야 한다.</b>
    /// 세 곳이 각자 craftTime 을 나누면 언젠가 한 곳만 고쳐져 "게이지는 다 찼는데 안 나온다" 가 된다.
    /// </summary>
    public float EffectiveCraftTime
    {
        get
        {
            if (activeRecipe == null) return 0f;
            float speed = (Info != null ? Info.speedMultiplier : 1f) * SpeedFactor;
            return speed > 0f ? activeRecipe.craftTime / speed : activeRecipe.craftTime;
        }
    }

    // ── 업그레이드 모듈 ────────────────────────────────────────
    // 배수는 <b>캐시하지 않는다</b>. 모듈을 꽂자마자 반영돼야 하는데, ApplyConfig 처럼 Bind 시점에
    // 복사해 두면 창을 닫았다 열기 전까지 옛 값이 계속 쓰인다(energyUseRate 가 그 함정에 있다).
    // 칸이 2개뿐이라 매 프레임 세도 비용이 없다.

    /// <summary>속도 모듈의 합. 1 이면 배수 없음, 2 면 두 배 빠름.</summary>
    private float SpeedFactor => 1f + UpgradeSum(UpgradeKind.Speed);

    /// <summary>
    /// 효율 모듈의 합. 소비에 <b>곱하는</b> 값이라 1 이 기본이고 작을수록 덜 쓴다.
    /// 0 이 되면 연료·전력이 공짜가 되므로 0.1 에서 막는다.
    /// </summary>
    private float EfficiencyFactor => Mathf.Max(0.1f, 1f - UpgradeSum(UpgradeKind.Efficiency));

    private float UpgradeSum(UpgradeKind kind)
    {
        if (inventory == null || inventory.upgradeSlots == null) return 0f;

        float total = 0f;
        for (int i = 0; i < inventory.upgradeSlots.Count; i++)
        {
            ItemStack stack = inventory.upgradeSlots[i];
            if (stack == null || stack.count <= 0) continue;
            if (stack.item is not UpgradeModuleItem module || module.kind != kind) continue;
            total += module.valuePerUnit * stack.count;
        }
        return total;
    }

    /// <summary>모듈 <b>개수</b>의 합. 용량처럼 valuePerUnit(비율)이 아니라 개수로 세는 값이 쓴다.</summary>
    private int UpgradeModuleCount(UpgradeKind kind)
    {
        if (inventory == null || inventory.upgradeSlots == null) return 0;

        int total = 0;
        for (int i = 0; i < inventory.upgradeSlots.Count; i++)
        {
            ItemStack stack = inventory.upgradeSlots[i];
            if (stack == null || stack.count <= 0) continue;
            if (stack.item is not UpgradeModuleItem module || module.kind != kind) continue;
            total += stack.count;
        }
        return total;
    }

    // ── 저장 블록 ──────────────────────────────────────────────

    /// <summary>저장 전용 블록인가(상자·아이템 저장소). 파이프 규칙과 슬롯 용량이 여기서 갈린다.</summary>
    public bool IsStorage => Info is StorageBlock;

    /// <summary>
    /// 개체 데이터(<see cref="ItemInstance"/>)가 붙은 아이템을 받아도 되는가.
    /// 고유 최대치를 쓰는 저장소는 <b>안 된다</b> — 한 칸에 수천 개가 들어가는데 인스턴스는 하나뿐이라,
    /// 그 하나가 닳아 없어질 때 <c>stack.Clear()</c> 로 전부가 통째로 사라진다
    /// (<see cref="RecipeSolver.AddItems"/> 의 칸당 1개 규칙과 같은 이유).
    /// 상자는 칸이 40개고 maxStack 을 따르므로 그 규칙이 그대로 통해 받아도 된다.
    /// </summary>
    public bool AcceptsInstanceItems => Info is not StorageBlock storage || !storage.HasOwnCapacity;

    /// <summary>
    /// 저장 칸(= 입력 구간) 한 칸의 최대치. 저장 블록이 아니면 아이템의 maxStack 그대로다.
    ///
    /// <b>캐시하지 않는다</b> — 업그레이드 모듈을 꽂자마자 반영돼야 한다
    /// (<see cref="SpeedFactor"/> · <see cref="EfficiencyFactor"/> 와 같은 규약).
    /// </summary>
    public int InputSlotCapacity(Items item)
    {
        if (Info is not StorageBlock storage || !storage.HasOwnCapacity) return RecipeSolver.MaxStackOf(item);

        // long 으로 곱한다 — 에셋 값이 크면 int 로 넘쳐 음수가 되고, 호출자가 "자리 없음" 으로 읽는다
        // (CountFreeSpace 가 같은 이유로 long 을 쓴다).
        long capacity = (long)storage.baseCapacity
                      + (long)storage.capacityPerUpgrade * UpgradeModuleCount(UpgradeKind.Efficiency);
        return capacity > int.MaxValue ? int.MaxValue : (int)capacity;
    }

    /// <summary>
    /// 평면 인덱스 한 칸의 최대치. <see cref="MachineInventory.capacityOverride"/> 에 꽂힌다.
    /// <b>입력 구간만</b> 고유 최대치를 쓴다 — 연료·업그레이드 칸은 언제나 maxStack 이다.
    /// </summary>
    private int SlotCapacityFor(int index, Items item)
        => index >= 0 && index < inputSlotCount ? InputSlotCapacity(item) : RecipeSolver.MaxStackOf(item);

    /// <summary>진행도 0~1. 진행 중이 아니면 0.</summary>
    public float ProgressRatio
    {
        get
        {
            float total = EffectiveCraftTime;
            return total > 0f ? Mathf.Clamp01(progress / total) : 0f;
        }
    }

    // UI가 읽는 공개 설정값
    public int InputCount => inputSlotCount;
    public int OutputCount => outputSlotCount;
    public int FuelCount => fuelSlotCount;
    public int UpgradeCount => upgradeSlotCount;
    public int InputTankCount => inputTankCount;
    public int OutputTankCount => outputTankCount;
    public bool UsesEnergy => IsUseEnergy;
    /// <summary>탱크 한 칸의 최대 저장량.</summary>
    public int MaxFluid => maxFluidAmount;
    public float MaxEnergy => MaxEnergyAmount;

    // ── 가동 표시 ────────────────────────────────────────────────
    // 프리팹의 그림. 프리팹을 만드는 쪽(MachineBlockFiller·FurnaceSetup)이 자식까지 훑어 넣으므로
    // 여기서도 자식을 포함해 찾는다.
    private SpriteRenderer bodyRenderer;
    // 배치 시점의 그림 = 멈춰 있을 때. <b>정지 그림의 정본은 프리팹</b>이라 SO 에 따로 두지 않는다.
    private Sprite idleSprite;
    private bool running;

    public void Bind(PlaceableRecord record, Vector2Int worldCell)
    {
        Record = record;
        this.worldCell = worldCell;
        blockId = record.blockId;

        bodyRenderer = GetComponentInChildren<SpriteRenderer>(true);
        idleSprite = bodyRenderer != null ? bodyRenderer.sprite : null;
        running = false;

        ApplyConfig(ItemDictionary.Instance != null ? ItemDictionary.Instance.GetMachineInfo(blockId) : null);

        if (inventory != null) inventory.OnChanged -= HandleInventoryChanged;
        inventory = new MachineInventory(inputSlotCount, outputSlotCount, fuelSlotCount, upgradeSlotCount);
        inventory.OnChanged += HandleInventoryChanged;
        // 숫자가 아니라 함수를 꽂는다 — 저장소 최대치는 업그레이드 모듈에 따라 런타임에 바뀐다.
        inventory.capacityOverride = SlotCapacityFor;

        // <b>탱크는 ApplyConfig 안에서 만든다.</b> 여기(LoadFrom 다음)에서 만들면
        // 레코드에서 복원한 유체가 매번 지워진다 — 전력·진행도가 정확히 이 자리에서 사라졌던 이력이 있다.
        LoadFrom(record);

        // 전력은 여기서 0으로 리셋하지 않는다 — LoadFrom 이 레코드에서 복원한 값을 지워 버리게 된다.
        // <b>진행도도 같다.</b> 예전엔 여기서 progress = 0 을 했는데, LoadFrom 다음이라
        // v10 에서 복원한 진행도가 매번 지워졌다(수동 기계는 20번 누른 것이 통째로 사라진다).

        activeRecipe = null;   // 레시피는 저장하지 않는다. Tick 이 다시 고르고 progress 를 craftTime 으로 자른다
        recipeDirty = true;
    }

    /// <summary>탱크 목록의 칸 수를 맞춘다(내용은 건드리지 않는다).</summary>
    private static void ResizeTanks(System.Collections.Generic.List<FluidStack> tanks, int count)
    {
        if (count < 0) count = 0;
        while (tanks.Count > count) tanks.RemoveAt(tanks.Count - 1);
        while (tanks.Count < count) tanks.Add(new FluidStack());
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
        // ⚠ <b>IsGenerator 도 함께 봐야 한다</b> — 지열 발전기는 연료 칸마저 0 이라 위 세 조건에
        //    하나도 안 걸리고, 폴백에서 isGenerator 가 지워져 <b>발전을 아예 안 했다</b>.
        if (info != null && (info.AllowsZeroSlots || info.IsGenerator
                             || info.inputSlotCount > 0 || info.outputSlotCount > 0 || info.fuelSlotCount > 0))
        {
            inputSlotCount = info.inputSlotCount;
            outputSlotCount = info.outputSlotCount;
            fuelSlotCount = info.fuelSlotCount;
            upgradeSlotCount = info.upgradeSlotCount;
            fuelBurnRate = info.fuelBurnRate;
            inputTankCount = info.inputFluidSlotCount;
            outputTankCount = info.outputFluidSlotCount;
            maxFluidAmount = info.maxFluidAmount;
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
            upgradeSlotCount = 0;
            fuelBurnRate = 0f;
            inputTankCount = 0;
            outputTankCount = 0;
            maxFluidAmount = 0;
            MaxEnergyAmount = 0f;
            IsUseEnergy = false;
            isGenerator = false;
            powerRange = 0;
            energyUseRate = 0f;
        }

        // 저장 블록은 저장 칸을 <b>입력 구간에 얹는다</b>(새 구간을 만들면 세이브·드랍·평면 인덱스가 다 따라 늘어난다).
        // 에셋에서 inputSlotCount 와 storageSlotCount 를 맞춰 둘 필요가 없도록 여기서 덮어쓴다.
        if (info is StorageBlock storage)
        {
            inputSlotCount = storage.storageSlotCount;
            outputSlotCount = 0;
        }

        // 탱크는 여기서 만든다 — Bind 가 LoadFrom <b>뒤에</b> 만들면 복원한 유체를 매번 덮어쓴다.
        ResizeTanks(inputTanks, inputTankCount);
        ResizeTanks(outputTanks, outputTankCount);
    }

    /// <summary>레코드 → 런타임 인벤토리로 복원(아이템은 ItemDictionary 로 이름 조회).</summary>
    public void LoadFrom(PlaceableRecord record)
    {
        LoadSlots(inventory.inputSlots, record.inputItemNames, record.inputCounts, record.inputInstances);
        LoadSlots(inventory.outputSlots, record.outputItemNames, record.outputCounts, record.outputInstances);
        LoadSlots(inventory.fuelSlots, record.fuelItemNames, record.fuelCounts, record.fuelInstances);
        LoadSlots(inventory.upgradeSlots, record.upgradeItemNames, record.upgradeCounts, record.upgradeInstances);

        // 인스턴스별 티어(코어 조합기 업그레이드). <b>여기서 복원한 값을 Bind 가 다시 0 으로 밀면 안 된다</b>
        // — 전력·진행도가 정확히 그렇게 사라졌던 이력이 있다.
        recordTier = Mathf.Max(0, record.tier);

        burnRemaining = record.burnRemaining;
        burnTotal = record.burnTotal;

        // 진행도는 복원만 하고 레시피는 다시 고른다. Tick 이 그때 craftTime 으로 잘라 준다
        // (레시피 선택 지점에서 progress 를 0 으로 밀지 않는 이유가 이것이다).
        progress = Mathf.Max(0f, record.progress);

        currentEnergy = Mathf.Clamp(record.energy, 0f, MaxEnergyAmount);
        links.Clear();
        if (record.links != null) links.AddRange(record.links);
        roundRobinCursor = record.roundRobinCursor;
        if (roundRobinCursor >= links.Count) roundRobinCursor = 0;

        LoadTanks(inputTanks, record.inputFluidIds, record.inputFluidAmounts);
        LoadTanks(outputTanks, record.outputFluidIds, record.outputFluidAmounts);
    }

    private void LoadTanks(System.Collections.Generic.List<FluidStack> tanks, string[] ids, int[] amounts)
    {
        int cap = ids != null ? ids.Length : 0;
        for (int i = 0; i < tanks.Count; i++)
        {
            FluidStack tank = tanks[i];
            if (i < cap && !string.IsNullOrEmpty(ids[i]) && amounts != null && i < amounts.Length && amounts[i] > 0)
            {
                // 유체 에셋이 사라졌으면 되살릴 근거가 없다. 아이템 슬롯과 같은 규칙으로 비운다.
                tank.fluid = ItemDictionary.Instance != null ? ItemDictionary.Instance.GetFluid(ids[i]) : null;
                tank.amount = tank.fluid != null ? Mathf.Min(amounts[i], Mathf.Max(1, maxFluidAmount)) : 0;
                if (tank.fluid == null) tank.Clear();
            }
            else tank.Clear();
        }
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
        WriteSlots(inventory.upgradeSlots, ref record.upgradeItemNames, ref record.upgradeCounts, ref record.upgradeInstances);
        record.tier = recordTier;

        record.burnRemaining = burnRemaining;
        record.burnTotal = burnTotal;

        record.energy = currentEnergy;
        record.roundRobinCursor = roundRobinCursor;
        record.links = links.Count > 0 ? links.ToArray() : System.Array.Empty<Vector2Int>();
        record.progress = progress;

        WriteTanks(inputTanks, ref record.inputFluidIds, ref record.inputFluidAmounts);
        WriteTanks(outputTanks, ref record.outputFluidIds, ref record.outputFluidAmounts);
    }

    private static void WriteTanks(System.Collections.Generic.List<FluidStack> tanks, ref string[] ids, ref int[] amounts)
    {
        int cap = tanks.Count;
        if (ids == null || ids.Length != cap) { ids = new string[cap]; amounts = new int[cap]; }
        if (amounts == null || amounts.Length != cap) amounts = new int[cap];

        for (int i = 0; i < cap; i++)
        {
            FluidStack tank = tanks[i];
            bool has = tank != null && !tank.IsEmpty;
            ids[i] = has ? tank.fluid.fluidId : "";
            amounts[i] = has ? tank.amount : 0;
        }
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

    /// <summary>
    /// 가동 표시를 바꾼다. <b>상태가 바뀔 때만</b> 스프라이트를 건드린다 —
    /// 매 프레임 대입하면 SpriteRenderer 가 계속 더티가 되어 배칭이 깨진다.
    ///
    /// <b>가동 그림이 지정되지 않은 기계는 그냥 넘어간다.</b> 47대 중 대부분이 그렇다.
    /// </summary>
    /// <summary>
    /// 지금 실제로 돌고 있는가. <see cref="LightEmitter"/> 가 이걸 보고 빛을 켜고 끈다 —
    /// 판정을 밖에서 다시 하면 그림과 빛이 어긋난다.
    /// </summary>
    public bool IsRunning => running;

    private void SetRunning(bool value)
    {
        if (running == value) return;
        running = value;

        Sprite target = Info != null ? Info.runningSprite : null;
        if (target == null || bodyRenderer == null) return;

        bodyRenderer.sprite = value ? target : idleSprite;
    }

    // ── 가공 (input → progress → output) ────────────────────────
    private void Update()
    {
        if (inventory == null) { SetRunning(false); return; }

        // 양동이 교환은 가공보다 먼저 한다 — 이번 프레임에 넣은 물로 바로 돌 수 있어야 한다.
        // 발전기·조합대도 탱크가 있으면 교환한다(탱크가 0개면 즉시 반환하므로 비용이 없다).
        ExchangeBuckets();

        if (isGenerator) { TickGenerator(Time.deltaTime); return; }   // 발전기는 레시피를 보지 않는다

        // 레시피 없이 그냥 켜져 있는 장치(조명). <b>AutoProcess 조기 return 보다 앞에 있어야 한다.</b>
        // ⚠ 이 분기가 없으면 전등은 영원히 안 켜진다 — Tick 은 activeRecipe 가 없으면 첫 줄에서
        //   되돌아가 ConsumeEnergy 에 닿지 않는다("놀고 있는 기계는 전기를 먹지 않는다"가 의도된 규칙이라,
        //   상시 소비는 그 규칙의 예외로 여기 따로 적는다).
        if (Info != null && Info.IsAlwaysOn)
        {
            // 전력을 쓰는 조명은 전력이 있을 때만, 아닌 것(횃불)은 무조건 켜진다.
            bool powered = !Info.isUseEnergy || ConsumeEnergy(Time.deltaTime);
            SetRunning(powered);

            // 저장 네트워크의 입출력 버스는 <b>컨트롤러만</b> 돌린다.
            // 네트워크당 컨트롤러가 정확히 하나라, 이렇게 하면 망 하나가 프레임당 한 번만 돈다 —
            // 케이블이나 드라이브에서 돌리면 개수만큼 빨라져 밸런스가 배치 모양에 좌우된다.
            if (powered && Info is StorageNetworkBlock device && device.role == StorageNetworkRole.Controller)
                StorageNetwork.PumpBuses(worldCell, Time.deltaTime);

            return;
        }

        if (Info != null && !Info.AutoProcess) { SetRunning(false); return; }   // 조합대는 버튼을 눌러야 만든다

        // 수동 기계는 시간이 아니라 클릭으로 진행한다(ManualStep).
        // 여기서 SetRunning(false) 를 부르므로, 수동 기계에 runningSprite 를 주면
        // 크랭크를 돌린 <b>한 프레임만</b> 보이고 곧바로 정지 그림으로 돌아간다 — 비워 두는 것이 맞다.
        if (Info != null && Info.IsManual) { SetRunning(false); return; }

        Tick(Time.deltaTime);
    }

    /// <summary>
    /// 수동 기계를 버튼 한 번만큼 돌린다. <c>craftTime × manualStepRatio</c> 만큼 진행한다.
    ///
    /// 재료·출력자리·연료·전력 판정은 <see cref="Tick"/> 이 이미 다 하므로 <b>그대로 재사용</b>한다.
    /// 여기에 판정을 또 쓰면 두 곳이 갈라져, 언젠가 한쪽만 고쳐지고 조용히 어긋난다.
    /// </summary>
    public void ManualStep()
    {
        if (inventory == null || Info == null || !Info.IsManual) return;

        if (activeRecipe == null) Tick(0f);   // 레시피부터 고르게 한다(0초라 진행은 없다)
        if (activeRecipe == null) return;     // 지금 만들 수 있는 것이 없다

        Tick(EffectiveCraftTime * Info.manualStepRatio);
    }

    private void Tick(float deltaTime)
    {
        if (activeRecipe == null)
        {
            if (!recipeDirty) { SetRunning(false); return; }   // 입력이 그대로면 매 프레임 다시 훑지 않는다
            recipeDirty = false;

            activeRecipe = SelectRecipe();

            // 여기서 progress 를 0 으로 밀면 <b>세이브에서 복원한 진행도가 지워진다.</b>
            // 완료(아래 progress = 0)·취소(CanCraft 실패) 경로가 이미 0 으로 만들어 두므로,
            // 이 지점에 0 이 아닌 값이 남아 있는 경우는 '로드 직후' 뿐이다. 새 레시피 길이로만 잘라 준다.
            progress = activeRecipe != null ? Mathf.Min(progress, EffectiveCraftTime) : 0f;

            PushProgress();
            if (activeRecipe == null) { SetRunning(false); return; }
        }

        // 진행 중에 재료·도구·유체를 빼가면 취소하고 처음부터
        if (!CanRun(activeRecipe))
        {
            activeRecipe = null;
            progress = 0f;
            recipeDirty = true;
            SetRunning(false);
            PushProgress();
            return;
        }

        // 출력이 가득 차 있으면 <b>연료·전력을 쓰기 전에</b> 멈춘다.
        // 이 검사가 아래(완성 시점)에만 있으면, 진행도가 100% 에 멈춘 채로도 매 프레임 연료가 계속 타고
        // 다 타면 Ignite 가 새 연료를 또 집는다 — 화면에 아무 단서 없이 석탄 한 스택이 통째로 증발한다.
        // 확률 부산물도 함께 자리를 잡아 둔다(blockId 를 넘기는 이유) — 굴린 뒤 자리가 없어 버리지 않도록.
        // 유체 산출도 같은 이유로 여기서 함께 본다 — 탱크가 차 있는데 진행하면 연료·전력만 태운다.
        if (!RecipeSolver.CanStoreOutputs(inventory.outputSlots, activeRecipe, blockId)
            || !RecipeSolver.CanStoreFluids(outputTanks, activeRecipe, maxFluidAmount))
        {
            SetRunning(false);
            PushProgress();
            return;
        }

        // 연료를 쓰는 기계는 불이 붙어 있는 동안에만 진행한다.
        if (UsesFuel && !BurnFuel(deltaTime))
        {
            SetRunning(false);
            PushProgress();
            return;
        }

        // 전력을 쓰는 기계는 전력이 남아 있는 동안에만 진행한다.
        // 여기까지 왔다는 건 레시피가 잡혀 있고 재료도 있다는 뜻이라, 놀고 있는 기계는 전기를 먹지 않는다.
        if (IsUseEnergy && !ConsumeEnergy(deltaTime))
        {
            SetRunning(false);
            PushProgress();
            return;
        }

        // 여기까지 왔으면 이번 프레임에 실제로 일이 진행된다 — 그것이 곧 '가동 중' 이다.
        SetRunning(true);

        progress += deltaTime;
        if (progress < EffectiveCraftTime)
        {
            PushProgress();
            return;
        }

        // 재료 소모는 완료 시점에 한다(가공 도중 기계가 디스폰돼도 재료가 사라지지 않도록).
        RecipeSolver.ConsumeInputs(inventory.inputSlots, activeRecipe);
        RecipeSolver.ConsumeTools(inventory.inputSlots, activeRecipe);   // 도구는 내구도만 닳는다
        RecipeSolver.ConsumeFluids(inputTanks, activeRecipe);
        RecipeSolver.StoreOutputs(inventory.outputSlots, activeRecipe);
        RecipeSolver.StoreFluids(outputTanks, activeRecipe, maxFluidAmount);
        RollChanceOutputs(activeRecipe);
        PushFluids();

        activeRecipe = null;
        progress = 0f;
        recipeDirty = true;

        Flush();                     // 레코드 동기화(중간 저장/디스폰 대비)
        inventory.NotifyChanged();   // 슬롯 뷰 갱신 + recipeDirty 재설정
        PushProgress();
    }

    /// <summary>
    /// 확률 부산물을 굴려 적재한다. <b>항목마다 독립 굴림</b>이라 한 번에 여러 개가 나올 수 있고,
    /// 하나도 안 나올 수도 있다(추출은 원래 그렇다).
    ///
    /// 최종 확률 = 레시피의 기본값 × <see cref="ExtractionTable"/> 배수 × <c>chanceMultiplier</c>.
    /// 배수가 0 이면 그 기계는 그 산출물을 못 얻는다 — 등급 상속도 표가 구현한다.
    /// 자리 확인은 이미 <see cref="RecipeSolver.CanStoreOutputs"/> 가 <b>나올 수 있는 것 전부</b>에 대해
    /// 해 뒀으므로, 여기서 넣지 못하는 일은 없다.
    /// </summary>
    private void RollChanceOutputs(Recipe recipe)
    {
        if (recipe == null || recipe.chanceOutputs == null || recipe.chanceOutputs.Count == 0) return;

        float bonus = Info != null ? Info.chanceMultiplier : 1f;

        for (int i = 0; i < recipe.chanceOutputs.Count; i++)
        {
            ChanceOutput roll = recipe.chanceOutputs[i];
            if (roll == null || roll.item == null || roll.count <= 0) continue;

            float multiplier = ExtractionTable.Multiplier(blockId, roll.item);
            if (multiplier <= 0f) continue;

            float chance = roll.chance * multiplier * bonus;
            if (chance <= 0f) continue;
            if (chance < 1f && Random.value >= chance) continue;   // 1 이상이면 확정

            RecipeSolver.TryAdd(inventory.outputSlots, roll.item, roll.count);
        }
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

        // 불이 꺼져 있으면 다음 연료를 붙이기 <b>전에 직전 연료의 찌꺼기부터 내보낸다.</b>
        // 순서가 뒤바뀌면 찌꺼기가 증발하거나("냈다고 치고" 다음 연료를 붙임)
        // 출력이 차 있어도 계속 태우게 된다.
        if (burnRemaining <= 0f)
        {
            if (!TryEjectSpentFuel()) return false;
            if (!Ignite()) return false;
        }

        // 배수는 <b>소비 시점</b>에 곱한다. fuelBurnRate 는 ApplyConfig 가 복사해 둔 값이라
        // 여기서 곱해야 모듈을 꽂자마자 반영된다.
        //
        // 소비 기계: 효율 모듈이 연료를 덜 쓰게 한다.
        // 발전기   : 태운 양이 곧 발전량이라 소비를 줄이면 출력도 준다 — 대신 <b>속도</b> 모듈이
        //            연소를 빠르게 해 출력을 올린다(총 에너지는 그대로). 효율은 TickGenerator 가 산출 쪽에서 건다.
        float want = fuelBurnRate * deltaTime * (isGenerator ? SpeedFactor : EfficiencyFactor);
        burned = Mathf.Min(want, burnRemaining);

        burnRemaining -= want;
        if (burnRemaining <= 0f)
        {
            // 이번 프레임 분은 태웠으니 진행은 허용하고, 다음 프레임에 다시 불을 붙인다.
            burnRemaining = 0f;

            // ⚠ <b>찌꺼기를 내는 기계는 burnTotal 을 남긴다</b> — "다 태웠는데 아직 안 냈다" 의 표시다.
            //    둘 다 세이브에 있는 필드(v5)라 <b>새 세이브 칸 없이</b> 저장·복원을 건너 살아남는다.
            //    표시로 쓰지 않으면 다 태운 순간과 내보내는 순간 사이에 저장했을 때 찌꺼기가 사라진다.
            if (Info == null || Info.spentFuelItem == null) burnTotal = 0f;
        }
        PushFuel();
        return true;
    }

    /// <summary>
    /// 다 태운 연료의 찌꺼기(<see cref="MachineBlock.spentFuelItem"/>)를 출력 칸에 내놓는다.
    /// 낼 것이 없으면 그냥 true 라 <b>기존 기계 46종의 동작은 한 톨도 바뀌지 않는다</b>(필드가 비어 있다).
    ///
    /// <b>자리가 없으면 false</b> — 부르는 쪽은 점화하지 않아야 한다. 찌꺼기를 버리면 핵연료 사슬이
    /// 조용히 새고, 그렇다고 계속 태우면 "산출이 차면 발전이 멈춘다" 규칙이 성립하지 않는다.
    /// </summary>
    private bool TryEjectSpentFuel()
    {
        Items spent = Info != null ? Info.spentFuelItem : null;
        if (spent == null) return true;

        // 낼 몫이 아직 없어도 <b>자리는 미리 확인한다.</b> 자리가 없는데 새 연료에 불을 붙이면
        // 다 태운 뒤 낼 곳이 없어 찌꺼기를 <b>빚으로 떠안고</b>(burnTotal 에만 남는다),
        // 그 상태로 기계를 캐면 그 한 개가 조용히 사라진다.
        // 이 줄 덕분에 "산출이 차면 발전이 멈춘다" 가 <b>연료를 한 개도 더 안 태우고</b> 성립한다.
        if (burnTotal <= 0f) return RecipeSolver.CountFreeSpace(inventory.outputSlots, spent) > 0;

        if (RecipeSolver.AddItems(inventory.outputSlots, spent, 1) <= 0) return false;

        burnTotal = 0f;
        inventory.NotifyChanged();   // AddItems 는 통지하지 않는다 — 여기서 직접 부른다
        Flush();
        return true;
    }

    /// <summary>이 프레임 분의 전력을 쓴다. 모자라면 false(진행 정지).</summary>
    private bool ConsumeEnergy(float deltaTime)
    {
        // energyUseRate 도 ApplyConfig 가 복사한 값이라, 배수는 여기서 곱해야 즉시 반영된다.
        float need = energyUseRate * deltaTime * EfficiencyFactor;
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
        bool burning = false;
        if (currentEnergy < MaxEnergyAmount)
        {
            if (Info != null && !Info.UsesFuel)
            {
                // <b>연료가 없는 발전기</b>(지열). 땅이 원천이라 태울 것이 없고 멈추지도 않는다.
                // 발전량의 정본은 <see cref="MachineBlock.fuelBurnRate"/> 를 그대로 쓴다 —
                // 연료식에서도 "태운 양 = 발전량" 이므로 필드를 새로 만들면 같은 뜻이 두 곳으로 갈린다.
                // 효율 모듈이 산출 쪽에 걸리는 것도 아래 연료식과 똑같다.
                SetEnergy(currentEnergy + fuelBurnRate * deltaTime * SpeedFactor / EfficiencyFactor);
                burning = true;
            }
            else if (BurnFuel(deltaTime, out float burned) && burned > 0f)
            {
                // 발전기의 효율 모듈은 <b>같은 연료로 더 많은 전력</b>이다(연소량은 BurnFuel 이 그대로 둔다).
                SetEnergy(currentEnergy + burned / EfficiencyFactor);
                burning = true;
            }
        }

        // 연료를 <b>실제로 태운 프레임</b>만 가동이다. 버퍼가 가득 차면 태우지 않으므로(위 조건)
        // 연료가 남아 있어도 정지 그림이 된다 — 그게 실제로 벌어지는 일이다.
        SetRunning(burning);

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

    /// <summary>
    /// <b>조합대</b>가 목록을 거를 때 쓰는 해금 티어. SO 값과 <b>이 인스턴스가 업그레이드로 올린
    /// 티어</b> 중 큰 쪽이다(코어 조합기).
    ///
    /// ⚠ <b>일반 기계의 가공에는 쓰이지 않는다</b> — 기계는 티어와 무관하게 자기 그룹을 전부 처리한다
    /// (<see cref="SelectRecipe"/> 주석 참고). 읽는 곳은 <c>CraftingTableUI</c> 와
    /// <see cref="TryUpgradeTier"/> 뿐이다.
    /// </summary>
    public int Tier => Mathf.Max(Info != null ? Info.tier : 0, recordTier);

    /// <summary>업그레이드로 올린 인스턴스별 티어(레코드에 저장된다). 0 이면 SO 값을 그대로 쓴다.</summary>
    public int RecordTier => recordTier;

    /// <summary>티어 상승 재료를 받는 조합대인가(코어 조합기).</summary>
    public bool AcceptsTierUpgrade => Info is CraftingTableBlock table && table.acceptsTierUpgrade;

    /// <summary>
    /// 업그레이드 칸의 아이템을 하나 소모해 티어를 올린다. 올릴 수 없으면 false.
    ///
    /// 티어는 <b>레코드</b>에 남으므로 코어를 캤다 다시 놓으면 0 으로 돌아간다(레코드가 새로 생긴다) —
    /// 코어 자체가 티어를 들고 있는 것이 아니라 "그 자리에 세운 코어" 가 들고 있다는 뜻이다.
    /// </summary>
    public bool TryUpgradeTier()
    {
        if (!AcceptsTierUpgrade || inventory == null || inventory.upgradeSlots == null) return false;

        for (int i = 0; i < inventory.upgradeSlots.Count; i++)
        {
            ItemStack stack = inventory.upgradeSlots[i];
            if (stack == null || stack.item == null || stack.count <= 0) continue;

            int target = CoreUpgradeTable.TargetTier(stack.item);
            if (target <= Tier) continue;   // 재료가 아니거나 이미 그 티어 이상이다

            recordTier = target;
            if (--stack.count <= 0) stack.Clear();
            inventory.NotifyChanged();
            Flush();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 지금 이 레시피를 돌릴 재료가 갖춰졌는가(아이템 · 도구 · 유체).
    ///
    /// <b><see cref="SelectRecipe"/> 와 <see cref="Tick"/> 이 같은 것을 봐야 한다.</b>
    /// 고를 때는 아이템만 보고 돌릴 때만 유체를 보면, "물이 없어 영원히 안 도는" 레시피를 골라 놓고
    /// 기계가 통째로 잠긴다(<see cref="SelectRecipe"/> 는 첫 후보를 잡으면 더 안 찾는다).
    /// </summary>
    private bool CanRun(Recipe recipe)
        => RecipeSolver.CanCraft(inventory.inputSlots, recipe) && RecipeSolver.HasFluids(inputTanks, recipe);

    /// <summary>
    /// 입력 슬롯·탱크로 지금 만들 수 있는 첫 레시피를 고른다.
    ///
    /// <b>⚠ 티어로 거르지 않는다.</b> 기계는 자기 그룹의 레시피를 <b>티어와 무관하게 전부 처리</b>한다 —
    /// 분쇄기 한 종류가 돌·마력석·운석을 다 빻고, 화로 한 종류가 모든 광석을 재련한다.
    /// <c>tier</c> 는 <b>"코어 조합대 목록에 언제 나타나는가" 에만</b> 쓰는 값이다
    /// (<see cref="RecipeDictionary.CollectRecipes"/> — 조합대는 <c>AutoProcess == false</c> 라
    /// 여기까지 오지도 않는다).
    ///
    /// 기계를 가르는 것은 <see cref="MachineBlock.recipeGroupId"/> 다 — 예를 들어
    /// <c>blast_steel</c>·<c>blast_titanium</c> 이 <c>Machine:BlastFurnace</c> 그룹에만 있어서
    /// "티타늄·강철만 용광로" 가 티어 게이트 없이도 성립한다.
    /// <b>여기에 티어 조건을 되살리면</b> "2티어 분쇄기가 없어 운석을 못 빻는다" 같은
    /// <b>없는 문제가 다시 생긴다</b>(실제로 그랬다).
    /// </summary>
    private Recipe SelectRecipe()
    {
        RecipeDictionary dictionary = RecipeDictionary.Instance;
        if (dictionary == null) return null;

        System.Collections.Generic.IReadOnlyList<Recipe> candidates = dictionary.GetRecipesFor(RecipeKey);
        for (int i = 0; i < candidates.Count; i++)
        {
            Recipe recipe = candidates[i];
            if (recipe == null) continue;
            if (CanRun(recipe)) return recipe;
        }
        return null;
    }

    // ── 양동이 ↔ 탱크 교환 ──────────────────────────────────────
    /// <summary>
    /// 입력 슬롯의 '채워진 양동이'를 탱크로 빨아들이고, 빈 양동이로 출력 탱크를 퍼낸다.
    ///
    /// 전용 슬롯을 만들지 않은 이유: 평면 인덱스([입력][출력][연료])에 구간이 하나 더 늘면
    /// 세이브·UI·파이프가 전부 따라 늘어난다. 입출력 슬롯을 그대로 쓰면 <b>파이프로 양동이를 넣는 것도
    /// 공짜로 된다</b>(파이프는 이미 입력 슬롯에 아이템을 넣는다).
    ///
    /// 빈 그릇을 놓을 자리가 없으면 <b>아무것도 하지 않는다</b> — 반쯤 처리하면 양동이가 증발한다.
    /// </summary>
    private void ExchangeBuckets()
    {
        if (inputTanks.Count == 0 && outputTanks.Count == 0) return;
        if (maxFluidAmount <= 0) return;

        ItemDictionary dictionary = ItemDictionary.Instance;
        if (dictionary == null) return;

        bool changed = false;
        for (int i = 0; i < inventory.inputSlots.Count; i++)
        {
            ItemStack stack = inventory.inputSlots[i];
            if (stack == null || stack.item == null || stack.count <= 0 || !stack.IsPlain) continue;

            // ① 채워진 양동이 → 입력 탱크 (빈 그릇이 출력으로 나간다)
            FluidDefine filled = dictionary.GetFluidForItem(stack.item);
            if (filled != null && filled.HasBucket
                && RecipeSolver.CountFreeFluidSpace(inputTanks, filled, maxFluidAmount) >= filled.bucketAmount
                && RecipeSolver.CountFreeSpace(inventory.outputSlots, filled.emptyItem) >= 1)
            {
                RecipeSolver.AddFluid(inputTanks, filled, filled.bucketAmount, maxFluidAmount);
                RecipeSolver.AddItems(inventory.outputSlots, filled.emptyItem, 1);
                if (--stack.count <= 0) stack.Clear();
                changed = true;
                continue;
            }

            // ② 빈 그릇 → 출력 탱크에서 퍼내기 (채워진 것이 출력으로 나간다)
            FluidDefine drained = FindDrainable(stack.item);
            if (drained != null
                && RecipeSolver.CountFluid(outputTanks, drained) >= drained.bucketAmount
                && RecipeSolver.CountFreeSpace(inventory.outputSlots, drained.bucketItem) >= 1)
            {
                DrainTanks(drained, drained.bucketAmount);
                RecipeSolver.AddItems(inventory.outputSlots, drained.bucketItem, 1);
                if (--stack.count <= 0) stack.Clear();
                changed = true;
            }
        }

        if (!changed) return;
        inventory.NotifyChanged();
        PushFluids();
        Flush();
    }

    /// <summary>이 빈 그릇으로 퍼낼 수 있는 유체가 출력 탱크에 있는가.</summary>
    private FluidDefine FindDrainable(Items emptyItem)
    {
        for (int i = 0; i < outputTanks.Count; i++)
        {
            FluidStack tank = outputTanks[i];
            if (tank == null || tank.IsEmpty) continue;
            if (tank.fluid.HasBucket && tank.fluid.emptyItem == emptyItem) return tank.fluid;
        }
        return null;
    }

    private void DrainTanks(FluidDefine fluid, int amount)
    {
        for (int i = 0; i < outputTanks.Count && amount > 0; i++)
        {
            FluidStack tank = outputTanks[i];
            if (tank == null || tank.fluid != fluid || tank.amount <= 0) continue;
            int taken = Mathf.Min(tank.amount, amount);
            tank.amount -= taken;
            amount -= taken;
            if (tank.amount <= 0) tank.Clear();
        }
    }

    // ── UI 연동 (기계가 자기 UI를 직접 구동한다) ─────────────────
    /// <summary>이 기계를 표시하기 시작한 패널을 연결한다(패널이 열릴 때 호출).</summary>
    public void AttachUI(DefaultMachineUI ui)
    {
        boundUI = ui;
        PushProgress();
        PushFuel();
        PushEnergy();
        PushFluids();
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

    // ── 유체 탱크 · 에너지 접근 ─────────────────────────────────
    /// <summary>입력 탱크(범위 밖이면 null).</summary>
    public FluidStack GetInputTank(int index)
        => index >= 0 && index < inputTanks.Count ? inputTanks[index] : null;

    /// <summary>출력 탱크(범위 밖이면 null).</summary>
    public FluidStack GetOutputTank(int index)
        => index >= 0 && index < outputTanks.Count ? outputTanks[index] : null;

    /// <summary>탱크 보유량을 한 칸 최대치 기준 0~1 로 환산한다.</summary>
    public float FluidRatio(FluidStack tank)
        => tank != null && maxFluidAmount > 0 ? Mathf.Clamp01((float)tank.amount / maxFluidAmount) : 0f;

    /// <summary>에너지 보유량을 설정하고 열려 있는 UI 에 반영한다.</summary>
    public void SetEnergy(float amount)
    {
        currentEnergy = Mathf.Clamp(amount, 0f, MaxEnergyAmount);
        PushEnergy();
    }

    /// <summary>
    /// 탱크를 밖에서 채우거나 비운 뒤 부르는 통지. 파이프처럼 <see cref="InputTanks"/> 를 직접 만진 쪽이
    /// 호출해야 UI 가 갱신되고 레코드가 동기화된다(<see cref="RecipeSolver.AddItems"/> 가 통지하지 않는 것과 같다).
    /// </summary>
    public void NotifyFluidChanged()
    {
        recipeDirty = true;   // 유체가 들어오면 멈춰 있던 레시피가 다시 돌 수 있다
        PushFluids();
        Flush();
    }

    /// <summary>
    /// 열려 있는 UI 의 유체 바를 전부 갱신한다.
    /// UI 에는 색이 아니라 <b>유체 이름</b>을 넘긴다 — 색을 고르는 것은 <c>FluidColors</c> 한 곳의 몫이다.
    /// </summary>
    private void PushFluids()
    {
        if (boundUI == null) return;
        for (int i = 0; i < inputTanks.Count; i++) boundUI.SetInputFluid(i, FluidRatio(inputTanks[i]), FluidIdOf(inputTanks[i]));
        for (int i = 0; i < outputTanks.Count; i++) boundUI.SetOutputFluid(i, FluidRatio(outputTanks[i]), FluidIdOf(outputTanks[i]));
    }

    /// <summary>탱크에 담긴 유체의 id(비었으면 빈 문자열).</summary>
    private static string FluidIdOf(FluidStack tank)
        => tank != null && !tank.IsEmpty ? tank.fluid.fluidId : "";
}
