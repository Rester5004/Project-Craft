using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// 발전기의 "전력 전송" 설정 모드. 화면을 덮고 발전 범위 안의 기계를 색 타일로 보여 준다.
///   빨강 = 아직 연결 안 됨 · 초록 = 연결됨 · 파랑 = 발전기 자신
///   좌클릭 = 연결 · 우클릭 = 해제 · 좌상단 "원래대로"(또는 ESC) = 나가기
///
/// UI 는 런타임에 캔버스 아래로 직접 만들므로 씬 배선이 필요 없다(<see cref="CommandConsole"/> 와 같은 규약).
/// 이 컴포넌트는 <b>항상 활성인 오브젝트</b>에 붙이고 panel 만 껐다 켠다 — 자기 자신이 꺼지면
/// Update 가 멈춰 다시 들어올 방법이 사라지기 때문이다.
/// </summary>
public class PowerLinkMode : MonoBehaviour
{
    private const string UIName = "PowerLink";

    // 오버레이 타일맵의 정렬 순서. 160 이상은 비어 있어 무엇보다 위에 그려진다
    // (Blocks/Floor 100 · FloorTexture 110 · 기계·파이프 120 · 플레이어 130 · WallTop 140 ·
    //  OutLine/Placeable 150 · 설치 미리보기 170·180).
    // ⚠ 예전 배율의 6 이었는데 정렬 순서가 (옛값 + 10) × 10 으로 바뀔 때 빠졌다 —
    //    6 은 바닥(100)보다 아래라 <b>전송 모드 오버레이가 통째로 안 보였다.</b>
    private const int OverlaySortingOrder = 160;

    private static readonly Color UnlinkedColor = new Color(1f, 0.25f, 0.25f, 0.55f);
    private static readonly Color LinkedColor = new Color(0.3f, 1f, 0.35f, 0.55f);
    private static readonly Color GeneratorColor = new Color(0.4f, 0.7f, 1f, 0.55f);

    [Tooltip("비우면 씬에서 첫 번째 Canvas 를 사용")]
    [SerializeField] private Canvas targetCanvas;

    public static PowerLinkMode Instance { get; private set; }

    /// <summary>전송 모드가 켜져 있는가. PlayerInteraction 이 커서 윤곽선·채굴을 멈추는 데 쓴다.</summary>
    public static bool IsActive => Instance != null && Instance.isOpen;

    private GameObject panel;
    private bool isOpen;
    private MachineInstance generator;

    private Tilemap overlay;
    private Tile solidTile;
    private readonly List<Vector3Int> painted = new();
    private readonly HashSet<Vector2Int> candidates = new();

