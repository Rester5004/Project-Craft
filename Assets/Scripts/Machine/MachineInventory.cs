using System.Collections.Generic;

/// <summary>
/// 머신 UI가 사용하는 아이템 저장소. 입력/출력 슬롯을 분리해 보관하되,
/// UI/드래그 시스템(<see cref="ItemSlot"/>)의 평면 인덱스 계약을 위해
/// <see cref="IItemContainer"/>는 [입력...][출력...] 순서의 통합 인덱스로 노출한다.
/// </summary>
public class MachineInventory : IItemContainer
{
    public List<ItemStack> inputSlots;
    public List<ItemStack> outputSlots;
    public System.Action OnChanged;

    public MachineInventory(int inputCount, int outputCount)
    {
        inputSlots = new List<ItemStack>(inputCount);
        for (int i = 0; i < inputCount; i++) inputSlots.Add(new ItemStack());

        outputSlots = new List<ItemStack>(outputCount);
        for (int i = 0; i < outputCount; i++) outputSlots.Add(new ItemStack());
    }

    public int InputCount => inputSlots.Count;
    public int OutputCount => outputSlots.Count;

    // ── IItemContainer (통합 평면 인덱스: 0..InputCount-1 = 입력, 그 뒤 = 출력) ──
    public int Capacity => InputCount + OutputCount;

    public ItemStack GetStack(int index)
        => index < InputCount ? inputSlots[index] : outputSlots[index - InputCount];

    public void NotifyChanged() => OnChanged?.Invoke();
}
