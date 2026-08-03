using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// P 로 여는 아이템 목록. 딕셔너리에 등록된 아이템을 화면 오른쪽에 전부 늘어놓고,
/// 클릭하면 인벤토리로 가져온다.
///
/// 지급은 <b><see cref="CommandConsole.Execute"/> 에 <c>/give</c> 를 넘겨서</b> 한다 —
/// 인벤토리에 넣는 규칙(스택 합치기·가득 찼을 때 처리·결과 메시지)이 두 벌이 되면 반드시 어긋난다.
///
/// UI 는 런타임에 캔버스 아래로 직접 만든다(<see cref="CommandConsole"/> 와 같은 규약).
/// 목록은 <b>처음 열 때</b> 만든다 — 200개가 넘어 시작할 때마다 짓기엔 아깝다.
/// </summary>
public class ItemBrowser : MonoBehaviour
{
    private const string UIName = "ItemBrowser";

    private const float PanelWidth = 380f;
    private const float RowHeight = 44f;
    private const float IconSize = 34f;

    [Tooltip("비우면 씬에서 첫 번째 Canvas 를 사용")]
    [SerializeField] private Canvas targetCanvas;

    private GameObject panel;
    private RectTransform content;
    private TMP_Text statusText;
    private TMP_FontAsset font;
    private bool isOpen;
    private bool built;

    private CommandConsole console;

    private void Start()
    {
        BuildUI();
        if (UIManager.Instance != null) UIManager.Instance.AddUI(panel, UIName);
        panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (InputActionManager.Instance != null)
            InputActionManager.Instance.OnItemBrowserPerformed += Toggle;
    }

    private void OnDisable()
    {
        InputActionManager input = InputActionManager.InstanceIfAlive;   // 종료 중엔 Instance 가 null 이다
        if (input != null) input.OnItemBrowserPerformed -= Toggle;
    }

    private void Update()
    {
        if (!isOpen) return;

        // 패널이 다른 경로로 꺼졌는데 isOpen 이 남아 있으면 P 가 먹통이 된다.
        if (panel != null && !panel.activeInHierarchy) { isOpen = false; return; }

        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
    }

    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (isOpen || panel == null) return;

        // 매번 등록을 보장한다. 등록이 안 돼 있으면 OpenUI 가 조용히 실패하는데,
        // 그 상태로 isOpen 을 세우면 P 가 영구히 안 먹는다(CommandConsole 과 같은 이유).
        if (UIManager.Instance != null)
        {
            UIManager.Instance.AddUI(panel, UIName);
            UIManager.Instance.OpenUI(UIName);
        }
        panel.SetActive(true);

        isOpen = panel.activeInHierarchy;
        if (!isOpen) { Debug.LogError("[ItemBrowser] 패널을 활성화하지 못했습니다.", panel); return; }

        // 켠 다음에 짓는다(처음 한 번만). 꺼져 있는 동안에는 레이아웃 재계산이 통째로 무시돼
        // 줄 높이가 0 인 채로 남는다.
        BuildRows();

