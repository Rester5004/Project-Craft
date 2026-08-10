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

    /// <summary>
    /// 이번 드래그로 옮기려는 개수. <b>좌클릭 = 전량 · 우클릭 = 올림 절반.</b>
    ///
    /// 데이터는 <see cref="OnDrop"/> 에서만 움직이므로, 절반 집기는 "지금 실제로 쪼개기" 가 아니라
    /// <b>요청량을 기억해 두는 것</b>으로 표현한다. 덕분에 "우클릭 절반" 과
    /// "1칸 저장소에서 maxStack 만큼만 꺼내기" 가 같은 분기 하나로 처리된다.
    ///
    /// ⚠ <b><see cref="draggedFrom"/> 보다 오래 살아남으면 안 된다.</b> 남겨 두면
    /// 다음 드래그가 옛 개수를 물고 간다 — 지우는 자리는 넷이다
    /// (OnBeginDrag 의 조기 return · OnDrop 의 끝 · OnEndDrag · OnDisable).
    /// </summary>
    private static int draggedAmount;
    // 이 슬롯이 실제로 드래그를 시작했는가. static draggedFrom 만으로는 판정할 수 없다 —
    // 유니티는 OnBeginDrag 에서 조기 return 한 슬롯에도 OnEndDrag 를 보내기 때문.
    private bool dragging;

    // ── 드래그 고스트 ──────────────────────────────────────────
    // 커서를 따라가는 그림은 <b>슬롯의 아이콘이 아니라 별도의 오브젝트</b>다.
    // 예전에는 iconImage·countText 를 캔버스로 옮겨 썼는데, 그러면 드래그 도중 슬롯이 통째로 비어 보여
    // <b>우클릭으로 절반만 집었을 때 남은 절반이 안 보였다.</b> 이제 슬롯은 "남는 만큼" 을 계속 그린다.
    // 캔버스마다 만들지 않고 하나를 옮겨 쓴다(드래그는 한 번에 하나뿐이다).
    //
    // 아이콘과 숫자는 <b>고스트 루트의 자식</b>이고, 둘의 위치는 드래그를 시작한 슬롯에서 매번 베낀다 —
    // 숫자 위치가 프리팹마다 다르기 때문이다(`slot` 은 아래 가운데, `MachineSlot` 은 오른쪽 아래).
    // 좌표를 코드에 박으면 어느 한쪽 슬롯에서 반드시 어긋난다.
    private static RectTransform ghostRoot;
    private static Image ghostIcon;
    private static TMP_Text ghostCount;

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

    /// <summary>
    /// 바인딩된 저장소의 데이터를 화면에 반영한다.
    ///
    /// <b>드래그로 들어 올린 만큼은 빼고 그린다</b> — 그 몫은 커서(고스트)가 들고 있기 때문이다.
    /// 우클릭으로 절반을 집으면 슬롯에는 나머지 절반이 그대로 보이고, 전량을 집으면 빈 칸으로 보인다.
    /// </summary>
    public void Refresh()
    {
        if (container == null) return;
        ItemStack stack = container.GetStack(index);

        int shown = stack != null ? stack.count : 0;
        if (dragging && draggedFrom == this) shown -= draggedAmount;
        bool hasItem = stack != null && stack.item != null && shown > 0;

        // 남는 것이 없으면 스택이 아니라 null 을 넘겨야 한다 — Apply 는 stack.count 를 보므로
        // 스택을 그대로 넘기면 들어 올린 몫까지 계속 그려진다.
        ItemIconView.Apply(iconImage, hasItem ? stack : null);   // 도구는 자루 + 머리를 겹쳐 그린다
        if (countText != null)
        {
            countText.text = hasItem && shown > 1 ? shown.ToString() : "";
            countText.transform.SetAsLastSibling();   // 겹침 레이어보다 위에 유지
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 빈 칸(또는 배선이 덜 된 슬롯)에서 시작한 드래그는 <b>draggedFrom 을 반드시 지운다.</b>
        // 예전에는 그냥 return 해서, 직전 드래그 도중 창이 닫혀 살아남은 draggedFrom 이 그대로 남았고
        // 다음 드롭이 손대지도 않은 슬롯의 아이템을 옮겼다.
        if (iconImage == null || countText == null || canvas == null || !iconImage.enabled)
        {
            draggedFrom = null;
            draggedAmount = 0;
            return;
        }

        int amount = AmountFor(eventData);
        if (amount <= 0) { draggedFrom = null; draggedAmount = 0; return; }

        dragging = true;
        draggedFrom = this;
        draggedAmount = amount;

        ShowGhost(container.GetStack(index), eventData.position);
        Refresh();   // 슬롯에는 들어 올리고 남은 만큼만 남는다
    }

    /// <summary>
    /// 이번 드래그로 집을 개수. 우클릭이면 <b>올림 절반</b>(1개짜리는 1개 그대로).
    ///
    /// 개체 데이터가 붙은 스택은 쪼개지 않는다 — 인스턴스가 하나뿐이라 나누면
    /// 한쪽이 내구도를 공유하거나 통째로 사라진다(<see cref="RecipeSolver.AddItems"/> 의 칸당 1개 규칙과 같은 이유).
    /// </summary>
    private int AmountFor(PointerEventData eventData)
    {
        ItemStack stack = container != null ? container.GetStack(index) : null;
        if (stack == null || stack.item == null || stack.count <= 0) return 0;

        bool half = eventData.button == PointerEventData.InputButton.Right && stack.instance == null;
        return half ? Mathf.Max(1, (stack.count + 1) / 2) : stack.count;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 캔버스가 Screen Space - Overlay 라 화면 좌표가 곧 월드 좌표다.
        // 다른 렌더 모드로 바꾸면 여기서 좌표 변환이 필요해진다(TooltipUI.FollowCursor 참고).
        if (dragging && draggedFrom == this && ghostRoot != null)
            ghostRoot.position = eventData.position;
    }

    /// <summary>
    /// 커서를 따라갈 그림을 띄운다. 슬롯의 <see cref="iconImage"/> 는 <b>건드리지 않는다</b> —
    /// 옮겨 쓰면 드래그 도중 슬롯이 비어 보여 남은 절반을 확인할 수 없다.
    ///
    /// 최상위 캔버스의 맨 뒤에 붙여 다른 패널에 가리지 않게 하고,
    /// <c>raycastTarget</c> 을 꺼서 <b>커서 아래 슬롯이 드롭을 받게</b> 한다(켜 두면 고스트가 가로챈다).
    /// </summary>
    private void ShowGhost(ItemStack stack, Vector2 screenPosition)
    {
        if (stack == null || stack.item == null) return;
        Canvas root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;

        if (ghostRoot == null)
        {
            GameObject go = new GameObject("ItemDragGhost", typeof(RectTransform));
            ghostRoot = (RectTransform)go.transform;

            GameObject iconGO = new GameObject("icon", typeof(RectTransform), typeof(Image));
            ghostIcon = iconGO.GetComponent<Image>();
            ghostIcon.raycastTarget = false;
            ghostIcon.rectTransform.SetParent(ghostRoot, false);

            GameObject label = new GameObject("count", typeof(RectTransform));
            ghostCount = label.AddComponent<TextMeshProUGUI>();
            ghostCount.raycastTarget = false;
            ghostCount.rectTransform.SetParent(ghostRoot, false);
        }

        ghostRoot.SetParent(root.transform, false);
        ghostRoot.SetAsLastSibling();
        ghostRoot.localScale = Vector3.one;
        ghostRoot.anchorMin = ghostRoot.anchorMax = ghostRoot.pivot = new Vector2(0.5f, 0.5f);
        ghostRoot.sizeDelta = ((RectTransform)transform).rect.size;
        ghostRoot.gameObject.SetActive(true);
        ghostRoot.position = screenPosition;

        // 아이콘·숫자의 자리는 <b>이 슬롯에서 그대로 베낀다</b>. 프리팹마다 숫자 위치가 달라서
        // (슬롯 아래 가운데 / 오른쪽 아래) 좌표를 코드에 박으면 어느 한쪽에서 반드시 어긋난다.
        CopyPlacement(ghostIcon.rectTransform, iconImage.rectTransform, root);
        CopyPlacement(ghostCount.rectTransform, countText.rectTransform, root);

        // 글꼴·색도 슬롯의 숫자에서 베낀다 — 여기서 따로 정하면 한글 폰트를 갈 때 여기만 남는다.
        ghostCount.font = countText.font;
        ghostCount.fontSharedMaterial = countText.fontSharedMaterial;
        ghostCount.fontSize = countText.fontSize;
        ghostCount.color = countText.color;
        ghostCount.alignment = countText.alignment;

        ghostIcon.preserveAspect = iconImage.preserveAspect;
        ItemIconView.Apply(ghostIcon, stack.item, stack.instance);   // 도구는 겹쳐 그린다
        ghostCount.text = draggedAmount > 1 ? draggedAmount.ToString() : "";
        ghostCount.rectTransform.SetAsLastSibling();   // Apply 가 만든 겹침 레이어보다 위에

        // 드래그 중에는 툴팁이 커서를 따라와 고스트와 겹친다.
        if (TooltipUI.Instance != null) TooltipUI.Instance.Hide();
    }

    /// <summary>
    /// 원본이 <b>슬롯 안에서 놓여 있던 자리</b>를 고스트 안의 같은 자리로 옮긴다.
    ///
    /// 원본의 앵커·부모 계층이 프리팹마다 달라서 <c>anchoredPosition</c> 을 그대로 베끼면 어긋난다 —
    /// 대신 <b>월드 좌표 차이</b>로 잰다. 캔버스 스케일러가 배율을 걸어도 맞도록 루트 캔버스 배율로 나눈다
    /// (고스트 루트는 그 캔버스 바로 밑이라 로컬 단위가 곧 화면 단위 ÷ 배율이다).
    /// </summary>
    private void CopyPlacement(RectTransform ghost, RectTransform source, Canvas root)
    {
        float scale = root.transform.lossyScale.x;
        if (Mathf.Approximately(scale, 0f)) scale = 1f;

        ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
        ghost.pivot = source.pivot;                       // position 은 피벗의 위치라 함께 맞춰야 한다
        ghost.localScale = Vector3.one;
        ghost.sizeDelta = source.rect.size;
        ghost.anchoredPosition = (Vector2)(source.position - transform.position) / scale;
    }

    /// <summary>
    /// 고스트를 감추고 "들고 있는 개수" 를 0 으로 돌린다.
    /// <b>드롭과 드래그 종료 양쪽에서 불린다</b> — 드롭 뒤에도 남겨 두면 그 슬롯이
    /// 이미 옮겨 간 몫을 한 번 더 빼서 잠깐 빈 칸으로 보인다. 여러 번 불러도 안전하다.
    /// </summary>
    private static void ClearDragCarry()
    {
        draggedAmount = 0;
        if (ghostRoot != null) ghostRoot.gameObject.SetActive(false);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging) return;
        dragging = false;
        if (draggedFrom == this) { draggedFrom = null; ClearDragCarry(); }
        Refresh();   // 들고 있던 몫을 슬롯에 되돌려 그린다(드롭이 없었으면 원래대로)
    }

    /// <summary>
    /// 드래그 중 창이 닫히면 유니티는 <c>OnEndDrag</c> 를 배달하지 않는다.
    /// 그대로 두면 static <c>draggedFrom</c> 이 살아남아 <b>다음 드롭이 손대지도 않은 슬롯의 아이템을 옮긴다</b>
    /// (고스트도 화면에 뜬 채로 남는다).
    ///
    /// 예전에는 여기서 아이콘을 슬롯 밖(캔버스)에서 되돌려야 했는데, 비활성화 중의 <c>SetParent</c> 를
    /// 유니티가 거부해 <c>OnEnable</c> 까지 미뤄야 했다. 이제 <b>슬롯의 아이콘은 애초에 움직이지 않으므로</b>
    /// 여기서 끝난다 — 그 복구 경로(pendingRestore·RestoreDragVisuals)는 통째로 사라졌다.
    /// </summary>
    protected virtual void OnDisable()
    {
        if (!dragging) return;
        dragging = false;
        if (draggedFrom == this) { draggedFrom = null; ClearDragCarry(); }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (draggedFrom == null || draggedFrom == this) return;
        if (draggedFrom.container == null || container == null) return;
        if (!insertable) return; // 이 슬롯은 드롭을 받지 않음(예: 기계 출력 슬롯)

        ItemStack source = draggedFrom.container.GetStack(draggedFrom.index);
        ItemStack target = container.GetStack(index);
        if (!Accepts(source)) return;   // 종류 제한이 있는 슬롯(도구 부품 칸 등)
        if (source.item == null || source.count <= 0) return;

        // 옮기려는 개수. 드래그 시작 때 정해진다(좌클릭 전량 / 우클릭 올림 절반).
        // 그 사이 슬롯 내용이 줄었을 수 있으므로 지금 개수로 다시 자른다.
        int want = Mathf.Clamp(draggedAmount, 0, source.count);
        if (want <= 0) return;

        // <b>상한은 아이템이 아니라 이 저장소에 묻는다</b> — item.maxStack 을 직접 읽으면
        // 1칸짜리 아이템 저장소가 64개에서 막힌다(IItemContainer.SlotCapacity 가 정본).
        int max = container.SlotCapacity(index, source.item);
        bool whole = want >= source.count;   // 전량을 드는가(= 교환이 성립하는가)
        if (target.CanStackWith(source))
        {
            // 같은 아이템(개체 데이터까지 동일): 교환 대신 합쳐서 개수를 늘린다(칸 한도까지).
            int space = max - target.count;
            if (space <= 0) return;   // 대상이 가득 찼다 — 아무것도 이동하지 않는다

            int moved = Mathf.Min(space, want);
            target.count += moved;
            source.count -= moved;
            if (source.count <= 0) source.Clear();
        }
        else if (target.item == null && (!whole || source.count > max))
        {
            // 빈 칸인데 <b>일부만</b> 옮긴다(우클릭 절반이거나 대상 한도가 모자라다).
            // 교환이 아니라 나눠 담는다 — 개체 데이터는 쪼갤 수 없으므로 여기 오지 않는다(AmountFor 가 막는다).
            int moved = Mathf.Min(want, max);
            target.item = source.item;
            target.count = moved;
            target.instance = null;
            source.count -= moved;
            if (source.count <= 0) source.Clear();
        }
        else if (!whole)
        {
            // 다른 아이템이 든 칸에 일부만 놓으려는 것 — 교환은 뜻이 성립하지 않는다(든 것보다 많이 돌아온다).
            return;
        }
        else
        {
            // 전량 교환. 단 <b>원래 칸이 상대 스택을 못 담으면</b> 개수가 잘려 아이템이 사라지므로 거부한다
            // (저장소 ↔ 인벤토리처럼 두 칸의 한도가 다를 때 실제로 걸린다).
            if (target.item != null &&
                target.count > draggedFrom.container.SlotCapacity(draggedFrom.index, target.item)) return;

            (source.item, target.item) = (target.item, source.item);
            (source.count, target.count) = (target.count, source.count);
            (source.instance, target.instance) = (target.instance, source.instance);
        }
        // 옮겼으므로 더는 들고 있지 않다. <b>Refresh 보다 먼저</b> 지워야 출발 슬롯이
        // 이미 빠져나간 몫을 한 번 더 빼서 잠깐 빈 칸으로 보이지 않는다.
        ClearDragCarry();

        // 두 슬롯 뷰를 즉시 갱신(컨테이너 종류/구독 여부와 무관하게 확실히 반영)
        draggedFrom.Refresh();
        Refresh();

        // 같은 컨테이너를 보는 다른 뷰(인벤토리 전체 등)를 위해 변경 통지도 유지
        draggedFrom.container.NotifyChanged();
        container.NotifyChanged();
    }

    // 호버할 때마다 델리게이트를 새로 만들지 않도록 한 번만 만들어 둔다.
    private System.Func<string> tooltipProvider;

    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        if (slotImage != null) slotImage.sprite = selectedSlotSprite;
        if (TooltipUI.Instance == null) return;

        // 기계가 가공을 끝내면 커서를 올려 둔 채로도 슬롯 내용이 바뀌므로 매 프레임 다시 읽는다.
        if (tooltipProvider == null) tooltipProvider = TooltipText;
        TooltipUI.Instance.Show(tooltipProvider);
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

    /// <summary>
    /// 고유 최대치를 쓰는 칸(아이템 저장소)에만 <c>현재 / 최대</c> 를 덧붙인다.
    /// 평범한 칸은 maxStack 이 뻔하므로 붙이지 않는다 — 모든 툴팁에 숫자가 늘어나면 오히려 안 읽힌다.
    /// </summary>
    private string CapacityLine(ItemStack stack)
    {
        int max = container.SlotCapacity(index, stack.item);
        if (max == RecipeSolver.MaxStackOf(stack.item)) return "";
        return "\n" + stack.count.ToString("N0") + " / " + max.ToString("N0");
    }

    /// <summary>호버 시 보여줄 내용. 빈 슬롯이면 빈 문자열이라 툴팁이 뜨지 않는다.</summary>
    protected virtual string TooltipText()
    {
        if (container == null) return "";

        ItemStack stack = container.GetStack(index);
        if (stack == null || stack.item == null || stack.count <= 0) return "";
        if (stack.instance == null) return stack.item.DisplayName + CapacityLine(stack);

        string extra = stack.instance.TooltipExtra();
        string name = stack.instance.DecorateName(stack.item);
        return string.IsNullOrEmpty(extra) ? name : name + "\n" + extra;
    }
}
