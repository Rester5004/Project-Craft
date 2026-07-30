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

    [Header("상세 패널 (검색창 아래)")]
    [Tooltip("소모 재료 줄이 쌓이는 부모")]
    [SerializeField] private RectTransform materialList;
    [Tooltip("복제해 쓰는 비활성 재료 줄 템플릿")]
    [SerializeField] private TMP_Text materialLineTemplate;
    [SerializeField] private Button craftButton;
    [SerializeField] private TMP_Text craftButtonLabel;

    [Header("도구 조립 (부품 칸)")]
    [Tooltip("부품 칸이 가로로 놓이는 부모. 일반 레시피를 고르면 통째로 꺼진다.")]
    [SerializeField] private RectTransform partSlotsRoot;
    [Tooltip("복제해 쓰는 비활성 부품 칸 템플릿")]
    [SerializeField] private ToolPartSlotUI partSlotTemplate;

    [Header("탭 표시")]
    [SerializeField] private Color selectedTabColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color normalTabColor = new Color(1f, 1f, 1f, 0.45f);

    [Header("재료 표시 색")]
    [SerializeField] private Color enoughColor = new Color(0.85f, 0.9f, 0.95f, 1f);
    [SerializeField] private Color shortColor = new Color(1f, 0.45f, 0.4f, 1f);

    private readonly List<RecipeCategory> categories = new();
    private readonly List<Recipe> filtered = new();
    private readonly List<CraftingTableTab> tabPool = new();
    private readonly List<CraftRecipeSlot> slotPool = new();
    private readonly List<TMP_Text> materialPool = new();
    private readonly List<ToolPartSlotUI> partSlotPool = new();

    private MachineInstance instance;
    private MachineInventory machineInventory;   // 부품 칸이 보는 저장소(구독 해제 때 instance 가 null 이어도 되도록 따로 들고 있는다)
    private RecipeCategory currentCategory;
    private Recipe selectedRecipe;
    private int tier;
    private bool subscribed;
    private bool searchFocused;

    /// <summary>조합대 프리팹에는 MachineUIElement 가 없는 것이 정상이다(부품 칸은 이 클래스가 직접 만든다).</summary>
    protected override bool WarnOnElementShortage => false;

    public override void Open(MachineInstance machine)
    {
        base.Open(machine);

        instance = machine;
        machineInventory = machine != null ? machine.inventory : null;
        tier = machine != null ? machine.Tier : 0;   // 티어는 MachineBlock 공통 필드

        if (titleText != null)
            titleText.text = MachineTitle(machine);

        if (searchField != null) searchField.SetTextWithoutNotify("");

        selectedRecipe = null;
        BuildTabs();
        RebuildGrid();
        Subscribe();
    }

    public override void Close()
    {
        Unsubscribe();
        ReleaseSearchFocus();
        instance = null;
        machineInventory = null;
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
        // 부품 칸은 기계 인벤토리를 보므로 그쪽 변경도 직접 듣는다
        // (조합대 프리팹에는 MachineUIElement 가 없어 베이스의 RefreshSlots 로는 갱신되지 않는다).
        if (machineInventory != null) machineInventory.OnChanged += HandlePartsChanged;
        if (searchField != null) searchField.onValueChanged.AddListener(HandleSearchChanged);
        if (craftButton != null) craftButton.onClick.AddListener(CraftSelected);
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (Inventory.Instance != null) Inventory.Instance.OnChanged -= HandleInventoryChanged;
        if (machineInventory != null) machineInventory.OnChanged -= HandlePartsChanged;
        if (searchField != null) searchField.onValueChanged.RemoveListener(HandleSearchChanged);
        if (craftButton != null) craftButton.onClick.RemoveListener(CraftSelected);
        subscribed = false;
    }

    /// <summary>부품 칸의 내용이 바뀌면 칸 뷰와 조합 버튼 상태를 다시 그린다.</summary>
    private void HandlePartsChanged()
    {
        for (int i = 0; i < partSlotPool.Count; i++)
            if (partSlotPool[i].gameObject.activeSelf) partSlotPool[i].Refresh();

        RefreshDetail();
    }

    private void HandleInventoryChanged()
    {
        RefreshCraftable();
        RefreshDetail();   // 소지량과 버튼 활성 상태도 함께 갱신
    }
    private void HandleSearchChanged(string _) => RebuildGrid();

    // ── 탭 ────────────────────────────────────────────────────
    private void BuildTabs()
    {
        if (tabsRoot == null || tabTemplate == null) return;

        RecipeDictionary dictionary = RecipeDictionary.Instance;
        if (dictionary != null && instance != null)
            dictionary.CollectCategories(instance.RecipeKey, tier, categories);
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
            dictionary.CollectRecipes(instance.RecipeKey, currentCategory, tier, SearchText, filtered);
        else
            filtered.Clear();

        List<ItemStack> slots = Inventory.Instance != null ? Inventory.Instance.slots : null;

        for (int i = 0; i < filtered.Count; i++)
        {
            CraftRecipeSlot slot = GetSlot(i);
            slot.Bind(filtered[i], IsCraftable(filtered[i], slots));
            slot.gameObject.SetActive(true);
        }

        for (int i = filtered.Count; i < slotPool.Count; i++)
            slotPool[i].gameObject.SetActive(false);

        // 탭/검색이 바뀌어 선택한 레시피가 목록에서 빠졌으면 선택을 해제한다.
        if (selectedRecipe != null && !filtered.Contains(selectedRecipe)) selectedRecipe = null;

        HighlightSelectedSlot();
        RefreshDetail();
    }

    /// <summary>목록 구성은 그대로 두고 재료 충족 여부(흐림)만 다시 계산한다.</summary>
    private void RefreshCraftable()
    {
        List<ItemStack> slots = Inventory.Instance != null ? Inventory.Instance.slots : null;
        for (int i = 0; i < filtered.Count && i < slotPool.Count; i++)
            slotPool[i].SetCraftable(IsCraftable(filtered[i], slots));
    }

    /// <summary>
    /// 목록에서 흐리게 보일지 판정한다.
    /// 도구 레시피는 재료가 인벤토리가 아니라 부품 칸에서 오므로 항상 또렷하게 둔다
    /// (무엇을 넣어야 하는지는 상세 패널과 조합 버튼이 알려 준다).
    /// </summary>
    private static bool IsCraftable(Recipe recipe, List<ItemStack> slots)
        => recipe is ToolRecipe || (slots != null && RecipeSolver.CanCraft(slots, recipe));

    private CraftRecipeSlot GetSlot(int index)
    {
        while (slotPool.Count <= index)
        {
            CraftRecipeSlot slot = Instantiate(slotTemplate, gridContent);
            slot.name = "RecipeSlot" + slotPool.Count;
            slot.OnClicked += SelectRecipe;
            slotPool.Add(slot);
        }
        return slotPool[index];
    }

    // ── 선택 & 상세 패널 ───────────────────────────────────────
    /// <summary>레시피를 선택해 상세 패널에 재료와 조합 버튼을 표시한다.</summary>
    private void SelectRecipe(Recipe recipe)
    {
        selectedRecipe = recipe;
        HighlightSelectedSlot();
        RefreshDetail();
    }

    private void HighlightSelectedSlot()
    {
        for (int i = 0; i < filtered.Count && i < slotPool.Count; i++)
            slotPool[i].SetSelected(filtered[i] == selectedRecipe);
    }

    /// <summary>선택한 레시피에 맞춰 상세 패널(재료 목록 · 부품 칸 · 조합 버튼)을 다시 그린다.</summary>
    private void RefreshDetail()
    {
        if (selectedRecipe is ToolRecipe toolRecipe) RefreshToolDetail(toolRecipe);
        else RefreshItemDetail();
    }

    // ── 도구 조립 상세 ─────────────────────────────────────────
    /// <summary>필요한 부품을 "막대 1개" 처럼 종류 단위로 알려 주고, 그 수만큼 부품 칸을 띄운다.</summary>
    private void RefreshToolDetail(ToolRecipe recipe)
    {
        ToolDefinition definition = recipe != null ? recipe.tool : null;

        int lines = 0;
        for (int i = 0; definition != null && i < definition.SlotCount; i++)
        {
            ToolPartSlot slot = definition.GetSlot(i);
            if (slot == null || slot.kind == null) continue;

            TMP_Text line = GetMaterialLine(lines);
            line.text = $"{slot.kind.DisplayName} 1개";
            line.color = enoughColor;
            line.gameObject.SetActive(true);
            lines++;
        }
        HideMaterialLinesFrom(lines);

        RefreshPartSlots(definition);
        UpdateToolCraftButton(recipe, definition);
    }

    /// <summary>
    /// 부품 칸을 기계의 입력 슬롯에 그대로 물린다(닫아도 남고 월드 세이브에 함께 저장된다).
    /// 필요 개수를 넘는 칸이라도 아이템이 남아 있으면 꺼내갈 수 있게 보여 준다 — 부품이 갇히지 않게.
    /// </summary>
    private void RefreshPartSlots(ToolDefinition definition)
    {
        if (partSlotsRoot == null || partSlotTemplate == null) return;

        // 인스턴스 없이 연 폴백 상태(레이아웃 확인용)에서는 부품 칸을 쓸 수 없다.
        if (machineInventory == null)
        {
            partSlotsRoot.gameObject.SetActive(false);
            return;
        }

        int capacity = machineInventory.InputCount;
        int required = definition != null ? definition.SlotCount : 0;
        if (required > capacity)
        {
            Debug.LogWarning($"[CraftingTableUI] '{definition.DisplayName}' 은 부품 칸 {required}개가 필요하지만 "
                + $"이 조합대의 입력 슬롯은 {capacity}개뿐입니다. 클램프합니다.", this);
            required = capacity;
        }

        int shown = required;
        for (int i = capacity - 1; i >= required; i--)
        {
            ItemStack leftover = machineInventory.GetStack(i);
            if (leftover != null && leftover.item != null && leftover.count > 0) { shown = i + 1; break; }
        }

        for (int i = 0; i < shown; i++)
        {
            ToolPartSlotUI slot = GetPartSlot(i);
            bool active = i < required;
            slot.SetRequirement(active ? definition : null, i);
            slot.SetInsertable(active);       // 남은 부품 칸은 꺼내기만 가능
            slot.gameObject.SetActive(true);
            slot.Bind(machineInventory, i);   // Bind 가 Refresh 까지 한다
        }

        for (int i = shown; i < partSlotPool.Count; i++)
            partSlotPool[i].gameObject.SetActive(false);

        partSlotsRoot.gameObject.SetActive(shown > 0);
    }

    private ToolPartSlotUI GetPartSlot(int index)
    {
        while (partSlotPool.Count <= index)
        {
            ToolPartSlotUI slot = Instantiate(partSlotTemplate, partSlotsRoot);
            slot.name = "PartSlot" + partSlotPool.Count;
            partSlotPool.Add(slot);
        }
        return partSlotPool[index];
    }

    private void UpdateToolCraftButton(ToolRecipe recipe, ToolDefinition definition)
    {
        if (craftButton == null) return;

        List<ItemStack> parts = machineInventory != null ? machineInventory.inputSlots : null;
        bool assembled = definition != null && ToolFactory.CanAssemble(definition, parts);

        List<ItemStack> inventorySlots = Inventory.Instance != null ? Inventory.Instance.slots : null;
        bool room = inventorySlots != null && RecipeSolver.CanStoreOutputs(inventorySlots, recipe);

        craftButton.interactable = assembled && room;

        if (craftButtonLabel == null) return;
        if (!assembled) craftButtonLabel.text = "부품을 넣으세요";
        else if (!room) craftButtonLabel.text = "인벤토리 가득 참";
        else craftButtonLabel.text = "조합";
    }

    // ── 일반 레시피 상세 ───────────────────────────────────────
    /// <summary>소모 재료 목록(재료명 : 보유량 / 필요량)과 조합 버튼 상태를 다시 그린다.</summary>
    private void RefreshItemDetail()
    {
        List<ItemStack> slots = Inventory.Instance != null ? Inventory.Instance.slots : null;

        // 도구 레시피가 아니면 부품 칸은 쓰지 않는다.
        if (partSlotsRoot != null) partSlotsRoot.gameObject.SetActive(false);

        int lines = 0;
        bool hasAll = true;

        if (selectedRecipe != null && selectedRecipe.inputs != null)
        {
            for (int i = 0; i < selectedRecipe.inputs.Count; i++)
            {
                ItemStack need = selectedRecipe.inputs[i];
                if (need == null || need.item == null || need.count <= 0) continue;

                int owned = slots != null ? RecipeSolver.CountItem(slots, need.item) : 0;
                bool enough = owned >= need.count;
                if (!enough) hasAll = false;

                TMP_Text line = GetMaterialLine(lines);
                line.text = $"{need.item.DisplayName} : {owned} / {need.count}";
                line.color = enough ? enoughColor : shortColor;
                line.gameObject.SetActive(true);
                lines++;
            }
        }

        lines = AppendToolLines(selectedRecipe, slots, lines, out bool hasTools);

        HideMaterialLinesFrom(lines);
        UpdateCraftButton(hasAll, hasTools, slots);
    }

    /// <summary>필요 도구 줄을 덧붙인다("필요 도구 : 곡괭이 (내구도 1)"). 다음에 쓸 줄 번호를 반환.</summary>
    private int AppendToolLines(Recipe recipe, List<ItemStack> slots, int lines, out bool hasTools)
    {
        hasTools = true;
        if (recipe == null || recipe.requiredTools == null) return lines;

        for (int i = 0; i < recipe.requiredTools.Count; i++)
        {
            ToolRequirement requirement = recipe.requiredTools[i];
            if (requirement == null || requirement.tool == null) continue;

            bool owned = slots != null && RecipeSolver.HasTool(slots, requirement);
            if (!owned) hasTools = false;

            TMP_Text line = GetMaterialLine(lines);
            line.text = $"필요 도구 : {requirement.tool.DisplayName} (내구도 {requirement.durabilityCost})";
            line.color = owned ? enoughColor : shortColor;
            line.gameObject.SetActive(true);
            lines++;
        }
        return lines;
    }

    private void HideMaterialLinesFrom(int index)
    {
        for (int i = index; i < materialPool.Count; i++)
            materialPool[i].gameObject.SetActive(false);
    }

    private void UpdateCraftButton(bool hasAllMaterials, bool hasTools, List<ItemStack> slots)
    {
        if (craftButton == null) return;

        bool hasSelection = selectedRecipe != null;
        bool roomForOutput = hasSelection && slots != null
            && RecipeSolver.CanStoreOutputs(slots, selectedRecipe);

        craftButton.interactable = hasSelection && hasAllMaterials && hasTools && roomForOutput;

        if (craftButtonLabel == null) return;
        if (!hasSelection) craftButtonLabel.text = "레시피를 선택하세요";
        else if (!hasAllMaterials) craftButtonLabel.text = "재료 부족";
        else if (!hasTools) craftButtonLabel.text = "도구 없음";
        else if (!roomForOutput) craftButtonLabel.text = "인벤토리 가득 참";
        else craftButtonLabel.text = "조합";
    }

    private TMP_Text GetMaterialLine(int index)
    {
        while (materialPool.Count <= index)
        {
            TMP_Text line = Instantiate(materialLineTemplate, materialList);
            line.name = "Material" + materialPool.Count;
            materialPool.Add(line);
        }
        return materialPool[index];
    }

    private string SearchText => searchField != null ? searchField.text : null;

    // ── 제작 ──────────────────────────────────────────────────
    /// <summary>조합 버튼이 눌렸을 때. 선택된 레시피를 만든다.</summary>
    private void CraftSelected()
    {
        if (selectedRecipe is ToolRecipe toolRecipe) CraftTool(toolRecipe);
        else Craft(selectedRecipe);
    }

    /// <summary>부품 칸의 부품으로 커스텀 도구를 조립해 플레이어 인벤토리에 넣는다.</summary>
    private void CraftTool(ToolRecipe recipe)
    {
        ToolDefinition definition = recipe != null ? recipe.tool : null;
        ToolItem output = recipe != null ? recipe.ToolOutput : null;
        Inventory inventory = Inventory.Instance;

        if (definition == null || output == null || machineInventory == null
            || inventory == null || inventory.slots == null) return;

        List<ItemStack> parts = machineInventory.inputSlots;
        if (!ToolFactory.CanAssemble(definition, parts)) return;

        ToolInstance made = ToolFactory.Create(definition, parts);
        if (made == null) return;

        // 결과를 먼저 넣어 본다. 자리가 없으면 부품을 소모하지 않는다.
        if (!RecipeSolver.TryAdd(inventory.slots, output, 1, made))
        {
            Debug.LogWarning("[CraftingTableUI] 인벤토리에 자리가 없어 도구를 만들지 못했습니다.", this);
            return;
        }

        for (int i = 0; i < definition.SlotCount; i++)
        {
            ItemStack part = parts[i];
            part.count--;
            if (part.count <= 0) part.Clear();
        }

        inventory.NotifyChanged();
        machineInventory.NotifyChanged();   // → HandlePartsChanged → 부품 칸 · 버튼 갱신
        if (instance != null) instance.Flush();
    }

    /// <summary>인벤토리 재료를 소모해 결과물을 지급한다.</summary>
    public void Craft(Recipe recipe)
    {
        Inventory inventory = Inventory.Instance;
        if (recipe == null || inventory == null || inventory.slots == null) return;

        if (!RecipeSolver.CanCraft(inventory.slots, recipe)) return;

        // 적재 가능 여부를 소모 "전에" 검사한다. 재료를 빼면 자리가 생기는 경계 상황에서
        // 보수적으로 거절할 수 있지만, 결과물이 사라지는 것보다 안전하다.
        if (!RecipeSolver.CanStoreOutputs(inventory.slots, recipe))
        {
            Debug.LogWarning("[CraftingTableUI] 인벤토리에 자리가 없어 제작하지 못했습니다.", this);
            return;
        }

        RecipeSolver.ConsumeInputs(inventory.slots, recipe);
        RecipeSolver.ConsumeTools(inventory.slots, recipe);   // 도구는 내구도만 닳는다
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
