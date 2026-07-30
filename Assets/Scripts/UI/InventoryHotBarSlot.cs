using UnityEngine.EventSystems;

/// <summary>
/// 핫바 슬롯. 드래그/드롭/표시는 <see cref="ItemSlot"/> 베이스가 담당하고,
/// 여기서는 숫자키로 선택된 상태를 시각적으로 유지하는 동작만 추가한다.
/// </summary>
public class InventoryHotBarSlot : ItemSlot
{
    private bool isSelected;

    /// <summary>숫자키로 선택된 슬롯인지 설정하고 배경 스프라이트를 갱신한다.</summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (slotImage != null)
            slotImage.sprite = isSelected ? selectedSlotSprite : defaultSlotSprite;
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        // 선택된 슬롯은 마우스가 벗어나도 하이라이트를 유지한다.
        // (base 를 부르면 하이라이트가 풀리므로 스프라이트만 직접 처리하고 툴팁은 따로 닫는다)
        if (!isSelected && slotImage != null)
            slotImage.sprite = defaultSlotSprite;

        if (TooltipUI.Instance != null) TooltipUI.Instance.Hide();
    }
}
