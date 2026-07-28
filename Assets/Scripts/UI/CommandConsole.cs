using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Enter 로 여는 명령어 입력창.
///   /give &lt;아이템명&gt; &lt;개수&gt;  → 아이템 지급
///   /remove &lt;슬롯인덱스&gt;      → 해당 슬롯 비우기
/// UI는 런타임에 캔버스 아래로 직접 구성하므로 씬 배선이 필요 없다.
/// </summary>
public class CommandConsole : MonoBehaviour
{
    private const string UIName = "Console";

    [Tooltip("비우면 씬에서 첫 번째 Canvas 를 사용")]
    [SerializeField] private Canvas targetCanvas;

    private GameObject panel;
    private TMP_InputField inputField;
    private TMP_Text resultText;
    private bool isOpen;
    private int lastCloseFrame = -1;
    private int focusAtFrame = -1;   // >=0 이면 "아직 입력창을 선택하지 않은 지연 활성화 대기 상태"

    private void Start()
    {
        BuildUI();
        if (UIManager.Instance != null) UIManager.Instance.AddUI(panel, UIName);
        panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (InputActionManager.Instance != null)
            InputActionManager.Instance.OnConsolePerformed += HandleConsoleKey;
    }

    private void OnDisable()
    {
        if (InputActionManager.Instance != null)
            InputActionManager.Instance.OnConsolePerformed -= HandleConsoleKey;
    }

    private void HandleConsoleKey()
    {
        // 제출로 방금 닫힌 프레임의 Enter 가 다시 창을 열지 않도록 방지
        if (isOpen || Time.frameCount == lastCloseFrame) return;
        Open();
    }

    public void Open()
    {
        if (isOpen || panel == null) return;

        // 매번 등록을 보장한다. 등록이 안 돼 있으면 OpenUI 가 조용히 실패하는데,
        // 그 상태로 isOpen 을 세우면 이후 Enter 가 영구히 막힌다.
        if (UIManager.Instance != null)
        {
            UIManager.Instance.AddUI(panel, UIName);
            UIManager.Instance.OpenUI(UIName);
        }
        panel.SetActive(true); // 어떤 경로로도 안 켜졌으면 직접 켠다

        // 실제로 켜진 것을 확인한 뒤에 상태를 확정한다.
        isOpen = panel.activeInHierarchy;
        if (!isOpen)
        {
            Debug.LogError("[CommandConsole] 패널을 활성화하지 못했습니다.", panel);
            return;
        }

        // 입력 중에는 게임 조작(i, 숫자키, 마우스)이 먹히지 않도록 차단
        if (InputActionManager.Instance != null)
            InputActionManager.Instance.SetPlayerInputEnabled(false);

        inputField.text = "";

        // 창을 연 Enter 는 아직 이 프레임의 IMGUI 이벤트 큐에 KeyDown 으로 남아 있다.
        // 지금 입력창을 선택하면 EventSystem 이 같은 프레임에 그 Enter 를 제출로 소비해
        // 창이 열리자마자 닫힌다. 그래서 선택/활성화만 다음 프레임으로 미룬다.
        focusAtFrame = Time.frameCount + 1;
    }

    /// <summary>isOpen 여부와 무관하게 항상 정리한다(수동 활성화 등으로 상태가 어긋나도 복구되도록).</summary>
    public void Close()
    {
        isOpen = false;
        focusAtFrame = -1;
        lastCloseFrame = Time.frameCount;

        if (inputField != null)
        {
            inputField.DeactivateInputField();
            if (EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == inputField.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }

        if (UIManager.Instance != null) UIManager.Instance.CloseUI(UIName);
        if (panel != null && panel.activeSelf) panel.SetActive(false);

        if (InputActionManager.Instance != null)
            InputActionManager.Instance.SetPlayerInputEnabled(true);
    }

    /// <summary>입력창에 포커스를 준다(활성화 직후엔 TMP 가 포커스를 놓칠 수 있어 Update 에서 재시도).</summary>
    private void FocusInput()
    {
        if (inputField == null) return;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(inputField.gameObject);
        inputField.ActivateInputField();
    }

    private void Update()
    {
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null || panel == null) return;

        // 상태 어긋남 복구: 패널이 꺼졌는데 isOpen 이 남아 있으면 잠기므로 정리한다.
        if (isOpen && !panel.activeInHierarchy) Close();

        if (isOpen)
        {
            // 열려 있는 동안 ESC 로 취소
            if (keyboard.escapeKey.wasPressedThisFrame) { Close(); return; }

            // 지연 활성화 대기 중에는 아래 포커스 회복을 돌리지 않는다.
            // (돌리면 창을 연 프레임에 결국 입력창이 선택되어 같은 문제가 재현된다)
            if (focusAtFrame >= 0)
            {
                if (Time.frameCount >= focusAtFrame) { focusAtFrame = -1; FocusInput(); }
                return;
            }
            // 인게임 클릭 등으로 포커스를 잃으면 Enter(제출)가 죽으므로 다시 잡아준다.
            if (EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject != inputField.gameObject)
                FocusInput();
            return;
        }
    }

    private void OnSubmit(string raw)
    {
        if (focusAtFrame >= 0) return; // 아직 의도적으로 활성화하지 않았다 = 창을 연 Enter 가 새어 들어온 것

        string message = Execute(raw);
        if (!string.IsNullOrEmpty(message))
        {
            resultText.text = message;
            Debug.Log("[Console] " + message);
        }
        Close();
    }

