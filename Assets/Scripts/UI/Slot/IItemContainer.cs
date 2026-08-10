/// <summary>
/// 아이템 슬롯 UI가 바인딩할 수 있는 아이템 저장소 추상화.
/// 플레이어 인벤토리(<see cref="Inventory"/>)와 머신 저장소(<see cref="MachineInventory"/>)가 구현한다.
/// 슬롯은 이 인터페이스에만 의존하므로 저장소 종류와 무관하게 드래그 앤 드롭이 가능하다.
/// </summary>
public interface IItemContainer
{
    /// <summary>슬롯(칸) 개수.</summary>
    int Capacity { get; }

    /// <summary>지정 인덱스의 아이템 스택을 반환한다.</summary>
    ItemStack GetStack(int index);

    /// <summary>내용이 바뀌었음을 구독자에게 통지한다(슬롯 Refresh 트리거).</summary>
    void NotifyChanged();

    /// <summary>
    /// 이 칸에 이 아이템을 <b>최대 몇 개까지</b> 담을 수 있는가. 보통은 <c>maxStack</c> 이고
    /// 아이템 저장소만 다르다(고유 최대치가 maxStack 을 무시한다).
    ///
    /// <b>개수 상한을 아이템이 아니라 저장소에 묻는 것이 정본이다</b> —
    /// <c>item.maxStack</c> 을 직접 읽으면 1칸 저장소가 64개에서 막힌다.
    /// 실제 자르는 일은 <see cref="RecipeSolver.AddItems"/> 와 <see cref="ItemSlot.OnDrop"/> 두 곳뿐이고,
    /// 둘 다 이 값을 받아 쓴다.
    /// </summary>
    int SlotCapacity(int index, Items item);
}
