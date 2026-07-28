using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 조합대 UI. 재료를 입력 슬롯이 아니라 플레이어 인벤토리에서 직접 소모하고 결과도 인벤토리로 돌려준다.
///
/// 카테고리 탭으로 레시피를 나누고, 조합대 티어보다 높은 레시피는 목록에서 제외하며,
/// 재료가 부족한 레시피는 흐리게 표시한다. 검색어로 결과물 이름을 걸러낼 수 있다.
///
/// <see cref="MachineUIHost"/> 가 패널을 <see cref="DefaultMachineUI"/> 타입으로 다루므로 이를 상속한다
/// (조합대 프리팹에는 MachineUIElement 가 없어 베이스의 슬롯/바 수집 결과는 비어 있다).
/// </summary>
public class CraftingTableUI : DefaultMachineUI
{
    [Header("조합대 UI 참조")]
    [SerializeField] private TMP_InputField searchField;
    [SerializeField] private RectTransform tabsRoot;
    [Tooltip("복제해 쓰는 비활성 탭 템플릿")]
    [SerializeField] private CraftingTableTab tabTemplate;
    [SerializeField] private RectTransform gridContent;
    [Tooltip("복제해 쓰는 비활성 레시피 슬롯 템플릿")]
    [SerializeField] private CraftRecipeSlot slotTemplate;
    [SerializeField] private TMP_Text titleText;

    [Header("탭 표시")]
    [SerializeField] private Color selectedTabColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color normalTabColor = new Color(1f, 1f, 1f, 0.45f);

    private readonly List<RecipeCategory> categories = new();
    private readonly List<Recipe> filtered = new();
    private readonly List<CraftingTableTab> tabPool = new();
    private readonly List<CraftRecipeSlot> slotPool = new();

    private MachineInstance instance;
    private RecipeCategory currentCategory;
    private int tier;
    private bool subscribed;
    private bool searchFocused;

    public override void Open(MachineInstance machine)
    {
        base.Open(machine);

        instance = machine;
        tier = machine != null && machine.Info is CraftingTableBlock table ? table.tier : 0;

        if (titleText != null)
            titleText.text = machine != null ? machine.blockId : "";

        if (searchField != null) searchField.SetTextWithoutNotify("");

        BuildTabs();
        RebuildGrid();
        Subscribe();
    }

    public override void Close()
    {
        Unsubscribe();
        ReleaseSearchFocus();
        instance = null;
        base.Close();
    }

    private void OnDisable()
    {
        // 패널이 다른 경로(호스트 전환 등)로 꺼져도 입력이 잠기지 않도록 확실히 되돌린다.
        Unsubscribe();
        ReleaseSearchFocus();
    }

    // ── 인벤토리 변경 구독 (재료가 바뀌면 흐림 상태를 다시 계산) ──
    private void Subscribe()
    {
        if (subscribed || Inventory.Instance == null) return;
        Inventory.Instance.OnChanged += HandleInventoryChanged;
        if (searchField != null) searchField.onValueChanged.AddListener(HandleSearchChanged);
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (Inventory.Instance != null) Inventory.Instance.OnChanged -= HandleInventoryChanged;
        if (searchField != null) searchField.onValueChanged.RemoveListener(HandleSearchChanged);
        subscribed = false;
    }

    private void HandleInventoryChanged() => RefreshCraftable();
    private void HandleSearchChanged(string _) => RebuildGrid();

    // ── 탭 ────────────────────────────────────────────────────
    private void BuildTabs()
    {
        if (tabsRoot == null || tabTemplate == null) return;

        RecipeDictionary dictionary = RecipeDictionary.Instance;
        if (dictionary != null && instance != null)
            dictionary.CollectCategories(instance.blockId, tier, categories);
        else
            categories.Clear();

        for (int i = 0; i < categories.Count; i++)
        {
            CraftingTableTab tab = GetTab(i);
            RecipeCategory category = categories[i];

            tab.Bind(category);

            tab.Button.onClick.RemoveAllListeners();
            RecipeCategory captured = category;              // 클로저 캡처용
            tab.Button.onClick.AddListener(() => SelectCategory(captured));
            tab.gameObject.SetActive(true);
        }

        for (int i = categories.Count; i < tabPool.Count; i++)
            tabPool[i].gameObject.SetActive(false);

        // 이전 선택이 사라졌으면 첫 탭으로
        if (currentCategory == null || !categories.Contains(currentCategory))
            currentCategory = categories.Count > 0 ? categories[0] : null;

        HighlightTabs();
    }

