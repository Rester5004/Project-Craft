using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventorySlot : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
{
    public Image iconImage;      // 인스펙터에서 Icon 연결
    public TMP_Text countText;   // 인스펙터에서 Count 연결

    [HideInInspector] public Inventory inventory; // 이 슬롯이 속한 인벤토리
    [HideInInspector] public int index;           // 몇 번째 칸인지

    static InventorySlot draggedFrom;  // 지금 끌고 있는 슬롯 (모든 슬롯이 공유)
    Canvas canvas;
    Transform iconStartParent;
    Vector3 iconStartPos;

    void Awake() => canvas = GetComponentInParent<Canvas>();

    // 데이터를 화면에 반영
    public void Refresh()
    {
        var stack = inventory.slots[index];
        bool hasItem = stack.item != null && stack.count > 0;
        iconImage.enabled = hasItem;
        if (hasItem)
        {
            iconImage.sprite = stack.item.itemIcon;
            countText.text = stack.count > 1 ? stack.count.ToString() : "";
        }
        else countText.text = "";
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (!iconImage.enabled) return;      // 빈 칸은 못 끔
        draggedFrom = this;
        iconStartParent = iconImage.transform.parent;
        iconStartPos = iconImage.rectTransform.position;
        iconImage.transform.SetParent(canvas.transform); // 맨 위로 올림
    }

    public void OnDrag(PointerEventData e)
    {
        if (draggedFrom == this)
            iconImage.rectTransform.position = e.position; // 마우스 따라다님
    }

    public void OnEndDrag(PointerEventData e)
    {
        // 아이콘을 원래 자리로 되돌림 (드롭 성공/실패와 무관)
        iconImage.transform.SetParent(iconStartParent);
        iconImage.rectTransform.position = iconStartPos;
        draggedFrom = null;
    }

    public void OnDrop(PointerEventData e)
    {
        if (draggedFrom == null || draggedFrom == this) return;
        // 두 칸의 데이터를 맞바꿈
        var a = draggedFrom.inventory.slots[draggedFrom.index];
        var b = inventory.slots[index];
        (a.item, b.item) = (b.item, a.item);
        (a.count, b.count) = (b.count, a.count);
        // 양쪽 새로고침
        draggedFrom.inventory.OnChanged?.Invoke();
        inventory.OnChanged?.Invoke();
    }
    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left)
            inventory.SelectSlot(index);
    }
}
