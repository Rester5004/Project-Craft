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
}