        // 플레이어 입력은 끄지 않는다 — 끄면 P 로 다시 닫을 수 없다.
        // 목록 위 클릭은 PlayerInteraction 이 IsPointerOverGameObject 로 이미 걸러 낸다.
    }

    public void Close()
    {
        isOpen = false;
        if (UIManager.Instance != null) UIManager.Instance.CloseUI(UIName);
        if (panel != null && panel.activeSelf) panel.SetActive(false);
    }

    /// <summary>줄을 클릭했을 때. <paramref name="wholeStack"/> 이면 한 스택을 준다.</summary>
    public void Give(Items item, bool wholeStack)
    {
        if (item == null) return;

        int count = wholeStack ? (item.maxStack > 0 ? item.maxStack : 64) : 1;

        if (console == null) console = FindFirstObjectByType<CommandConsole>();
        if (console == null)
        {
            SetStatus("CommandConsole 을 찾을 수 없습니다.");
            return;
        }

        // itemName 에 공백이 있어도 된다 — /give 는 마지막 토큰만 개수로 본다.
        SetStatus(console.Execute($"/give {item.itemName} {count}"));
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message ?? "";
    }

    // ── UI 구성 ─────────────────────────────────────────────────

    private void BuildUI()
    {
        Canvas canvas = targetCanvas != null ? targetCanvas : FindFirstObjectByType<Canvas>();
        font = ResolveFont();

        panel = CreateRect("ItemBrowser", canvas.transform);
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.sizeDelta = new Vector2(PanelWidth, -40f);
        panelRect.anchoredPosition = new Vector2(-20f, 0f);

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.05f, 0.06f, 0.09f, 0.92f);

        TMP_Text title = CreateText("Title", panel.transform, 26f, TextAlignmentOptions.MidlineLeft);
        Stretch((RectTransform)title.transform, 1f, -38f, -8f, 12f, -12f);
        title.color = new Color(1f, 0.95f, 0.75f, 1f);
        title.text = "아이템 목록";

        TMP_Text hint = CreateText("Hint", panel.transform, 18f, TextAlignmentOptions.MidlineLeft);
        Stretch((RectTransform)hint.transform, 1f, -26f, -44f, 12f, -12f);
        hint.color = new Color(1f, 1f, 1f, 0.5f);
        hint.text = "좌클릭 1개 · 우클릭 한 스택 · P/ESC 닫기";

        // 스크롤 영역
        GameObject viewport = CreateRect("Viewport", panel.transform);
        RectTransform viewportRect = (RectTransform)viewport.transform;
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(8f, 44f);
        viewportRect.offsetMax = new Vector2(-8f, -74f);
        viewport.AddComponent<RectMask2D>();

        GameObject contentGO = CreateRect("Content", viewport.transform);
        content = (RectTransform)contentGO.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(0f, 0f);
        content.offsetMax = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = contentGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 2f;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = viewport.AddComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        statusText = CreateText("Status", panel.transform, 20f, TextAlignmentOptions.MidlineLeft);
        RectTransform statusRect = (RectTransform)statusText.transform;
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.offsetMin = new Vector2(12f, 6f);
        statusRect.offsetMax = new Vector2(-12f, 38f);
        statusText.color = new Color(1f, 0.95f, 0.6f, 1f);
        statusText.text = "";
    }

    /// <summary>딕셔너리의 아이템을 표시 이름 순으로 늘어놓는다. 한 번만 짓는다.</summary>
    private void BuildRows()
    {
        if (built || content == null) return;
        built = true;

        if (ItemDictionary.Instance == null)
        {
            SetStatus("ItemDictionary 를 찾을 수 없습니다.");
            return;
        }

        List<Items> items = new List<Items>(ItemDictionary.Instance.AllItems);
        items.Sort(delegate (Items a, Items b)
        {
            return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.CurrentCulture);
        });

        foreach (Items item in items) CreateRow(item);

        // 레이아웃을 지금 확정한다. 안 하면 여는 첫 프레임에 줄이 겹쳐 보이고,
        // ContentSizeFitter 가 높이를 잡기 전이라 스크롤도 한 프레임 동안 먹지 않는다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        SetStatus($"아이템 {items.Count}개");
    }

    private void CreateRow(Items item)
    {
        GameObject row = CreateRect("Row_" + item.itemName, content);
        RectTransform rowRect = (RectTransform)row.transform;
        rowRect.sizeDelta = new Vector2(0f, RowHeight);

        LayoutElement element = row.AddComponent<LayoutElement>();
        element.preferredHeight = RowHeight;
        element.minHeight = RowHeight;

        Image background = row.AddComponent<Image>();   // 클릭 판정을 받으려면 Graphic 이 있어야 한다

        GameObject iconGO = CreateRect("Icon", row.transform);
        RectTransform iconRect = (RectTransform)iconGO.transform;
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(8f, 0f);
        iconRect.sizeDelta = new Vector2(IconSize, IconSize);

        Image icon = iconGO.AddComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        // 도구는 자루 + 머리를 겹쳐 그려야 해서 sprite 를 직접 넣지 않는다.
        ItemIconView.Apply(icon, item, null);

        TMP_Text label = CreateText("Label", row.transform, 20f, TextAlignmentOptions.MidlineLeft);
        RectTransform labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(IconSize + 16f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);
        label.color = Color.white;
        label.text = item.DisplayName == item.itemName
            ? item.itemName
            : $"{item.DisplayName}  <alpha=#80>{item.itemName}";
        label.richText = true;

        row.AddComponent<ItemBrowserEntry>().Bind(this, item, background);
    }

    // ── 작은 도우미 ─────────────────────────────────────────────

    /// <summary>가로로 늘리고 세로 위치만 지정한다(위 기준, 값은 음수 = 아래로).</summary>
    private static void Stretch(RectTransform rect, float anchorY, float height, float top, float left, float right)
    {
        rect.anchorMin = new Vector2(0f, anchorY);
        rect.anchorMax = new Vector2(1f, anchorY);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, top + height);
        rect.offsetMax = new Vector2(right, top);
    }

    private static GameObject CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private TMP_Text CreateText(string name, Transform parent, float size, TextAlignmentOptions alignment)
    {
        GameObject go = CreateRect(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static TMP_FontAsset ResolveFont()
    {
        if (TMP_Settings.defaultFontAsset != null) return TMP_Settings.defaultFontAsset;
        TMP_Text any = FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include);
        return any != null ? any.font : null;
    }
}
