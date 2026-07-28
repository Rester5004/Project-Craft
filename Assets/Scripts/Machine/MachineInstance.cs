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
        inventory = new MachineInventory(inputSlotCount, outputSlotCount);
        inventory.OnChanged += HandleInventoryChanged;

        LoadFrom(record);

        activeRecipe = null;
        progress = 0f;
        recipeDirty = true;
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
        LoadSlots(inventory.inputSlots, record.inputItemNames, record.inputCounts);
        LoadSlots(inventory.outputSlots, record.outputItemNames, record.outputCounts);
    }

    private static void LoadSlots(System.Collections.Generic.List<ItemStack> slots, string[] names, int[] counts)
    {
        int cap = names != null ? names.Length : 0;
        for (int i = 0; i < slots.Count; i++)
        {
            ItemStack stack = slots[i];
            if (i < cap && !string.IsNullOrEmpty(names[i]) && counts[i] > 0)
            {
                stack.item = ItemDictionary.Instance != null ? ItemDictionary.Instance.GetItem(names[i]) : null;
                stack.count = stack.item != null ? counts[i] : 0;
            }
            else
            {
                stack.item = null;
                stack.count = 0;
            }
        }
    }

    /// <summary>런타임 인벤토리 → 레코드로 직렬화(저장/디스폰 전 호출).</summary>
    public void WriteBack(PlaceableRecord record)
    {
        record.blockId = blockId;
        WriteSlots(inventory.inputSlots, ref record.inputItemNames, ref record.inputCounts);
        WriteSlots(inventory.outputSlots, ref record.outputItemNames, ref record.outputCounts);
    }

    private static void WriteSlots(System.Collections.Generic.List<ItemStack> slots, ref string[] names, ref int[] counts)
    {
        int cap = slots.Count;
        if (names == null || names.Length != cap)
        {
            names = new string[cap];
            counts = new int[cap];
        }
        for (int i = 0; i < cap; i++)
        {
            ItemStack stack = slots[i];
            bool has = stack.item != null && stack.count > 0;
            names[i] = has ? stack.item.itemName : "";
            counts[i] = has ? stack.count : 0;
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

        // 진행 중에 재료를 빼가면 취소하고 처음부터
        if (!RecipeSolver.HasInputs(inventory.inputSlots, activeRecipe))
        {
            activeRecipe = null;
            progress = 0f;
            recipeDirty = true;
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
        RecipeSolver.StoreOutputs(inventory.outputSlots, activeRecipe);

        activeRecipe = null;
        progress = 0f;
        recipeDirty = true;

        Flush();                     // 레코드 동기화(중간 저장/디스폰 대비)
        inventory.NotifyChanged();   // 슬롯 뷰 갱신 + recipeDirty 재설정
        PushProgress();
    }

    /// <summary>입력 슬롯으로 지금 만들 수 있는 첫 레시피를 고른다.</summary>
    private Recipe SelectRecipe()
    {
        RecipeDictionary dictionary = RecipeDictionary.Instance;
        if (dictionary == null) return null;

        System.Collections.Generic.IReadOnlyList<Recipe> candidates = dictionary.GetRecipesFor(blockId);
        for (int i = 0; i < candidates.Count; i++)
        {
            Recipe recipe = candidates[i];
            if (recipe != null && RecipeSolver.HasInputs(inventory.inputSlots, recipe))
                return recipe;
        }
        return null;
    }

    // ── UI 연동 (기계가 자기 UI를 직접 구동한다) ─────────────────
    /// <summary>이 기계를 표시하기 시작한 패널을 연결한다(패널이 열릴 때 호출).</summary>
    public void AttachUI(DefaultMachineUI ui)
    {
        boundUI = ui;
        PushProgress();
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
}
