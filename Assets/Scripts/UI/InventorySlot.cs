using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 플레이어 인벤토리/머신 등에서 쓰는 표준 아이템 슬롯.
/// 드래그/드롭/표시는 <see cref="ItemSlot"/> 베이스가 담당하고,
/// 여기서는 좌클릭으로 플레이어 인벤토리 슬롯을 "선택"하는 동작만 추가한다.
/// </summary>
public class InventorySlot : ItemSlot, IPointerClickHandler
{
    // "지금 든 것" 의 정본은 핫바다. 클릭도 그쪽으로 보내야 하이라이트와 실제 선택이 어긋나지 않는다.
    private InventoryHotBarUI hotbar;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        // 플레이어 인벤토리 슬롯일 때만 선택 처리(머신 슬롯이 선택을 가로채지 않도록).
        if (!(container is Inventory)) return;

        if (hotbar == null) hotbar = FindFirstObjectByType<InventoryHotBarUI>();
        // 핫바 범위(30~39) 밖의 칸을 눌렀으면 안에서 그냥 무시한다 — 손에 들 수 없는 칸이기 때문.
        if (hotbar != null) hotbar.SelectByInventoryIndex(index);
    }
}
