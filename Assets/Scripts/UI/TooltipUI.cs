using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 슬롯에 커서를 올렸을 때 뜨는 툴팁. 커서를 따라다니며 화면 밖으로 나가지 않게 접힌다.
///
/// UI를 런타임에 캔버스 아래로 직접 구성하므로 씬/프리팹 배선이 필요 없다(CommandConsole 과 같은 방식).
/// UIManager 에는 등록하지 않는다 — 등록하면 isAnyUIOpen 이 참이 되어 채굴·배치가 막힌다.
/// </summary>
public class TooltipUI : Singleton<TooltipUI>
{
    /// <summary>
    /// <b>씬을 넘겨 살려 두면 안 된다.</b> 이 클래스는 <c>Awake</c> 에서 패널을 캔버스 아래에 짓는데,
    /// 캔버스는 씬과 함께 죽는다 — 살아남은 싱글톤만 남고 패널은 파괴되어 <see cref="Display"/> 가
    /// 첫 줄에서 되돌아가고, 그 뒤로 툴팁이 영영 뜨지 않는다(<c>Awake</c> 는 다시 불리지 않는다).
    /// <see cref="UIManager"/> · <see cref="TilemapTextureLoader"/> 와 같은 규약이다.
    /// </summary>
    protected override bool PersistAcrossScenes => false;

    [Tooltip("비우면 씬에서 첫 번째 Canvas 를 사용")]
    [SerializeField] private Canvas targetCanvas;

    [Tooltip("커서와 툴팁 사이 간격(픽셀)")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(18f, -18f);

    [SerializeField] private float fontSize = 28f;

    private RectTransform panel;
    private RectTransform canvasRect;
    private TextMeshProUGUI label;
    private bool visible;

    // 떠 있는 동안 매 프레임 다시 물어볼 문구 공급자(없으면 고정 문구).
    // 전력·연료·진행도처럼 계속 변하는 값은 호버 시점에 한 번 읽으면 곧바로 낡은 값이 된다.
    private System.Func<string> provider;

    protected override void Awake()
    {
        base.Awake();
        BuildUI();
        Hide();
    }

    /// <summary>고정 문구로 툴팁을 띄운다. 내용이 비면 대신 숨긴다(빈 슬롯 등).</summary>
    public void Show(string text)
    {
        provider = null;
        Display(text);
    }

    /// <summary>
    /// 매 프레임 <paramref name="textProvider"/> 를 다시 불러 갱신되는 툴팁을 띄운다.
    /// 전력·연료 잔량·가공 진행도처럼 커서를 올려 둔 사이에도 계속 변하는 값에 쓴다.
    /// 호출자는 델리게이트를 필드에 캐시해 두는 편이 좋다(호버마다 새로 만들지 않도록).
    /// </summary>
    public void Show(System.Func<string> textProvider)
    {
        provider = textProvider;
        Display(textProvider != null ? textProvider() : "");
    }

    public void Hide()
    {
        visible = false;
        provider = null;
        if (panel != null && panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
    }

    private void Display(string text)
    {
        if (panel == null) return;
        if (string.IsNullOrWhiteSpace(text)) { Hide(); return; }

        label.text = text;
        if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
        visible = true;
        FollowCursor();   // 뜨는 첫 프레임부터 제자리에 있도록
    }

    private void LateUpdate()
    {
        if (!visible) return;

        if (provider != null)
        {
            string text = provider();
            if (string.IsNullOrWhiteSpace(text)) { Hide(); return; }   // 내용이 사라지면(슬롯이 비는 등) 접는다

            // 같은 문자열이면 건드리지 않는다 — 매 프레임 TMP 재조판과 레이아웃 재계산이 걸린다.
            if (label.text != text) label.text = text;
        }

        FollowCursor();
    }

    /// <summary>커서를 따라가되 캔버스 밖으로 나가면 반대 방향으로 접는다.</summary>
    private void FollowCursor()
    {
        if (canvasRect == null) return;

        Vector2 screenPoint = UnityEngine.InputSystem.Mouse.current != null
            ? UnityEngine.InputSystem.Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;

        Camera camera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? targetCanvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, camera, out Vector2 local))
            return;

        // 레이아웃이 아직 갱신되지 않았으면 크기를 먼저 확정한다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

        Vector2 size = panel.rect.size;
        Vector2 canvasSize = canvasRect.rect.size;
        Vector2 position = local + cursorOffset;

        // pivot 이 (0,1) 이므로 오른쪽/아래로 넘치면 커서 반대편에 붙인다.
        float rightEdge = canvasSize.x * 0.5f;
        float bottomEdge = -canvasSize.y * 0.5f;
        if (position.x + size.x > rightEdge) position.x = local.x - cursorOffset.x - size.x;
        if (position.y - size.y < bottomEdge) position.y = local.y - cursorOffset.y + size.y;

        // 그래도 넘치면 가장자리에 물린다.
        position.x = Mathf.Clamp(position.x, -rightEdge, rightEdge - size.x);
        position.y = Mathf.Clamp(position.y, bottomEdge + size.y, canvasSize.y * 0.5f);

        panel.anchoredPosition = position;
    }

    // ── UI 구성 ─────────────────────────────────────────────────
    private void BuildUI()
    {
        Canvas canvas = targetCanvas != null ? targetCanvas : FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[TooltipUI] 캔버스를 찾을 수 없습니다.", this);
            return;
        }
        targetCanvas = canvas;
        canvasRect = canvas.transform as RectTransform;

        GameObject go = new GameObject("Tooltip", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(canvas.transform, false);

        panel = (RectTransform)go.transform;
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0f, 1f);          // 좌상단 기준 = 커서 오른쪽 아래로 펼쳐짐

        Image background = go.AddComponent<Image>();
        background.color = new Color(0.05f, 0.06f, 0.09f, 0.92f);
        background.raycastTarget = false;            // 툴팁이 아래 슬롯의 호버를 가리지 않게

        HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 8, 8);
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.layer = go.layer;
        textGO.transform.SetParent(panel, false);

        label = textGO.AddComponent<TextMeshProUGUI>();
        label.font = ResolveFont();
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.richText = false;
        label.raycastTarget = false;

        panel.SetAsLastSibling();   // 다른 UI 위에 그려지도록
    }

    private static TMP_FontAsset ResolveFont()
    {
        if (TMP_Settings.defaultFontAsset != null) return TMP_Settings.defaultFontAsset;
        TMP_Text any = FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include);
        return any != null ? any.font : null;
    }
}
