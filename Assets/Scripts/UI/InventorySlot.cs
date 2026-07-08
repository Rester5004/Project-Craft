using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventorySlot : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;      // 인스펙터에서 Icon 연결
    [SerializeField] private TMP_Text countText;   // 인스펙터에서 Count 연결
    private Image slotImage;      // 슬롯 배경 Image (인스펙터에서 연결, 비어있으면 자동으로 GetComponent)
    [SerializeField] private Sprite selectedSlotSprite;
    private Inventory inventory; // 이 슬롯이 속한 인벤토리
    [HideInInspector] public int index;           // 몇 번째 칸인지

    static InventorySlot draggedFrom;  // 지금 끌고 있는 슬롯 (모든 슬롯이 공유)
    Canvas canvas;
    Transform iconStartParent;
    Vector3 iconStartPos;
    Transform countStartParent;
    Vector3 countStartPos;
    Sprite defaultSlotSprite;

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        inventory = Inventory.Instance;
        countText.transform.SetAsLastSibling(); // countText가 iconImage보다 항상 위에 보이도록
        if (slotImage == null) slotImage = GetComponent<Image>();
        defaultSlotSprite = slotImage.sprite;
    }

    void Start()
    {
        if(inventory == null)
            inventory = Inventory.Instance;
    }
    // 데이터를 화면에 반영
    public void Refresh()
    {
        var stack = inventory.slots[index];
        bool hasItem = stack.item != null && stack.count > 0;
        iconImage.enabled = hasItem;
        if (hasItem)
        {
            iconImage.sprite = stack.item.Icon;
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
        countStartParent = countText.transform.parent;
        countStartPos = countText.rectTransform.position;

        iconImage.transform.SetParent(canvas.transform); // 맨 위로 올림
        countText.transform.SetParent(iconImage.transform); // icon과 함께 움직이도록 자식으로 붙임 (위치는 그대로 유지됨)
    }

    public void OnDrag(PointerEventData e)
    {
        if (draggedFrom == this)
            iconImage.rectTransform.position = e.position; // 마우스 따라다님 (countText는 자식이라 함께 이동)
    }

    public void OnEndDrag(PointerEventData e)
    {
        // 아이콘을 원래 자리로 되돌림 (드롭 성공/실패와 무관)
        iconImage.transform.SetParent(iconStartParent);
        iconImage.rectTransform.position = iconStartPos;
        countText.transform.SetParent(countStartParent);
        countText.rectTransform.position = countStartPos;
        countText.transform.SetAsLastSibling(); // countText가 iconImage보다 항상 위에 보이도록
        draggedFrom = null;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        slotImage.sprite = selectedSlotSprite;
    }

    public void OnPointerExit(PointerEventData e)
    {
        slotImage.sprite = defaultSlotSprite;
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
}