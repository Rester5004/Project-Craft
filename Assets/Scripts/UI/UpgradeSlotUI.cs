/// <summary>
/// 기계 업그레이드 칸. <see cref="UpgradeModuleItem"/> 만 받는다.
///
/// 아무거나 받게 두면 플레이어가 석탄을 꽂아 놓고 "왜 안 빨라지지" 하게 된다 —
/// <see cref="ToolPartSlotUI"/> 와 같은 방식으로 <see cref="ItemSlot.Accepts"/> 를 좁힌다.
/// (연료 칸에는 이런 필터가 없어 아무것이나 들어간다. 소비 시점의 <c>IsFuel</c> 검사가 걸러 줄 뿐이다.)
/// </summary>
public class UpgradeSlotUI : InventorySlot
{
    protected override bool Accepts(ItemStack source)
        => source != null && source.item is UpgradeModuleItem;
}
