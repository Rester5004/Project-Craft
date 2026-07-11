using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventorySlot : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Sprite selectedSlotSprite;

    private Image slotImage;
    private Inventory inventory;
    [HideInInspector] public int index;

    private static InventorySlot draggedFrom;
    private Canvas canvas;
    private Transform iconStartParent;
    private Vector3 iconStartPos;
    private Transform countStartParent;
    private Vector3 countStartPos;
    private Sprite defaultSlotSprite;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        inventory = Inventory.Instance;
        countText.transform.SetAsLastSibling();
        slotImage = GetComponent<Image>();
        defaultSlotSprite = slotImage.sprite;
    }

    private void Start()
    {
        if (inventory == null) inventory = Inventory.Instance;
    }

    public void Refresh()
    {
        ItemStack stack = inventory.slots[index];
        bool hasItem = stack.item != null && stack.count > 0;
        iconImage.enabled = hasItem;

        if (hasItem)
        {
            iconImage.sprite = stack.item.Icon;
            countText.text = stack.count > 1 ? stack.count.ToString() : "";
        }
        else
        {
            countText.text = "";
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!iconImage.enabled) return;

        draggedFrom = this;
        iconStartParent = iconImage.transform.parent;
        iconStartPos = iconImage.rectTransform.position;
        countStartParent = countText.transform.parent;
        countStartPos = countText.rectTransform.position;

        iconImage.transform.SetParent(canvas.transform);
        countText.transform.SetParent(iconImage.transform);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (draggedFrom == this)
            iconImage.rectTransform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        iconImage.transform.SetParent(iconStartParent);
        iconImage.rectTransform.position = iconStartPos;
        countText.transform.SetParent(countStartParent);
        countText.rectTransform.position = countStartPos;
        countText.transform.SetAsLastSibling();
        draggedFrom = null;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (draggedFrom == null || draggedFrom == this) return;

        ItemStack sourceStack = draggedFrom.inventory.slots[draggedFrom.index];
        ItemStack targetStack = inventory.slots[index];
        (sourceStack.item, targetStack.item) = (targetStack.item, sourceStack.item);
        (sourceStack.count, targetStack.count) = (targetStack.count, sourceStack.count);

        draggedFrom.inventory.OnChanged?.Invoke();
        inventory.OnChanged?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        slotImage.sprite = selectedSlotSprite;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        slotImage.sprite = defaultSlotSprite;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            inventory.SelectSlot(index);
    }
}
