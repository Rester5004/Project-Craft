using System.Collections.Generic;

/// <summary>
/// 머신 UI가 사용하는 아이템 저장소. 입력/출력/연료/업그레이드 슬롯을 분리해 보관하되,
/// UI/드래그 시스템(<see cref="ItemSlot"/>)의 평면 인덱스 계약을 위해
/// <see cref="IItemContainer"/>는 [입력...][출력...][연료...][업그레이드...] 순서의 통합 인덱스로 노출한다.
/// <b>새 구간은 언제나 맨 뒤에 붙인다</b> — 앞에 끼우면 기존 프리팹이 바인딩한 인덱스가 통째로 어긋난다.
/// </summary>
public class MachineInventory : IItemContainer
{
    public List<ItemStack> inputSlots;
    public List<ItemStack> outputSlots;
    public List<ItemStack> fuelSlots;

    /// <summary>업그레이드 모듈 칸. <b>소모되지 않고</b> 들어 있는 개수만큼 기계 성능이 바뀐다.</summary>
    public List<ItemStack> upgradeSlots;

    public System.Action OnChanged;

    /// <summary>
    /// 칸별 고유 최대치를 정하는 주인(<see cref="MachineInstance"/>)의 콜백. null 이면 아이템의 maxStack.
    ///
    /// <b>값을 복사해 두지 않고 델리게이트로 묻는다</b> — 저장소의 최대치는 업그레이드 모듈 개수에
    /// 따라 <b>런타임에 바뀌므로</b>, 여기 숫자를 캐시하면 창을 닫았다 열기 전까지 옛 값이 쓰인다
    /// (SpeedFactor·EfficiencyFactor 를 캐시하지 않는 것과 같은 규약).
    /// </summary>
    public System.Func<int, Items, int> capacityOverride;

    /// <summary>
    /// "이 칸이 이 아이템을 받는가" 를 정하는 주인의 콜백. null 이면 무엇이든 받는다.
    /// <see cref="capacityOverride"/> 와 같은 이유로 값이 아니라 함수다.
    /// </summary>
    public System.Func<int, Items, bool> acceptOverride;

    public MachineInventory(int inputCount, int outputCount) : this(inputCount, outputCount, 0, 0) { }

    public MachineInventory(int inputCount, int outputCount, int fuelCount) : this(inputCount, outputCount, fuelCount, 0) { }

    public MachineInventory(int inputCount, int outputCount, int fuelCount, int upgradeCount)
    {
        inputSlots = Fill(inputCount);
        outputSlots = Fill(outputCount);
        fuelSlots = Fill(fuelCount);
        upgradeSlots = Fill(upgradeCount);
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
    public int UpgradeCount => upgradeSlots.Count;

    // ── IItemContainer (통합 평면 인덱스: [입력][출력][연료][업그레이드]) ──
    public int Capacity => InputCount + OutputCount + FuelCount + UpgradeCount;

    public ItemStack GetStack(int index)
    {
        if (index < InputCount) return inputSlots[index];
        index -= InputCount;
        if (index < OutputCount) return outputSlots[index];
        index -= OutputCount;
        if (index < FuelCount) return fuelSlots[index];
        return upgradeSlots[index - FuelCount];
    }

    public void NotifyChanged() => OnChanged?.Invoke();

    public int SlotCapacity(int index, Items item)
        => capacityOverride != null ? capacityOverride(index, item) : RecipeSolver.MaxStackOf(item);

    public bool AcceptsItem(int index, Items item)
        => acceptOverride == null || acceptOverride(index, item);
}
