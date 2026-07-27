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

        inventory = new MachineInventory(inputSlotCount, outputSlotCount);
        LoadFrom(record);
    }

    /// <summary>MachineBlock 설정을 적용한다. 없거나 카운트 미설정이면 기본값으로 폴백.</summary>
    private void ApplyConfig(MachineBlock info)
    {
        if (info != null && (info.inputSlotCount > 0 || info.outputSlotCount > 0))
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
}