    private CraftingTableTab GetTab(int index)
    {
        while (tabPool.Count <= index)
        {
            CraftingTableTab tab = Instantiate(tabTemplate, tabsRoot);
            tab.name = "Tab" + tabPool.Count;
            tabPool.Add(tab);
        }
        return tabPool[index];
    }

    private void SelectCategory(RecipeCategory category)
    {
        currentCategory = category;
        HighlightTabs();
        RebuildGrid();
    }

    private void HighlightTabs()
    {
        for (int i = 0; i < categories.Count && i < tabPool.Count; i++)
            tabPool[i].SetSelected(categories[i] == currentCategory, selectedTabColor, normalTabColor);
    }

    // ── 레시피 그리드 ──────────────────────────────────────────
    private void RebuildGrid()
    {
        if (gridContent == null || slotTemplate == null) return;

        RecipeDictionary dictionary = RecipeDictionary.Instance;
        if (dictionary != null && instance != null)
            dictionary.CollectRecipes(instance.blockId, currentCategory, tier, SearchText, filtered);
        else
            filtered.Clear();

        List<ItemStack> slots = Inventory.Instance != null ? Inventory.Instance.slots : null;

        for (int i = 0; i < filtered.Count; i++)
        {
            CraftRecipeSlot slot = GetSlot(i);
            slot.Bind(filtered[i], slots != null && RecipeSolver.HasInputs(slots, filtered[i]));
            slot.gameObject.SetActive(true);
        }

        for (int i = filtered.Count; i < slotPool.Count; i++)
            slotPool[i].gameObject.SetActive(false);
    }

    /// <summary>목록 구성은 그대로 두고 재료 충족 여부(흐림)만 다시 계산한다.</summary>
    private void RefreshCraftable()
    {
        List<ItemStack> slots = Inventory.Instance != null ? Inventory.Instance.slots : null;
        for (int i = 0; i < filtered.Count && i < slotPool.Count; i++)
            slotPool[i].SetCraftable(slots != null && RecipeSolver.HasInputs(slots, filtered[i]));
    }

    private CraftRecipeSlot GetSlot(int index)
    {
        while (slotPool.Count <= index)
        {
            CraftRecipeSlot slot = Instantiate(slotTemplate, gridContent);
            slot.name = "RecipeSlot" + slotPool.Count;
            slot.OnClicked += Craft;
            slotPool.Add(slot);
        }
        return slotPool[index];
    }

    private string SearchText => searchField != null ? searchField.text : null;

    // ── 제작 ──────────────────────────────────────────────────
    /// <summary>인벤토리 재료를 소모해 결과물을 지급한다.</summary>
    public void Craft(Recipe recipe)
    {
        Inventory inventory = Inventory.Instance;
        if (recipe == null || inventory == null || inventory.slots == null) return;

        if (!RecipeSolver.HasInputs(inventory.slots, recipe)) return;

        // 적재 가능 여부를 소모 "전에" 검사한다. 재료를 빼면 자리가 생기는 경계 상황에서
        // 보수적으로 거절할 수 있지만, 결과물이 사라지는 것보다 안전하다.
        if (!RecipeSolver.CanStoreOutputs(inventory.slots, recipe))
        {
            Debug.LogWarning("[CraftingTableUI] 인벤토리에 자리가 없어 제작하지 못했습니다.", this);
            return;
        }

        RecipeSolver.ConsumeInputs(inventory.slots, recipe);
        RecipeSolver.StoreOutputs(inventory.slots, recipe);
        inventory.NotifyChanged();   // → HandleInventoryChanged → 흐림 상태 재계산
    }

    // ── 검색창 포커스 중 게임 입력 차단 ─────────────────────────
    // 포커스된 동안 i/숫자키가 게임으로 가면 InventoryToggle 이 기계 뷰를 닫아버린다.
    // CommandConsole 과 같은 방식으로 EventSystem 선택 상태를 폴링해 플레이어 입력을 껐다 켠다.
    private void Update()
    {
        if (searchField == null) return;

        bool focused = EventSystem.current != null
            && EventSystem.current.currentSelectedGameObject == searchField.gameObject;

        if (focused == searchFocused) return;
        searchFocused = focused;

        if (InputActionManager.Instance != null)
            InputActionManager.Instance.SetPlayerInputEnabled(!focused);
    }

    private void ReleaseSearchFocus()
    {
        if (!searchFocused) return;
        searchFocused = false;
        if (InputActionManager.Instance != null)
            InputActionManager.Instance.SetPlayerInputEnabled(true);
    }
}
