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
    [Tooltip("비우면 씬에서 첫 번째 Canvas 를 사용")]
    [SerializeField] private Canvas targetCanvas;

    [Tooltip("커서와 툴팁 사이 간격(픽셀)")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(18f, -18f);

    [SerializeField] private float fontSize = 28f;

    private RectTransform panel;
    private RectTransform canvasRect;
    private TextMeshProUGUI label;
    private bool visible;

    protected override void Awake()
    {
        base.Awake();
        BuildUI();
        Hide();
    }

    /// <summary>툴팁을 띄운다. 내용이 비면 대신 숨긴다(빈 슬롯 등).</summary>
    public void Show(string text)
    {
        if (panel == null) return;

        if (string.IsNullOrWhiteSpace(text)) { Hide(); return; }

        label.text = text;
        if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
        visible = true;
        FollowCursor();   // 뜨는 첫 프레임부터 제자리에 있도록
    }

    public void Hide()
    {
        visible = false;
        if (panel != null && panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (visible) FollowCursor();
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
