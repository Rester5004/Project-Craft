using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectCraft.UIFactory.EditorTools
{
    /// <summary>
    /// 조합대 UI 프리팹을 한 번에 조립하는 도구.
    /// 탭·검색창·스크롤 그리드는 런타임에 내용이 채워지는 구조라 자유 배치 모델(Machine UI Factory)과
    /// 성격이 달라, 뼈대를 만들어 주고 이후 위치·크기만 씬에서 손보게 한다.
    ///
    /// 자동화 중 멈추지 않도록 대화상자를 띄우지 않는다(MachineUIMigration 과 같은 규약).
    /// </summary>
    public static class CraftingTableUIFactory
    {
        private const float PanelWidth = 1960f;
        private const float PanelHeight = 853f;
        private const float PanelPosY = 300f;
        private const float Margin = 40f;
        private const float HeaderHeight = 64f;
        private const float TabsHeight = 80f;

        /// <summary>검색창 아래 상세 패널의 폭. 검색창과 같게 두어 세로로 정렬된다.</summary>
        private const float DetailWidth = 700f;
        private const float CraftButtonHeight = 86f;

        /// <summary>도구 부품 칸이 놓이는 가로 줄의 높이(조합 버튼 바로 위).</summary>
        private const float PartSlotsHeight = 140f;

        /// <summary>레시피 목록 격자의 칸 사이 간격.</summary>
        private const float RecipeGridSpacing = 24f;

        [MenuItem("Tools/Project Craft/Machine UI/Create Crafting Table UI")]
        public static void CreateMenu() => Create(true);

        /// <summary>조합대 UI 프리팹(과 필요한 슬롯 프리팹)을 생성한다. 성공하면 프리팹 경로를 반환.</summary>
        public static string Create(bool ping)
        {
            GameObject slotPrefab = EnsureCraftRecipeSlotPrefab();
            if (slotPrefab == null)
            {
                Debug.LogError("[CraftingTableUIFactory] CraftRecipeSlot 프리팹을 만들지 못했습니다.");
                return null;
            }

            GameObject partSlotPrefab = EnsureToolPartSlotPrefab();
            if (partSlotPrefab == null)
            {
                Debug.LogError("[CraftingTableUIFactory] ToolPartSlot 프리팹을 만들지 못했습니다.");
                return null;
            }

            TMP_FontAsset font = ResolveFont();
            Sprite background = ResolveBackgroundSprite();
            Vector2 cellSize = ResolveCellSize(slotPrefab);

            GameObject root = NewRect("CraftingTable_UI", null);
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            rootRect.anchoredPosition = new Vector2(0f, PanelPosY);

            Image backgroundImage = root.AddComponent<Image>();
            backgroundImage.sprite = background;
            backgroundImage.type = Image.Type.Sliced;
            if (background == null) backgroundImage.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);

            // ── 제목 ──
            GameObject titleGO = NewRect("Title", rootRect);
            RectTransform titleRect = (RectTransform)titleGO.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(Margin, -Margin * 0.6f);
            titleRect.sizeDelta = new Vector2(800f, HeaderHeight);
            TextMeshProUGUI title = titleGO.AddComponent<TextMeshProUGUI>();
            title.font = font;
            title.fontSize = 44f;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.raycastTarget = false;
            title.text = "조합대";

            // ── 검색창 ──
            GameObject searchGO = NewRect("SearchField", rootRect);
            RectTransform searchRect = (RectTransform)searchGO.transform;
            searchRect.anchorMin = new Vector2(1f, 1f);
            searchRect.anchorMax = new Vector2(1f, 1f);
            searchRect.pivot = new Vector2(1f, 1f);
            searchRect.anchoredPosition = new Vector2(-Margin, -Margin * 0.6f);
            searchRect.sizeDelta = new Vector2(700f, HeaderHeight);
            Image searchBackground = searchGO.AddComponent<Image>();
            searchBackground.color = new Color(0f, 0f, 0f, 0.6f);

            GameObject viewportGO = NewRect("Text Area", searchRect);
            RectTransform searchViewport = Stretch(viewportGO, 16f, 8f);
            viewportGO.AddComponent<RectMask2D>();

            TextMeshProUGUI placeholder = NewText("Placeholder", searchViewport, font, 30f);
            placeholder.text = "검색...";
            placeholder.color = new Color(1f, 1f, 1f, 0.4f);
            TextMeshProUGUI searchText = NewText("Text", searchViewport, font, 30f);
            searchText.color = Color.white;

            TMP_InputField searchField = searchGO.AddComponent<TMP_InputField>();
            searchField.textViewport = searchViewport;
            searchField.textComponent = searchText;
            searchField.placeholder = placeholder;
            searchField.fontAsset = font;
            searchField.pointSize = 30f;
            searchField.lineType = TMP_InputField.LineType.SingleLine;
            searchField.restoreOriginalTextOnEscape = false;

            // ── 탭 ──
            GameObject tabsGO = NewRect("Tabs", rootRect);
            RectTransform tabsRect = (RectTransform)tabsGO.transform;
            tabsRect.anchorMin = new Vector2(0f, 1f);
            tabsRect.anchorMax = new Vector2(1f, 1f);
            tabsRect.pivot = new Vector2(0.5f, 1f);
            tabsRect.offsetMin = new Vector2(Margin, 0f);
            tabsRect.offsetMax = new Vector2(-Margin, 0f);
            tabsRect.sizeDelta = new Vector2(tabsRect.sizeDelta.x, TabsHeight);
            tabsRect.anchoredPosition = new Vector2(0f, -(Margin * 0.6f + HeaderHeight + 20f));
            HorizontalLayoutGroup tabsLayout = tabsGO.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 12f;
            tabsLayout.childForceExpandWidth = false;
            tabsLayout.childForceExpandHeight = true;
            tabsLayout.childControlWidth = false;
            tabsLayout.childControlHeight = true;
            tabsLayout.childAlignment = TextAnchor.MiddleLeft;

            GameObject tabGO = NewRect("TabTemplate", tabsRect);
            RectTransform tabRect = (RectTransform)tabGO.transform;
            tabRect.sizeDelta = new Vector2(240f, TabsHeight);
            Image tabImage = tabGO.AddComponent<Image>();   // 배경(아이콘과 분리해야 아이콘이 늘어나지 않는다)
            tabImage.color = new Color(1f, 1f, 1f, 0.45f);
            if (background != null) { tabImage.sprite = background; tabImage.type = Image.Type.Sliced; }
            Button tabButton = tabGO.AddComponent<Button>();
            tabButton.targetGraphic = tabImage;
            LayoutElement tabLayout = tabGO.AddComponent<LayoutElement>();
            tabLayout.minWidth = 160f;
            tabLayout.preferredWidth = 240f;

            // 아이콘 + 이름을 가로로 배치. 아이콘 없는 카테고리는 아이콘 오브젝트가 꺼져 이름이 전체를 쓴다.
            HorizontalLayoutGroup tabContent = tabGO.AddComponent<HorizontalLayoutGroup>();
            tabContent.padding = new RectOffset(14, 14, 10, 10);
            tabContent.spacing = 10f;
            tabContent.childForceExpandWidth = false;
            tabContent.childForceExpandHeight = true;
            tabContent.childControlWidth = true;
            tabContent.childControlHeight = true;
            tabContent.childAlignment = TextAnchor.MiddleCenter;

            GameObject tabIconGO = NewRect("Icon", tabRect);
            Image tabIcon = tabIconGO.AddComponent<Image>();
            tabIcon.preserveAspect = true;                 // 원본 비율 유지
            tabIcon.raycastTarget = false;
            LayoutElement tabIconLayout = tabIconGO.AddComponent<LayoutElement>();
            tabIconLayout.preferredWidth = TabsHeight - 20f;
            tabIconLayout.preferredHeight = TabsHeight - 20f;
            tabIconLayout.flexibleWidth = 0f;

            GameObject tabLabelGO = NewRect("Label", tabRect);
            TextMeshProUGUI tabLabel = tabLabelGO.AddComponent<TextMeshProUGUI>();
            tabLabel.font = font;
            tabLabel.fontSize = 30f;
            tabLabel.alignment = TextAlignmentOptions.Center;
            tabLabel.textWrappingMode = TextWrappingModes.NoWrap;
            tabLabel.richText = false;
            tabLabel.raycastTarget = false;
            tabLabel.text = "탭";
            LayoutElement tabLabelLayout = tabLabelGO.AddComponent<LayoutElement>();
            tabLabelLayout.flexibleWidth = 1f;

            CraftingTableTab tabTemplate = tabGO.AddComponent<CraftingTableTab>();
            SerializedObject tabSo = new SerializedObject(tabTemplate);
            SetRef(tabSo, "background", tabImage);
            SetRef(tabSo, "icon", tabIcon);
            SetRef(tabSo, "label", tabLabel);
            tabSo.ApplyModifiedPropertiesWithoutUndo();

            tabGO.SetActive(false);   // 템플릿은 비활성

            // ── 레시피 스크롤 그리드 ──
            GameObject scrollGO = NewRect("RecipeScroll", rootRect);
            RectTransform scrollRect = (RectTransform)scrollGO.transform;
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.pivot = new Vector2(0.5f, 0.5f);
            float bodyTop = Margin * 0.6f + HeaderHeight + 20f + TabsHeight + 16f;
            scrollRect.offsetMin = new Vector2(Margin, Margin);
            // 오른쪽에 상세 패널 자리를 비워 둔다(검색창 폭과 맞춰 세로로 정렬되게).
            scrollRect.offsetMax = new Vector2(-(Margin + DetailWidth + 16f), -bodyTop);

            GameObject gridViewportGO = NewRect("Viewport", scrollRect);
            RectTransform gridViewport = Stretch(gridViewportGO, 0f, 0f);
            gridViewportGO.AddComponent<RectMask2D>();

            GameObject contentGO = NewRect("Content", gridViewport);
            RectTransform content = (RectTransform)contentGO.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);
            GridLayoutGroup grid = contentGO.AddComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = new Vector2(RecipeGridSpacing, RecipeGridSpacing);
            grid.constraint = GridLayoutGroup.Constraint.Flexible;
            grid.childAlignment = TextAnchor.UpperLeft;
            ContentSizeFitter fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.viewport = gridViewport;
            scroll.content = content;
            scroll.scrollSensitivity = 40f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            GameObject slotTemplateGO = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, content);
            slotTemplateGO.name = "SlotTemplate";
            slotTemplateGO.SetActive(false);   // 템플릿은 비활성
            CraftRecipeSlot slotTemplate = slotTemplateGO.GetComponent<CraftRecipeSlot>();

            // ── 상세 패널 (검색창 아래: 위 소모 재료, 아래 조합 버튼) ──
            GameObject detailGO = NewRect("DetailPanel", rootRect);
            RectTransform detailRect = (RectTransform)detailGO.transform;
            detailRect.anchorMin = new Vector2(1f, 0f);
            detailRect.anchorMax = new Vector2(1f, 1f);
            detailRect.pivot = new Vector2(1f, 0.5f);
            detailRect.offsetMin = new Vector2(-(Margin + DetailWidth), Margin);
            detailRect.offsetMax = new Vector2(-Margin, -bodyTop);
            Image detailBg = detailGO.AddComponent<Image>();
            detailBg.color = new Color(0f, 0f, 0f, 0.35f);

            // 재료 목록: 위에서부터 쌓인다
            GameObject listGO = NewRect("MaterialList", detailRect);
            RectTransform listRect = (RectTransform)listGO.transform;
            listRect.anchorMin = new Vector2(0f, 0f);
            listRect.anchorMax = new Vector2(1f, 1f);
            // 아래쪽에 부품 칸 줄 + 조합 버튼 자리를 비워 둔다
            listRect.offsetMin = new Vector2(20f, CraftButtonHeight + PartSlotsHeight + 44f);
            listRect.offsetMax = new Vector2(-20f, -20f);
            VerticalLayoutGroup listLayout = listGO.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 8f;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childAlignment = TextAnchor.UpperLeft;

            GameObject lineGO = NewRect("MaterialLineTemplate", listRect);
            TextMeshProUGUI lineText = lineGO.AddComponent<TextMeshProUGUI>();
            lineText.font = font;
            lineText.fontSize = 30f;
            lineText.alignment = TextAlignmentOptions.MidlineLeft;
            lineText.textWrappingMode = TextWrappingModes.NoWrap;
            lineText.richText = false;
            lineText.raycastTarget = false;
            lineText.text = "재료 : 0 / 0";
            LayoutElement lineLayout = lineGO.AddComponent<LayoutElement>();
            lineLayout.minHeight = 38f;
            lineLayout.preferredHeight = 38f;
            lineGO.SetActive(false);   // 템플릿은 비활성

            // 도구 부품 칸 줄: 재료 목록과 조합 버튼 사이. 도구 레시피를 고를 때만 켜진다.
            GameObject partsGO = NewRect("PartSlots", detailRect);
            RectTransform partsRect = (RectTransform)partsGO.transform;
            partsRect.anchorMin = new Vector2(0f, 0f);
            partsRect.anchorMax = new Vector2(1f, 0f);
            partsRect.pivot = new Vector2(0.5f, 0f);
            partsRect.offsetMin = new Vector2(20f, CraftButtonHeight + 32f);
            partsRect.offsetMax = new Vector2(-20f, CraftButtonHeight + 32f + PartSlotsHeight);
            HorizontalLayoutGroup partsLayout = partsGO.AddComponent<HorizontalLayoutGroup>();
            partsLayout.spacing = 16f;
            partsLayout.childForceExpandWidth = false;
            partsLayout.childForceExpandHeight = false;
            partsLayout.childControlWidth = false;
            partsLayout.childControlHeight = false;
            partsLayout.childAlignment = TextAnchor.MiddleLeft;

            GameObject partTemplateGO = (GameObject)PrefabUtility.InstantiatePrefab(partSlotPrefab, partsRect);
            partTemplateGO.name = "PartSlotTemplate";
            partTemplateGO.SetActive(false);   // 템플릿은 비활성
            ToolPartSlotUI partSlotTemplate = partTemplateGO.GetComponent<ToolPartSlotUI>();
            partsGO.SetActive(false);          // 도구 레시피를 고르기 전에는 줄 자체가 숨어 있다

            // 조합 버튼: 패널 아래쪽 고정
            GameObject craftGO = NewRect("CraftButton", detailRect);
            RectTransform craftRect = (RectTransform)craftGO.transform;
            craftRect.anchorMin = new Vector2(0f, 0f);
            craftRect.anchorMax = new Vector2(1f, 0f);
            craftRect.pivot = new Vector2(0.5f, 0f);
            craftRect.offsetMin = new Vector2(20f, 20f);
            craftRect.offsetMax = new Vector2(-20f, 20f + CraftButtonHeight);
            Image craftImage = craftGO.AddComponent<Image>();
            craftImage.color = new Color(1f, 1f, 1f, 0.85f);
            if (background != null) { craftImage.sprite = background; craftImage.type = Image.Type.Sliced; }
            Button craftButton = craftGO.AddComponent<Button>();
            craftButton.targetGraphic = craftImage;

            // 비활성일 때 확실히 구분되도록 색 전이를 지정한다.
            ColorBlock colors = craftButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            craftButton.colors = colors;

            GameObject craftLabelGO = NewRect("Label", craftRect);
            Stretch(craftLabelGO, 0f, 0f);
            TextMeshProUGUI craftLabel = craftLabelGO.AddComponent<TextMeshProUGUI>();
            craftLabel.font = font;
            craftLabel.fontSize = 34f;
            craftLabel.color = new Color(0.08f, 0.09f, 0.12f, 1f);
            craftLabel.alignment = TextAlignmentOptions.Center;
            craftLabel.textWrappingMode = TextWrappingModes.NoWrap;
            craftLabel.richText = false;
            craftLabel.raycastTarget = false;
            craftLabel.text = "조합";

            // ── 컴포넌트 배선 ──
            CraftingTableUI ui = root.AddComponent<CraftingTableUI>();
            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("materialList").objectReferenceValue = listRect;
            so.FindProperty("materialLineTemplate").objectReferenceValue = lineText;
            so.FindProperty("craftButton").objectReferenceValue = craftButton;
            so.FindProperty("craftButtonLabel").objectReferenceValue = craftLabel;
            so.FindProperty("partSlotsRoot").objectReferenceValue = partsRect;
            so.FindProperty("partSlotTemplate").objectReferenceValue = partSlotTemplate;
            so.FindProperty("searchField").objectReferenceValue = searchField;
            so.FindProperty("tabsRoot").objectReferenceValue = tabsRect;
            so.FindProperty("tabTemplate").objectReferenceValue = tabTemplate;
            so.FindProperty("gridContent").objectReferenceValue = content;
            so.FindProperty("slotTemplate").objectReferenceValue = slotTemplate;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(MachineUIFactoryPaths.OutputFolder);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, MachineUIFactoryPaths.CraftingTableUIPrefab);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (saved == null)
            {
                Debug.LogError("[CraftingTableUIFactory] 프리팹 저장에 실패했습니다.");
                return null;
            }

            Debug.Log("[CraftingTableUIFactory] 생성 완료: " + MachineUIFactoryPaths.CraftingTableUIPrefab, saved);
            if (ping) EditorGUIUtility.PingObject(saved);
            return MachineUIFactoryPaths.CraftingTableUIPrefab;
        }

        /// <summary>
        /// MachineSlot 프리팹의 아트를 그대로 물려받은 CraftRecipeSlot 프리팹을 만든다.
        /// 드래그용 InventorySlot 과 역할 태그 MachineUIElement 는 제거한다.
        /// </summary>
        private static GameObject EnsureCraftRecipeSlotPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(MachineUIFactoryPaths.CraftRecipeSlotPrefab);
            if (existing != null && existing.GetComponent<CraftRecipeSlot>() != null) return existing;

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(MachineUIFactoryPaths.SlotPrefab);
            if (source == null)
            {
                Debug.LogError("[CraftingTableUIFactory] 원본 슬롯 프리팹이 없습니다: " + MachineUIFactoryPaths.SlotPrefab);
                return null;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(MachineUIFactoryPaths.SlotPrefab);
            try
            {
                // 제거 전에 기존 슬롯의 참조를 가져온다(아이콘/개수/선택 스프라이트).
                ItemSlot itemSlot = contents.GetComponent<ItemSlot>();
                Object icon = null, count = null, selectedSprite = null;
                if (itemSlot != null)
                {
                    SerializedObject src = new SerializedObject(itemSlot);
                    icon = GetRef(src, "iconImage");
                    count = GetRef(src, "countText");
                    selectedSprite = GetRef(src, "selectedSlotSprite");
                    Object.DestroyImmediate(itemSlot, true);
                }

                MachineUIElement element = contents.GetComponent<MachineUIElement>();
                if (element != null) Object.DestroyImmediate(element, true);

                // 참조를 못 얻었으면 이름으로 찾는다(MachineSlot 은 icon/count 자식을 가진다).
                if (icon == null) icon = FindComponentByName<Image>(contents.transform, "icon");
                if (count == null) count = FindComponentByName<TMP_Text>(contents.transform, "count");

                CraftRecipeSlot slot = contents.AddComponent<CraftRecipeSlot>();
                SerializedObject dst = new SerializedObject(slot);
                SetRef(dst, "iconImage", icon);
                SetRef(dst, "countText", count);
                SetRef(dst, "slotImage", contents.GetComponent<Image>());
                SetRef(dst, "selectedSlotSprite", selectedSprite);
                dst.ApplyModifiedPropertiesWithoutUndo();

                EnsureFolder(MachineUIFactoryPaths.BuildingBlockFolder);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(contents, MachineUIFactoryPaths.CraftRecipeSlotPrefab);
                return saved;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// 이미 만들어 둔 조합대 UI 프리팹에 <b>도구 부품 칸 줄만 덧붙인다</b>.
        /// 통째로 다시 만들면 손으로 맞춘 위치·크기가 날아가고 CoreCrafter 의 uiPrefab 참조도 흔들리므로,
        /// 기존 프리팹을 열어 없는 것만 추가하는 쪽을 쓴다. 재실행해도 안전하다.
        /// </summary>
        [MenuItem("Tools/Project Craft/Machine UI/Add Tool Part Slots To Crafting Table UI")]
        public static void AddPartSlots()
        {
            GameObject partSlotPrefab = EnsureToolPartSlotPrefab();
            if (partSlotPrefab == null)
            {
                Debug.LogError("[CraftingTableUIFactory] ToolPartSlot 프리팹을 만들지 못했습니다.");
                return;
            }

            string path = MachineUIFactoryPaths.CraftingTableUIPrefab;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogError("[CraftingTableUIFactory] 조합대 UI 프리팹이 없습니다. 먼저 Create Crafting Table UI 를 실행하세요: " + path);
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                CraftingTableUI ui = contents.GetComponent<CraftingTableUI>();
                Transform detail = contents.transform.Find("DetailPanel");
                if (ui == null || detail == null)
                {
                    Debug.LogError("[CraftingTableUIFactory] 프리팹 구조가 예상과 다릅니다(CraftingTableUI / DetailPanel 없음).");
                    return;
                }

                RectTransform detailRect = (RectTransform)detail;
                Transform existing = detail.Find("PartSlots");
                RectTransform partsRect;

                if (existing != null)
                {
                    partsRect = (RectTransform)existing;
                }
                else
                {
                    GameObject partsGO = NewRect("PartSlots", detailRect);
                    partsRect = (RectTransform)partsGO.transform;
                    partsRect.anchorMin = new Vector2(0f, 0f);
                    partsRect.anchorMax = new Vector2(1f, 0f);
                    partsRect.pivot = new Vector2(0.5f, 0f);
                    partsRect.offsetMin = new Vector2(20f, CraftButtonHeight + 32f);
                    partsRect.offsetMax = new Vector2(-20f, CraftButtonHeight + 32f + PartSlotsHeight);

                    HorizontalLayoutGroup layout = partsGO.AddComponent<HorizontalLayoutGroup>();
                    layout.spacing = 16f;
                    layout.childForceExpandWidth = false;
                    layout.childForceExpandHeight = false;
                    layout.childControlWidth = false;
                    layout.childControlHeight = false;
                    layout.childAlignment = TextAnchor.MiddleLeft;

                    partsGO.SetActive(false);   // 도구 레시피를 고르기 전에는 숨어 있다
                }

                Transform templateTransform = partsRect.Find("PartSlotTemplate");
                if (templateTransform == null)
                {
                    GameObject templateGO = (GameObject)PrefabUtility.InstantiatePrefab(partSlotPrefab, partsRect);
                    templateGO.name = "PartSlotTemplate";
                    templateGO.SetActive(false);
                    templateTransform = templateGO.transform;
                }

                // 부품 칸 줄이 들어간 만큼 재료 목록의 아래쪽을 올린다.
                RectTransform listRect = detail.Find("MaterialList") as RectTransform;
                if (listRect != null)
                    listRect.offsetMin = new Vector2(listRect.offsetMin.x, CraftButtonHeight + PartSlotsHeight + 44f);

                SerializedObject so = new SerializedObject(ui);
                so.FindProperty("partSlotsRoot").objectReferenceValue = partsRect;
                so.FindProperty("partSlotTemplate").objectReferenceValue = templateTransform.GetComponent<ToolPartSlotUI>();
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, path);
                Debug.Log("[CraftingTableUIFactory] 조합대 UI 에 도구 부품 칸을 추가했습니다: " + path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// MachineSlot 프리팹의 아트를 그대로 물려받은 도구 부품 칸 프리팹을 만든다.
        /// 드래그는 그대로 두고(부품을 넣고 빼야 한다) 종류 제한만 얹은 <see cref="ToolPartSlotUI"/> 로 교체한다.
        /// </summary>
        private static GameObject EnsureToolPartSlotPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(MachineUIFactoryPaths.ToolPartSlotPrefab);
            if (existing != null && existing.GetComponent<ToolPartSlotUI>() != null) return existing;

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(MachineUIFactoryPaths.SlotPrefab);
            if (source == null)
            {
                Debug.LogError("[CraftingTableUIFactory] 원본 슬롯 프리팹이 없습니다: " + MachineUIFactoryPaths.SlotPrefab);
                return null;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(MachineUIFactoryPaths.SlotPrefab);
            try
            {
                // 기존 슬롯의 참조를 옮겨 받은 뒤 컴포넌트만 갈아 끼운다.
                ItemSlot itemSlot = contents.GetComponent<ItemSlot>();
                Object icon = null, count = null, selectedSprite = null;
                if (itemSlot != null)
                {
                    SerializedObject src = new SerializedObject(itemSlot);
                    icon = GetRef(src, "iconImage");
                    count = GetRef(src, "countText");
                    selectedSprite = GetRef(src, "selectedSlotSprite");
                    Object.DestroyImmediate(itemSlot, true);
                }

                // 역할 태그는 DefaultMachineUI 가 수집해 버리므로 제거한다(조합대가 직접 관리한다).
                MachineUIElement element = contents.GetComponent<MachineUIElement>();
                if (element != null) Object.DestroyImmediate(element, true);

                if (icon == null) icon = FindComponentByName<Image>(contents.transform, "icon");
                if (count == null) count = FindComponentByName<TMP_Text>(contents.transform, "count");

                ToolPartSlotUI slot = contents.AddComponent<ToolPartSlotUI>();
                SerializedObject dst = new SerializedObject(slot);
                SetRef(dst, "iconImage", icon);
                SetRef(dst, "countText", count);
                SetRef(dst, "selectedSlotSprite", selectedSprite);
                dst.ApplyModifiedPropertiesWithoutUndo();

                EnsureFolder(MachineUIFactoryPaths.BuildingBlockFolder);
                return PrefabUtility.SaveAsPrefabAsset(contents, MachineUIFactoryPaths.ToolPartSlotPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ── 헬퍼 ──────────────────────────────────────────────
        private static Object GetRef(SerializedObject so, string path)
        {
            SerializedProperty property = so.FindProperty(path);
            return property != null ? property.objectReferenceValue : null;
        }

        private static void SetRef(SerializedObject so, string path, Object value)
        {
            SerializedProperty property = so.FindProperty(path);
            if (property != null) property.objectReferenceValue = value;
        }

        private static T FindComponentByName<T>(Transform root, string name) where T : Component
        {
            foreach (T candidate in root.GetComponentsInChildren<T>(true))
                if (candidate.gameObject.name == name) return candidate;
            return null;
        }

        private static GameObject NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static RectTransform Stretch(GameObject go, float horizontalPadding, float verticalPadding)
        {
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
            rect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
            return rect;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, float size)
        {
            GameObject go = NewRect(name, parent);
            Stretch(go, 0f, 0f);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.richText = false;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>기존 기계 UI와 톤을 맞추기 위해 MachinePanelBase 의 배경 스프라이트를 재사용한다.</summary>
        private static Sprite ResolveBackgroundSprite()
        {
            GameObject panelBase = AssetDatabase.LoadAssetAtPath<GameObject>(MachineUIFactoryPaths.PanelBasePrefab);
            Image image = panelBase != null ? panelBase.GetComponent<Image>() : null;
            return image != null ? image.sprite : null;
        }

        private static TMP_FontAsset ResolveFont()
        {
            GameObject nameText = AssetDatabase.LoadAssetAtPath<GameObject>(MachineUIFactoryPaths.NameTextPrefab);
            TMP_Text text = nameText != null ? nameText.GetComponentInChildren<TMP_Text>(true) : null;
            if (text != null && text.font != null) return text.font;
            return TMP_Settings.defaultFontAsset;
        }

        private static Vector2 ResolveCellSize(GameObject slotPrefab)
        {
            RectTransform rect = slotPrefab != null ? slotPrefab.transform as RectTransform : null;
            if (rect == null || rect.sizeDelta.x <= 1f || rect.sizeDelta.y <= 1f) return new Vector2(120f, 120f);
            return rect.sizeDelta;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
