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

    /// <summary>
    /// 조합대 프리팹에 입출력·연료 요소가 없는 것은 정상이다 — 재료는 플레이어 인벤토리에서 먹고
    /// 부품 칸은 이 클래스가 직접 만든다. 그래서 개수 부족 경고를 끈다.
    /// (업그레이드 칸과 코어 업그레이드 버튼은 <b>프리팹 요소</b>이고, 베이스가 조용히 클램프한다.)
    /// </summary>
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
        BindCoreUpgrade(machine);
        BuildTabs();
        RebuildGrid();
        Subscribe();
    }

    // ── 코어 업그레이드 (조합대 티어 상승) ──────────────────────
    private bool warnedMissingCoreUpgrade;

    /// <summary>
    /// 코어 업그레이드 버튼을 이 기계에 맞게 붙인다.
    ///
    /// <b>칸도 버튼도 프리팹에 있는 요소다</b>(<see cref="MachineUIRole.UpgradeSlot"/> ·
    /// <see cref="MachineUIRole.CoreUpgradeButton"/>). 예전에는 코드로 만들었는데, 그러면
    /// 위치·크기를 씬에서 못 옮기고 팩토리 검증기도 볼 수 없었다.
    ///
    /// 이 패널 하나를 코어·고급 조합기와 재단 3종이 함께 쓰지만, 칸은 베이스가
    /// <c>upgradeSlotCount</c>(재단은 0)로 꺼 주고 버튼은 여기서 코어일 때만 켠다.
    /// </summary>
    private void BindCoreUpgrade(MachineInstance machine)
    {
        bool accepts = machine != null && machine.AcceptsTierUpgrade && machine.UpgradeCount > 0;

        Button coreUpgradeButton = CoreUpgradeButton;
        if (coreUpgradeButton == null)
        {
            // 코어가 아닌 조합대에서도 매번 경고하면 로그가 묻힌다 — 필요한 기계에서 한 번만 알린다.
            if (accepts && !warnedMissingCoreUpgrade)
            {
                warnedMissingCoreUpgrade = true;
                Debug.LogWarning($"[CraftingTableUI] '{name}' 에 CoreUpgradeButton 요소가 없어 " +
                                 "코어 티어를 올릴 수 없습니다. Machine UI Factory 에서 추가하세요.", this);
            }
            return;
        }

        if (CoreUpgradeButtonObject != null) CoreUpgradeButtonObject.SetActive(accepts);

        // 리스너를 지우지 않으면 조합대를 열 때마다 쌓여 한 번 눌렀는데 여러 번 올라간다.
        coreUpgradeButton.onClick.RemoveAllListeners();
        if (!accepts) return;

        // 칸 자체는 베이스가 이미 업그레이드 구간에 바인딩했다(평면 인덱스 [입력][출력][연료][업그레이드]).
        if (FirstUpgradeSlot == null && !warnedMissingCoreUpgrade)
        {
            warnedMissingCoreUpgrade = true;
            Debug.LogWarning($"[CraftingTableUI] '{name}' 에 UpgradeSlot 요소가 없어 " +
                             "재료를 넣을 칸이 없습니다. Machine UI Factory 에서 추가하세요.", this);
        }

        MachineInstance captured = machine;
        coreUpgradeButton.onClick.AddListener(() =>
        {
            if (!captured.TryUpgradeTier()) return;
            tier = captured.Tier;
            currentCategory = null;
            selectedRecipe = null;
            BuildTabs();      // 새로 열린 티어의 탭·레시피가 즉시 보여야 한다
            RebuildGrid();
            RefreshCoreUpgrade();
        });
        RefreshCoreUpgrade();
    }

    /// <summary>버튼 라벨로 지금 무엇이 필요한지 알려 준다(현재 티어 · 넣은 재료가 유효한지).</summary>
    private void RefreshCoreUpgrade()
    {
        TMP_Text coreUpgradeLabel = CoreUpgradeLabel;
        if (coreUpgradeLabel == null || CoreUpgradeButton == null || instance == null) return;

        // 슬롯이 아니라 <b>기계 인벤토리</b>에서 읽는다 — 정본은 저장소 쪽이고 슬롯은 그것을 비출 뿐이다.
        Items held = null;
        if (machineInventory != null && machineInventory.UpgradeCount > 0)
        {
            ItemStack stack = machineInventory.upgradeSlots[0];
            if (stack != null && stack.count > 0) held = stack.item;
        }
        int target = CoreUpgradeTable.TargetTier(held);

        if (held == null) coreUpgradeLabel.text = $"코어 {instance.Tier}티어 · 재료를 넣으세요";
        else if (target < 0) coreUpgradeLabel.text = "업그레이드 재료가 아닙니다";
        else if (target <= instance.Tier) coreUpgradeLabel.text = $"이미 {instance.Tier}티어입니다";
        else coreUpgradeLabel.text = $"{target}티어로 업그레이드";

        CoreUpgradeButton.interactable = target > instance.Tier;
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
        RefreshCoreUpgrade();   // 업그레이드 칸도 같은 저장소를 쓴다
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
    /// <summary>
    /// 목록에서 "만들 수 있음" 으로 밝게 보일지. 도구 조립과 부품 제작은 재료가 <b>인벤토리가 아니라
    /// 조합대 칸</b>에 들어가므로 여기서 판단하지 않고 늘 밝게 둔다(상세 패널이 부족분을 알려 준다).
    /// </summary>
    private static bool IsCraftable(Recipe recipe, List<ItemStack> slots)
        => recipe is ToolRecipe || recipe is ToolPartRecipe
           || (slots != null && RecipeSolver.CanCraft(slots, recipe));

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
        else if (selectedRecipe is ToolPartRecipe partRecipe) RefreshPartDetail(partRecipe);
        else RefreshItemDetail();
    }

    // ── 부품 제작 상세 (재질 칸) ────────────────────────────────
    /// <summary>
    /// 부품은 재료가 <b>고정 아이템이 아니라 재질</b>이라, 재료 목록 대신 재질 칸 하나를 띄운다.
    /// 무엇이 나오는지는 넣은 재료가 정하므로(돌 → 돌 곡괭이 머리) 지금 든 것을 보고 알려 준다.
    /// </summary>
    private void RefreshPartDetail(ToolPartRecipe recipe)
    {
        ItemStack held = machineInventory != null && machineInventory.InputCount > 0
            ? machineInventory.GetStack(0) : null;
        ToolMaterial material = held != null ? recipe.MaterialOf(held.item) : null;
        ToolPartItem result = material != null && ToolDictionary.Instance != null
            ? ToolDictionary.Instance.GetPart(recipe.kind, material) : null;

        int lines = 0;
        TMP_Text line = GetMaterialLine(lines++);
        if (material == null)
            { line.text = "재료를 재질 칸에 넣으세요"; line.color = shortColor; }
        else
        {
            bool enough = held.count >= recipe.materialCost;
            line.text = $"{held.item.DisplayName} : {held.count} / {recipe.materialCost}";
            line.color = enough ? enoughColor : shortColor;
        }
        line.gameObject.SetActive(true);

        if (result != null)
        {
            TMP_Text made = GetMaterialLine(lines++);
            made.text = $"→ {result.DisplayName}";
            made.color = enoughColor;
            made.gameObject.SetActive(true);
        }
        HideMaterialLinesFrom(lines);

        RefreshMaterialSlot(recipe);
        UpdatePartCraftButton(recipe, held, material, result);
    }

    /// <summary>재질 칸 하나만 띄운다(부품 칸 풀을 그대로 쓴다 — 같은 입력 슬롯 0번을 본다).</summary>
    private void RefreshMaterialSlot(ToolPartRecipe recipe)
    {
        if (partSlotsRoot == null || partSlotTemplate == null) return;
        if (machineInventory == null || machineInventory.InputCount == 0)
        {
            partSlotsRoot.gameObject.SetActive(false);
            return;
        }

        ToolPartSlotUI slot = GetPartSlot(0);
        slot.SetMaterialRequirement(recipe);
        slot.SetInsertable(true);
        slot.gameObject.SetActive(true);
        slot.Bind(machineInventory, 0);

        // 나머지 칸은 남은 것을 꺼내갈 수 있게만 둔다 — 도구 조립에서 넣어 둔 부품이 갇히지 않게.
        for (int i = 1; i < partSlotPool.Count; i++)
        {
            ItemStack leftover = i < machineInventory.InputCount ? machineInventory.GetStack(i) : null;
            bool keep = leftover != null && leftover.item != null && leftover.count > 0;
            if (keep)
            {
                partSlotPool[i].SetRequirement(null, i);
                partSlotPool[i].SetInsertable(false);
                partSlotPool[i].Bind(machineInventory, i);
            }
            partSlotPool[i].gameObject.SetActive(keep);
        }
        partSlotsRoot.gameObject.SetActive(true);
    }

    private void UpdatePartCraftButton(ToolPartRecipe recipe, ItemStack held, ToolMaterial material, ToolPartItem result)
    {
        if (craftButton == null) return;

        bool enough = material != null && held != null && held.count >= recipe.materialCost;
        List<ItemStack> inventorySlots = Inventory.Instance != null ? Inventory.Instance.slots : null;
        bool room = result != null && inventorySlots != null
                    && RecipeSolver.CountFreeSpace(inventorySlots, result) > 0;

        craftButton.interactable = enough && room;

        if (craftButtonLabel == null) return;
        if (material == null) craftButtonLabel.text = "재료를 넣으세요";
        else if (result == null) craftButtonLabel.text = "이 재질의 부품이 없습니다";
        else if (!enough) craftButtonLabel.text = "재료가 모자랍니다";
        else if (!room) craftButtonLabel.text = "인벤토리 가득 참";
        else craftButtonLabel.text = "조합";
    }

    /// <summary>재질 칸의 재료를 소모하고 그 재질의 부품을 인벤토리에 넣는다.</summary>
    private void CraftToolPart(ToolPartRecipe recipe)
    {
        if (machineInventory == null || machineInventory.InputCount == 0) return;

        ItemStack held = machineInventory.GetStack(0);
        ToolMaterial material = held != null ? recipe.MaterialOf(held.item) : null;
        if (material == null || held.count < recipe.materialCost) return;

        ToolPartItem result = ToolDictionary.Instance != null
            ? ToolDictionary.Instance.GetPart(recipe.kind, material) : null;
        if (result == null) return;

        Inventory inventory = Inventory.Instance;
        if (inventory == null || inventory.AddPartial(result, 1) <= 0) return;   // 못 넣으면 재료도 안 먹는다

        held.count -= recipe.materialCost;
        if (held.count <= 0) held.Clear();
        machineInventory.NotifyChanged();
        if (instance != null) instance.Flush();
        RefreshDetail();
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
        else if (selectedRecipe is ToolPartRecipe partRecipe) CraftToolPart(partRecipe);
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