    /// <summary>명령어 한 줄을 실행하고 결과 메시지를 돌려준다.</summary>
    public string Execute(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        string[] tokens = raw.Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        string command = tokens[0].TrimStart('/').ToLowerInvariant();

        switch (command)
        {
            case "give": return CommandGive(tokens);
            case "remove": return CommandRemove(tokens);
            case "help": return "/give <item> <count>  |  /remove <slotIndex>";
            default: return $"Unknown command: '{command}' (try /help)";
        }
    }

    private string CommandGive(string[] tokens)
    {
        if (tokens.Length < 3) return "Usage: /give <item> <count>";

        // 아이템 이름에 공백이 있을 수 있으므로 마지막 토큰만 개수로 본다.
        string countToken = tokens[tokens.Length - 1];
        if (!int.TryParse(countToken, out int count) || count <= 0)
            return $"Invalid count: '{countToken}'";

        string itemName = string.Join(" ", tokens, 1, tokens.Length - 2);
        if (ItemDictionary.Instance == null) return "ItemDictionary not found.";

        Items item = ItemDictionary.Instance.FindItem(itemName);
        if (item == null) return $"Item not found: '{itemName}'" + SuggestNames(itemName);

        Inventory inventory = Inventory.Instance;
        if (inventory == null) return "Inventory not found.";

        if (!inventory.AddItem(item, count)) return $"Inventory full: could not add '{item.itemName}'.";
        return $"Gave {item.itemName} x{count}";
    }

    private string CommandRemove(string[] tokens)
    {
        if (tokens.Length < 2) return "Usage: /remove <slotIndex>";
        if (!int.TryParse(tokens[1], out int index))
            return $"Invalid slot index: '{tokens[1]}'";

        Inventory inventory = Inventory.Instance;
        if (inventory == null || inventory.slots == null) return "Inventory not found.";
        if (index < 0 || index >= inventory.slots.Count)
            return $"Slot index out of range: {index} (0-{inventory.slots.Count - 1})";

        ItemStack stack = inventory.slots[index];
        if (stack.item == null || stack.count <= 0) return $"Slot {index} is already empty.";

        string removed = $"{stack.item.itemName} x{stack.count}";
        stack.item = null;
        stack.count = 0;
        inventory.NotifyChanged();
        return $"Removed {removed} from slot {index}";
    }

    /// <summary>오타 시 비슷한 이름 몇 개를 알려준다.</summary>
    private static string SuggestNames(string typed)
    {
        if (ItemDictionary.Instance == null || string.IsNullOrEmpty(typed)) return "";
        List<string> hits = new();
        foreach (string name in ItemDictionary.Instance.ItemNames)
        {
            if (name.IndexOf(typed, System.StringComparison.OrdinalIgnoreCase) >= 0) hits.Add(name);
            if (hits.Count >= 5) break;
        }
        return hits.Count > 0 ? "\nDid you mean: " + string.Join(", ", hits) : "";
    }

    // ── UI 구성 ─────────────────────────────────────────────────
    private void BuildUI()
    {
        Canvas canvas = targetCanvas != null ? targetCanvas : FindFirstObjectByType<Canvas>();
        TMP_FontAsset font = ResolveFont();

        panel = CreateRect("CommandConsole", canvas.transform);
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.offsetMin = new Vector2(40f, 40f);
        panelRect.offsetMax = new Vector2(-40f, 40f);
        panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, 110f);

        // 결과 메시지(입력창 위)
        GameObject resultGO = CreateRect("Result", panel.transform);
        RectTransform resultRect = (RectTransform)resultGO.transform;
        resultRect.anchorMin = new Vector2(0f, 1f);
        resultRect.anchorMax = new Vector2(1f, 1f);
        resultRect.pivot = new Vector2(0.5f, 0f);
        resultRect.offsetMin = new Vector2(8f, 4f);
        resultRect.offsetMax = new Vector2(-8f, 44f);
        resultText = resultGO.AddComponent<TextMeshProUGUI>();
        resultText.font = font;
        resultText.fontSize = 26f;
        resultText.color = new Color(1f, 0.95f, 0.6f, 1f);
        resultText.alignment = TextAlignmentOptions.BottomLeft;
        resultText.raycastTarget = false;
        resultText.text = "";

        // 입력창
        GameObject fieldGO = CreateRect("InputField", panel.transform);
        RectTransform fieldRect = (RectTransform)fieldGO.transform;
        fieldRect.anchorMin = Vector2.zero;
        fieldRect.anchorMax = Vector2.one;
        fieldRect.offsetMin = Vector2.zero;
        fieldRect.offsetMax = Vector2.zero;
        Image background = fieldGO.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.78f);

        GameObject viewport = CreateRect("Text Area", fieldGO.transform);
        RectTransform viewportRect = (RectTransform)viewport.transform;
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(14f, 8f);
        viewportRect.offsetMax = new Vector2(-14f, -8f);
        viewport.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = CreateText("Placeholder", viewport.transform, font);
        placeholder.text = "/give <item> <count>   |   /remove <slotIndex>";
        placeholder.color = new Color(1f, 1f, 1f, 0.4f);

        TextMeshProUGUI text = CreateText("Text", viewport.transform, font);
        text.color = Color.white;

        inputField = fieldGO.AddComponent<TMP_InputField>();
        inputField.textViewport = viewportRect;
        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        inputField.fontAsset = font;
        inputField.pointSize = 30f;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.restoreOriginalTextOnEscape = false;
        inputField.onSubmit.AddListener(OnSubmit);
    }

    private static GameObject CreateRect(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font)
    {
        GameObject go = CreateRect(name, parent);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = 30f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.richText = false;
        return text;
    }

    private static TMP_FontAsset ResolveFont()
    {
        if (TMP_Settings.defaultFontAsset != null) return TMP_Settings.defaultFontAsset;
        TMP_Text any = FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include);
        return any != null ? any.font : null;
    }
}
