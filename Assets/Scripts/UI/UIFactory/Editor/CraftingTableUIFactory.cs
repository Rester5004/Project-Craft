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
            title.text = "Crafting Table";

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
            placeholder.text = "Search...";
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
            tabLabel.text = "Tab";
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
            scrollRect.offsetMin = new Vector2(Margin, Margin);
            scrollRect.offsetMax = new Vector2(-Margin, -(Margin * 0.6f + HeaderHeight + 20f + TabsHeight + 16f));

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
            grid.spacing = new Vector2(12f, 12f);
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

            // ── 컴포넌트 배선 ──
            CraftingTableUI ui = root.AddComponent<CraftingTableUI>();
            SerializedObject so = new SerializedObject(ui);
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
