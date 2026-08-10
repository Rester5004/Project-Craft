/// <summary>
/// 저장 블록(상자·아이템 저장소)의 칸.
///
/// 평범한 슬롯과 다른 점은 하나뿐이다 — <b>고유 최대치를 쓰는 저장소는 개체 데이터가 붙은 아이템을 받지 않는다.</b>
/// 한 칸에 수천 개가 들어가는데 <see cref="ItemInstance"/> 는 하나뿐이라, 그 하나가 닳아 없어질 때
/// <c>stack.Clear()</c> 로 전부가 통째로 사라진다(<see cref="RecipeSolver.AddItems"/> 의 칸당 1개 규칙과 같은 이유).
/// 상자는 칸이 40개고 maxStack 을 따르므로 도구도 그대로 받는다.
///
/// 개수 상한 자체는 여기서 안 본다 — <see cref="IItemContainer.SlotCapacity"/> 가 정본이고
/// <see cref="ItemSlot.OnDrop"/> 이 그걸 보고 자른다. 여기서 또 세면 두 곳이 갈라진다.
/// (<see cref="UpgradeSlotUI"/> · <see cref="ToolPartSlotUI"/> 와 같은 방식이다.)
/// </summary>
public class StorageSlotUI : InventorySlot
{
    protected override bool Accepts(ItemStack source)
    {
        if (source == null || source.item == null) return true;   // 빈 손 드롭은 베이스가 알아서 걸러 낸다
        if (source.instance == null) return true;                 // 평범한 아이템은 언제나 받는다

        // 고유 최대치를 쓰는 칸인가는 저장소에 묻는다 — 상자는 maxStack 그대로라 도구도 받는다.
        return container == null || container.SlotCapacity(index, source.item) <= RecipeSolver.MaxStackOf(source.item);
    }
}
