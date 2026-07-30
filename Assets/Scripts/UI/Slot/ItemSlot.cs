using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 아이템 슬롯 공통 베이스. 임의의 <see cref="IItemContainer"/> + index 에 바인딩되어
/// 드래그/드롭으로 슬롯 간 아이템을 교환한다.
/// 비제네릭 베이스의 <c>static draggedFrom</c> 을 모든 파생 슬롯(인벤토리/핫바/머신)이
/// 단일 저장소로 공유하므로 저장소 종류가 달라도 교차 드롭이 성립한다.
/// </summary>
public abstract class ItemSlot : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] protected Image iconImage;
    [SerializeField] protected TMP_Text countText;
    [SerializeField] protected Sprite selectedSlotSprite;

    protected Image slotImage;
    protected Sprite defaultSlotSprite;
    protected Canvas canvas;

    [HideInInspector] public int index;
    protected IItemContainer container;

    // 드롭 수용 여부. 기계 출력 슬롯 등은 false 로 설정해 플레이어가 아이템을 넣지 못하게 한다.
    // (드래그 아웃/프로그램적 기록에는 영향 없음)
    protected bool insertable = true;

    private static ItemSlot draggedFrom;
    private Transform iconStartParent;
    private Vector3 iconStartPos;
    private Transform countStartParent;
    private Vector3 countStartPos;

    protected virtual void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (slotImage == null) slotImage = GetComponent<Image>();
        if (slotImage != null) defaultSlotSprite = slotImage.sprite;
        if (countText != null) countText.transform.SetAsLastSibling();
        // 명시적 Bind 이전 기본값. Bind 가 호출되면 덮어써진다.
        if (container == null) container = Inventory.Instance;
    }

    /// <summary>이 슬롯이 표시/조작할 저장소와 인덱스를 지정하고 즉시 갱신한다.</summary>
    public void Bind(IItemContainer container, int index)
    {
        this.container = container;
        this.index = index;
        Refresh();
    }

    /// <summary>플레이어 드롭 수용 여부를 설정한다(예: 기계 출력 슬롯은 false).</summary>
    public void SetInsertable(bool value) => insertable = value;

    /// <summary>바인딩된 저장소의 데이터를 화면에 반영한다.</summary>
    public void Refresh()
    {
        if (container == null) return;
        ItemStack stack = container.GetStack(index);
        bool hasItem = stack != null && stack.item != null && stack.count > 0;

        ItemIconView.Apply(iconImage, stack);   // 도구는 자루 + 머리를 겹쳐 그린다
        if (countText != null)
        {
            countText.text = hasItem && stack.count > 1 ? stack.count.ToString() : "";
            countText.transform.SetAsLastSibling();   // 겹침 레이어보다 위에 유지
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
        if (draggedFrom.container == null || container == null) return;
        if (!insertable) return; // 이 슬롯은 드롭을 받지 않음(예: 기계 출력 슬롯)

        ItemStack source = draggedFrom.container.GetStack(draggedFrom.index);
        ItemStack target = container.GetStack(index);
        if (!Accepts(source)) return;   // 종류 제한이 있는 슬롯(도구 부품 칸 등)

        if (source.item != null && target.CanStackWith(source))
        {
            // 같은 아이템(개체 데이터까지 동일): 교환 대신 합쳐서 개수를 늘린다(maxStack 한도까지).
            int max = source.item.maxStack > 0 ? source.item.maxStack : int.MaxValue;
            int space = max - target.count;
            if (space > 0)
            {
                int moved = Mathf.Min(space, source.count);
                target.count += moved;
                source.count -= moved;
                if (source.count <= 0) source.Clear();
            }
            // space <= 0 이면 대상이 가득 차 있으므로 아무것도 이동하지 않는다.
        }
        else
        {
            // 다른 아이템(또는 빈 칸): 두 슬롯을 교환한다. 개체 데이터도 아이템을 따라간다.
            (source.item, target.item) = (target.item, source.item);
            (source.count, target.count) = (target.count, source.count);
            (source.instance, target.instance) = (target.instance, source.instance);
        }

        // 두 슬롯 뷰를 즉시 갱신(컨테이너 종류/구독 여부와 무관하게 확실히 반영)
        draggedFrom.Refresh();
        Refresh();

        // 같은 컨테이너를 보는 다른 뷰(인벤토리 전체 등)를 위해 변경 통지도 유지
        draggedFrom.container.NotifyChanged();
        container.NotifyChanged();
    }

    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        if (slotImage != null) slotImage.sprite = selectedSlotSprite;
        if (TooltipUI.Instance != null) TooltipUI.Instance.Show(TooltipText());
    }

    public virtual void OnPointerExit(PointerEventData eventData)
    {
        if (slotImage != null) slotImage.sprite = defaultSlotSprite;
        if (TooltipUI.Instance != null) TooltipUI.Instance.Hide();
    }

    /// <summary>
    /// 이 슬롯이 받아 줄 수 있는 아이템인가. 기본은 무엇이든 허용.
    /// 종류를 제한하는 슬롯(도구 부품 칸 등)이 재정의한다.
    /// </summary>
    protected virtual bool Accepts(ItemStack source) => true;

    /// <summary>호버 시 보여줄 내용. 빈 슬롯이면 빈 문자열이라 툴팁이 뜨지 않는다.</summary>
    protected virtual string TooltipText()
    {
        if (container == null) return "";

        ItemStack stack = container.GetStack(index);
        if (stack == null || stack.item == null || stack.count <= 0) return "";
        if (stack.instance == null) return stack.item.DisplayName;

        string extra = stack.instance.TooltipExtra();
        string name = stack.instance.DecorateName(stack.item);
        return string.IsNullOrEmpty(extra) ? name : name + "\n" + extra;
    }
}