    private SpriteRenderer playerRenderer;
    private MachineInteraction machineInteraction;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        BuildUI();
        if (UIManager.Instance != null) UIManager.Instance.AddUI(panel, UIName);
        panel.SetActive(false);
    }

    // ── 진입 / 종료 ─────────────────────────────────────────────

    /// <summary>발전기의 전송 설정을 시작한다. 발전기가 아니면 아무 일도 하지 않는다.</summary>
    public void Enter(MachineInstance generatorInstance)
    {
        if (isOpen || generatorInstance == null || !generatorInstance.IsGenerator || panel == null) return;

        generator = generatorInstance;

        // 전체화면 패널이 겹치지 않도록 기계 UI 를 먼저 닫는다.
        if (machineInteraction == null) machineInteraction = FindFirstObjectByType<MachineInteraction>();
        if (machineInteraction != null) machineInteraction.CloseView();

        // 매번 등록을 보장한다. 등록이 안 돼 있으면 OpenUI 가 조용히 실패하는데,
        // 그 상태로 isOpen 을 세우면 영구히 빠져나갈 수 없게 된다.
        if (UIManager.Instance != null)
        {
            UIManager.Instance.AddUI(panel, UIName);
            UIManager.Instance.OpenUI(UIName);
        }
        panel.SetActive(true);

        isOpen = panel.activeInHierarchy;
        if (!isOpen)
        {
            Debug.LogError("[PowerLinkMode] 패널을 활성화하지 못했습니다.", panel);
            generator = null;
            return;
        }

        // 플레이어 액션맵 전체를 끈다(이동·채굴·배치·핫바). UI 클릭은 EventSystem 이
        // 별도 액션 에셋으로 처리하므로 이 모드의 좌/우클릭은 그대로 살아 있다.
        if (InputActionManager.Instance != null)
            InputActionManager.Instance.SetPlayerInputEnabled(false);

        SetPlayerVisible(false);
        if (TilemapTextureLoader.Instance != null) TilemapTextureLoader.Instance.ClearOutline();

        Refresh();
    }

    /// <summary>모드를 끝내고 들어오기 직전 상태(발전기 UI)로 되돌린다.</summary>
    public void Exit()
    {
        isOpen = false;
        ClearOverlay();
        SetPlayerVisible(true);

        if (InputActionManager.Instance != null)
            InputActionManager.Instance.SetPlayerInputEnabled(true);

        if (UIManager.Instance != null) UIManager.Instance.CloseUI(UIName);
        if (panel != null && panel.activeSelf) panel.SetActive(false);

        MachineInstance back = generator;
        generator = null;
        if (machineInteraction != null && back != null) machineInteraction.OpenMachine(back);
    }

    private void Update()
    {
        if (!isOpen) return;

        // 상태 어긋남 복구: 패널이 외부에서 꺼졌는데 isOpen 이 남아 있으면 조작이 잠긴다.
        if (panel == null || !panel.activeInHierarchy) { Exit(); return; }

        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Exit();
    }

    /// <summary>
    /// Cinemachine 이 플레이어 트랜스폼을 추적 중이라 SetActive 는 위험하다. 렌더러만 끈다.
    /// 이동은 UIManager 에 이 모드가 열려 있어 PlayerForTest 가 알아서 막는다.
    /// </summary>
    private void SetPlayerVisible(bool visible)
    {
        if (playerRenderer == null)
        {
            PlayerInteraction player = FindFirstObjectByType<PlayerInteraction>();
            if (player != null) playerRenderer = player.GetComponentInChildren<SpriteRenderer>(true);
        }
        if (playerRenderer != null) playerRenderer.enabled = visible;
    }

    // ── 오버레이 ────────────────────────────────────────────────

    /// <summary>범위 안의 연결 가능한 기계를 다시 칠한다.</summary>
    private void Refresh()
    {
        ClearOverlay();
        candidates.Clear();
        if (generator == null || MapGenerator.Active == null) return;

        EnsureOverlay();
        if (overlay == null) return;

        Paint(generator.worldCell, GeneratorColor);

        // LoadedMachines 는 여러 칸 기계를 <b>덮는 칸마다</b> 담고 있다. 칠하기는 그대로 두어
        // 큰 기계가 점 하나로 보이지 않게 하고, <b>링크·거리·연결 여부는 기준점(worldCell)</b>으로 묻는다 —
        // 링크가 칸 단위라 정규화하지 않으면 한 기계가 전력을 칸 수만큼 받아 간다.
        foreach (KeyValuePair<Vector2Int, MachineInstance> pair in MapGenerator.Active.LoadedMachines)
        {
            Vector2Int cell = pair.Key;
            MachineInstance machine = pair.Value;
            if (machine == null) continue;
            if (machine == generator) { Paint(cell, GeneratorColor); continue; }   // 자기 자신은 전 칸이 파랑
            if (!generator.IsInPowerRange(machine.worldCell)) continue;

            // 전력을 쓰는 기계와, 발전은 안 하고 전송만 하는 중계기가 대상이다.
            bool relay = machine.Info != null && machine.Info.powerRange > 0 && !machine.IsGenerator;
            if (!machine.UsesEnergy && !relay) continue;

            candidates.Add(cell);
            Paint(cell, generator.IsLinkedTo(machine.worldCell) ? LinkedColor : UnlinkedColor);
        }
    }

    /// <summary>연결 상태를 칠하는 전용 타일맵을 만든다(씬을 건드리지 않도록 런타임 생성).</summary>
    private void EnsureOverlay()
    {
        if (overlay != null) return;

        Tilemap blocks = MapGenerator.Active != null ? MapGenerator.Active.blocksTilemap : null;
        if (blocks == null) return;

        GameObject go = new GameObject("PowerLinkOverlay", typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(blocks.transform.parent, false);   // 같은 Grid 아래여야 셀이 맞는다

        overlay = go.GetComponent<Tilemap>();
        TilemapRenderer renderer = go.GetComponent<TilemapRenderer>();
        renderer.sortingOrder = OverlaySortingOrder;
    }

    /// <summary>
    /// 한 칸을 통째로 채우는 흰 타일. 칸마다 Tile 을 새로 만들면 인스턴스가 쌓이므로
    /// 하나만 만들어 두고 <see cref="Tilemap.SetColor"/> 로 색을 입힌다.
    /// </summary>
    private Tile SolidTile()
    {
        if (solidTile != null) return solidTile;

        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.filterMode = FilterMode.Point;
        texture.Apply();

        // pixelsPerUnit = 1, 1x1 픽셀 → 셀 크기(1)와 정확히 같아진다.
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

        solidTile = ScriptableObject.CreateInstance<Tile>();
        solidTile.sprite = sprite;
        return solidTile;
    }

    private void Paint(Vector2Int cell, Color color)
    {
        Vector3Int pos = (Vector3Int)cell;
        overlay.SetTile(pos, SolidTile());
        overlay.SetTileFlags(pos, TileFlags.None);   // 이걸 빼면 SetColor 가 조용히 무시된다
        overlay.SetColor(pos, color);
        painted.Add(pos);
    }

    private void ClearOverlay()
    {
        if (overlay != null)
            for (int i = 0; i < painted.Count; i++) overlay.SetTile(painted[i], null);
        painted.Clear();
    }

    // ── 클릭 ────────────────────────────────────────────────────
    private void HandleClick(PointerEventData data)
    {
        if (!isOpen || generator == null || Camera.main == null || MapGenerator.Active == null) return;

        // z 를 버리고 Vector2 로 받는다. ScreenToWorldPoint 의 z 를 그대로 넘기면 엉뚱한 셀이 나온다.
        Vector2 world = Camera.main.ScreenToWorldPoint(data.position);
        Vector2Int cell = (Vector2Int)MapGenerator.Active.blocksTilemap.WorldToCell(world);

        if (!candidates.Contains(cell)) return;   // 범위 밖이거나 연결할 수 없는 칸

        // 링크는 칸이 아니라 기계를 가리켜야 한다. 여러 칸 기계의 어느 칸을 눌렀든
        // <b>기준점 하나</b>로 이어야 같은 기계가 라운드로빈에서 칸 수만큼 몫을 챙기지 않는다.
        Vector2Int link = MapGenerator.Active.TryGetMachineAt(cell, out MachineInstance clicked) && clicked != null
            ? clicked.worldCell : cell;

        if (data.button == PointerEventData.InputButton.Left) generator.AddLink(link);
        else if (data.button == PointerEventData.InputButton.Right) generator.RemoveLink(link);
        else return;

        Refresh();
    }

    // ── UI 구성 ─────────────────────────────────────────────────
    private void BuildUI()
    {
        Canvas canvas = targetCanvas != null ? targetCanvas : FindFirstObjectByType<Canvas>();
        TMP_FontAsset font = ResolveFont();

        panel = CreateRect("PowerLinkMode", canvas.transform);
        Stretch((RectTransform)panel.transform);

        // 전체화면 블로커. 이것 하나가 배치·채굴·기계 열기를 전부 막는다
        // (PlayerInteraction 이 EventSystem.IsPointerOverGameObject 를 보기 때문).
        GameObject blockerGO = CreateRect("Blocker", panel.transform);
        Stretch((RectTransform)blockerGO.transform);
        Image blocker = blockerGO.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.1f);
        blocker.raycastTarget = true;
        blockerGO.AddComponent<PowerLinkClickArea>().OnClicked = HandleClick;

        // 좌상단 "원래대로" 버튼 — 블로커보다 위에 있어 스스로 클릭을 소비한다.
        GameObject backGO = CreateRect("BackButton", panel.transform);
        RectTransform backRect = (RectTransform)backGO.transform;
        backRect.anchorMin = new Vector2(0f, 1f);
        backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0f, 1f);
        backRect.anchoredPosition = new Vector2(24f, -24f);
        backRect.sizeDelta = new Vector2(180f, 56f);
        Image backBg = backGO.AddComponent<Image>();
        backBg.color = new Color(0.1f, 0.1f, 0.12f, 0.9f);
        Button backButton = backGO.AddComponent<Button>();
        backButton.onClick.AddListener(Exit);

        TextMeshProUGUI backLabel = CreateText("Label", backGO.transform, font);
        backLabel.text = "원래대로";
        backLabel.fontSize = 28f;
        backLabel.alignment = TextAlignmentOptions.Center;
        backLabel.color = Color.white;

        // 상단 안내문
        GameObject hintGO = CreateRect("Hint", panel.transform);
        RectTransform hintRect = (RectTransform)hintGO.transform;
        hintRect.anchorMin = new Vector2(0.5f, 1f);
        hintRect.anchorMax = new Vector2(0.5f, 1f);
        hintRect.pivot = new Vector2(0.5f, 1f);
        hintRect.anchoredPosition = new Vector2(0f, -28f);
        hintRect.sizeDelta = new Vector2(900f, 44f);
        TextMeshProUGUI hint = hintGO.AddComponent<TextMeshProUGUI>();
        hint.font = font;
        hint.text = "왼쪽 클릭: 연결   |   오른쪽 클릭: 연결 해제   |   ESC: 나가기";
        hint.fontSize = 26f;
        hint.alignment = TextAlignmentOptions.Center;
        hint.color = new Color(1f, 0.95f, 0.7f, 1f);
        hint.raycastTarget = false;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static GameObject CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font)
    {
        GameObject go = CreateRect(name, parent);
        Stretch((RectTransform)go.transform);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
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
