using System.Collections.Generic;

/// <summary>
/// 머신 UI가 사용하는 아이템 저장소. 입력/출력/연료 슬롯을 분리해 보관하되,
/// UI/드래그 시스템(<see cref="ItemSlot"/>)의 평면 인덱스 계약을 위해
/// <see cref="IItemContainer"/>는 [입력...][출력...][연료...] 순서의 통합 인덱스로 노출한다.
/// 연료를 맨 뒤에 붙였으므로 기존 입력·출력 인덱스 계산은 그대로다.
/// </summary>
public class MachineInventory : IItemContainer
{
    public List<ItemStack> inputSlots;
    public List<ItemStack> outputSlots;
    public List<ItemStack> fuelSlots;
    public System.Action OnChanged;

    public MachineInventory(int inputCount, int outputCount) : this(inputCount, outputCount, 0) { }

    public MachineInventory(int inputCount, int outputCount, int fuelCount)
    {
        inputSlots = Fill(inputCount);
        outputSlots = Fill(outputCount);
        fuelSlots = Fill(fuelCount);
    }

    private static List<ItemStack> Fill(int count)
    {
        List<ItemStack> slots = new List<ItemStack>(count);
        for (int i = 0; i < count; i++) slots.Add(new ItemStack());
        return slots;
    }

    public int InputCount => inputSlots.Count;
    public int OutputCount => outputSlots.Count;
    public int FuelCount => fuelSlots.Count;

    // ── IItemContainer (통합 평면 인덱스: [입력][출력][연료]) ──
    public int Capacity => InputCount + OutputCount + FuelCount;

    public ItemStack GetStack(int index)
    {
        if (index < InputCount) return inputSlots[index];
        index -= InputCount;
        if (index < OutputCount) return outputSlots[index];
        return fuelSlots[index - OutputCount];
    }

    public void NotifyChanged() => OnChanged?.Invoke();
}
