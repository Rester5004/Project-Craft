using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectCraft.UIFactory.EditorTools
{
    /// <summary>
    /// 기계 UI 제작 창. 전용 씬(MachineUIFactory)에서 요소를 추가/역할 지정하고,
    /// 위치·크기는 씬뷰의 기본 RectTransform 툴로 배치한 뒤 프리팹으로 저장한다.
    /// </summary>
    public class MachineUIFactoryWindow : EditorWindow
    {
        private MachineBlock target;
        private Vector2 scroll;
        private List<LayoutIssue> issues = new();

        [MenuItem("Tools/Project Craft/Machine UI/Machine UI Factory")]
        public static void OpenWindow()
        {
            MachineUIFactoryWindow window = GetWindow<MachineUIFactoryWindow>();
            window.titleContent = new GUIContent("Machine UI Factory");
            window.minSize = new Vector2(360f, 480f);
        }

        private void OnSelectionChange() => Repaint();

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawSceneSection();
            EditorGUILayout.Space();
            DrawTargetSection();
            EditorGUILayout.Space();
            DrawLayoutSection();
            EditorGUILayout.Space();
            DrawAddElementSection();
            EditorGUILayout.Space();
            DrawSelectionSection();
            EditorGUILayout.Space();
            DrawValidationSection();
            EditorGUILayout.Space();
            DrawSaveSection();

            EditorGUILayout.EndScrollView();
        }

        // ── 1. 제작 씬 ───────────────────────────────────────────────
        private void DrawSceneSection()
        {
            EditorGUILayout.LabelField("1. 제작 씬", EditorStyles.boldLabel);

            if (IsFactorySceneOpen())
            {
                EditorGUILayout.HelpBox("제작 씬이 열려 있습니다.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("기계 UI는 전용 제작 씬에서 만듭니다.", MessageType.Warning);
            if (GUILayout.Button("제작 씬 열기"))
            {
                if (!File.Exists(MachineUIFactoryPaths.FactoryScene))
                {
                    EditorUtility.DisplayDialog("Machine UI Factory",
                        $"제작 씬이 없습니다:\n{MachineUIFactoryPaths.FactoryScene}", "확인");
                    return;
                }
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(MachineUIFactoryPaths.FactoryScene, OpenSceneMode.Single);
            }
        }

        // ── 2. 대상 기계 ─────────────────────────────────────────────
        private void DrawTargetSection()
        {
            EditorGUILayout.LabelField("2. 대상 기계", EditorStyles.boldLabel);
            target = (MachineBlock)EditorGUILayout.ObjectField("MachineBlock", target, typeof(MachineBlock), false);

            if (target == null)
            {
                EditorGUILayout.HelpBox("레이아웃을 만들 기계의 MachineBlock 을 지정하세요.", MessageType.Info);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("입력 슬롯", target.inputSlotCount);
                EditorGUILayout.IntField("출력 슬롯", target.outputSlotCount);
                EditorGUILayout.IntField("가스 입력 슬롯", target.inputGasSlotCount);
                EditorGUILayout.IntField("가스 출력 슬롯", target.outputGasSlotCount);
                EditorGUILayout.FloatField("가스 최대치(공용)", target.maxGasAmount);
                EditorGUILayout.Toggle("에너지 사용", target.isUseEnergy);
                EditorGUILayout.ObjectField("현재 uiPrefab", target.uiPrefab, typeof(GameObject), false);
            }
        }

        // ── 3. 레이아웃 ──────────────────────────────────────────────
        private void DrawLayoutSection()
        {
            EditorGUILayout.LabelField("3. 레이아웃", EditorStyles.boldLabel);

            MachineUIFactoryStage stage = FindStage();
            if (stage == null)
            {
                EditorGUILayout.HelpBox("제작 씬에서 MachineUIFactoryStage 를 찾지 못했습니다.", MessageType.Warning);
                return;
            }

            if (!HasBuildingBlocks())
            {
                EditorGUILayout.HelpBox(
                    "빌딩 블록 프리팹이 없습니다.\nMapTest 씬을 열고 'Tools/Project Craft/Machine UI/1. 기존 MachinePanel 마이그레이션' 을 먼저 실행하세요.",
                    MessageType.Error);
            }

            GameObject layout = FindLayoutRoot(stage);
            EditorGUILayout.LabelField("현재 작업물", layout != null ? layout.name : "(없음)");

            using (new EditorGUI.DisabledScope(target == null || !HasBuildingBlocks()))
            {
                if (GUILayout.Button("새 레이아웃 만들기 (설정대로 자동 배치)"))
                    CreateNewLayout(stage);

                using (new EditorGUI.DisabledScope(target == null || target.uiPrefab == null))
                {
                    if (GUILayout.Button("기존 uiPrefab 불러오기"))
                        LoadExistingLayout(stage);
                }
            }
        }

        // ── 4. 요소 추가 ─────────────────────────────────────────────
        private void DrawAddElementSection()
        {
            EditorGUILayout.LabelField("4. 요소 추가", EditorStyles.boldLabel);

            GameObject layout = FindLayoutRoot(FindStage());
            using (new EditorGUI.DisabledScope(layout == null || !HasBuildingBlocks()))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("입력 슬롯")) AddElement(layout, MachineUIRole.InputSlot);
                    if (GUILayout.Button("출력 슬롯")) AddElement(layout, MachineUIRole.OutputSlot);
                    if (GUILayout.Button("연료 슬롯")) AddElement(layout, MachineUIRole.FuelSlot);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("입력 가스 바")) AddElement(layout, MachineUIRole.InputGasBar);
                    if (GUILayout.Button("출력 가스 바")) AddElement(layout, MachineUIRole.OutputGasBar);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("에너지 바")) AddElement(layout, MachineUIRole.EnergyBar);
                    if (GUILayout.Button("연료 바")) AddElement(layout, MachineUIRole.FuelBar);
                    if (GUILayout.Button("진행도 바")) AddElement(layout, MachineUIRole.ProgressBar);
                }
                if (GUILayout.Button("기계 이름")) AddElement(layout, MachineUIRole.MachineName);
            }
            EditorGUILayout.HelpBox("위치·크기는 씬뷰에서 직접 옮기세요(RectTransform 툴 그대로 사용).", MessageType.None);
        }

        // ── 5. 선택 요소 ─────────────────────────────────────────────
        private void DrawSelectionSection()
        {
            EditorGUILayout.LabelField("5. 선택한 요소", EditorStyles.boldLabel);

            GameObject selected = Selection.activeGameObject;
            MachineUIElement element = selected != null ? selected.GetComponent<MachineUIElement>() : null;
            if (element == null)
            {
                EditorGUILayout.HelpBox("씬에서 요소를 선택하면 역할/인덱스를 여기서 바꿀 수 있습니다.", MessageType.None);
                return;
            }

            EditorGUI.BeginChangeCheck();
            MachineUIRole role = (MachineUIRole)EditorGUILayout.EnumPopup("역할", element.role);
            int index = EditorGUILayout.IntField("인덱스", element.index);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(element, "Edit Machine UI Element");
                element.role = role;
                element.index = index;
                EditorUtility.SetDirty(element);
            }

            if (GUILayout.Button("역할별 인덱스 재정렬 (계층 순서 기준)"))
                ReindexAll(FindLayoutRoot(FindStage()));
        }

        // ── 6. 검증 ─────────────────────────────────────────────────
        private void DrawValidationSection()
        {
            EditorGUILayout.LabelField("6. 검증", EditorStyles.boldLabel);

            if (GUILayout.Button("검증 실행"))
                issues = MachineUILayoutValidator.Validate(FindLayoutRoot(FindStage()), target);

            if (issues == null || issues.Count == 0)
            {
                EditorGUILayout.HelpBox("문제 없음(또는 아직 검증하지 않음).", MessageType.None);
                return;
            }

            foreach (LayoutIssue issue in issues)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox(issue.message,
                        issue.level == IssueLevel.Error ? MessageType.Error : MessageType.Warning);
                    if (issue.context != null && GUILayout.Button("선택", GUILayout.Width(48f), GUILayout.Height(38f)))
                        Selection.activeObject = issue.context;
                }
            }
        }

        // ── 7. 저장 ─────────────────────────────────────────────────
        private void DrawSaveSection()
        {
            EditorGUILayout.LabelField("7. 저장", EditorStyles.boldLabel);

            GameObject layout = FindLayoutRoot(FindStage());
            using (new EditorGUI.DisabledScope(layout == null || target == null))
            {
                if (GUILayout.Button("프리팹으로 저장 & MachineBlock 에 연결", GUILayout.Height(30f)))
                    SaveAndLink(layout);
            }
        }

        // ── 동작 ────────────────────────────────────────────────────
        private void CreateNewLayout(MachineUIFactoryStage stage)
        {
            RectTransform root = stage.Root;
            if (root.childCount > 0 &&
                !EditorUtility.DisplayDialog("새 레이아웃", "작업 루트의 기존 내용을 지우고 새로 만듭니다. 계속할까요?", "계속", "취소"))
                return;

            for (int i = root.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);

            // 기본 배경은 기존 MachinePanel 에서 추출한 패널 프리팹을 사용한다.
            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MachineUIFactoryPaths.PanelBasePrefab);
            GameObject panel;
            if (basePrefab != null)
            {
                panel = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, root);
                PrefabUtility.UnpackPrefabInstance(panel, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                panel.name = $"{target.name}_UI";
                panel.SetActive(true);
                if (panel.GetComponent<DefaultMachineUI>() == null) panel.AddComponent<DefaultMachineUI>();
            }
            else
            {
                Debug.LogWarning($"[Machine UI Factory] 패널 배경 프리팹이 없어 임시 배경을 사용합니다: {MachineUIFactoryPaths.PanelBasePrefab}");
                panel = new GameObject($"{target.name}_UI", typeof(RectTransform), typeof(Image), typeof(DefaultMachineUI));
                RectTransform plain = (RectTransform)panel.transform;
                plain.SetParent(root, false);
                plain.sizeDelta = new Vector2(800f, 500f);
                panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            }
            Undo.RegisterCreatedObjectUndo(panel, "Create Machine UI Layout");
            RectTransform rect = (RectTransform)panel.transform;
            rect.anchoredPosition = Vector2.zero;

            const float step = 120f;
            for (int i = 0; i < Mathf.Max(0, target.inputSlotCount); i++)
                PlaceElement(panel, MachineUIRole.InputSlot, i, new Vector2(-300f + i * step, 140f));
            for (int i = 0; i < Mathf.Max(0, target.outputSlotCount); i++)
                PlaceElement(panel, MachineUIRole.OutputSlot, i, new Vector2(-300f + i * step, -120f));
            for (int i = 0; i < Mathf.Max(0, target.inputGasSlotCount); i++)
                PlaceElement(panel, MachineUIRole.InputGasBar, i, new Vector2(-260f - i * 70f, 0f));
            for (int i = 0; i < Mathf.Max(0, target.outputGasSlotCount); i++)
                PlaceElement(panel, MachineUIRole.OutputGasBar, i, new Vector2(260f + i * 70f, 0f));
            if (target.isUseEnergy)
                PlaceElement(panel, MachineUIRole.EnergyBar, 0, new Vector2(-360f, 0f));
            for (int i = 0; i < target.fuelSlotCount; i++)
                PlaceElement(panel, MachineUIRole.FuelSlot, i, new Vector2(-300f + i * 140f, 10f));
            if (target.UsesFuel)
                PlaceElement(panel, MachineUIRole.FuelBar, 0, new Vector2(-360f, 0f));
            PlaceElement(panel, MachineUIRole.ProgressBar, 0, new Vector2(0f, 10f));
            PlaceElement(panel, MachineUIRole.MachineName, 0, new Vector2(0f, 210f));

            Selection.activeGameObject = panel;
            issues = MachineUILayoutValidator.Validate(panel, target);
        }

        private void LoadExistingLayout(MachineUIFactoryStage stage)
        {
            RectTransform root = stage.Root;
            if (root.childCount > 0 &&
                !EditorUtility.DisplayDialog("불러오기", "작업 루트의 기존 내용을 지우고 불러옵니다. 계속할까요?", "계속", "취소"))
                return;

            for (int i = root.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(target.uiPrefab, root);
            Undo.RegisterCreatedObjectUndo(instance, "Load Machine UI Layout");
            instance.SetActive(true);
            Selection.activeGameObject = instance;
            issues = MachineUILayoutValidator.Validate(instance, target);
        }

        private void AddElement(GameObject layout, MachineUIRole role)
        {
            int nextIndex = NextIndexFor(layout, role);
            GameObject created = PlaceElement(layout, role, nextIndex, new Vector2(nextIndex * 16f, nextIndex * -16f));
            if (created != null) Selection.activeGameObject = created;
        }

        private static GameObject PlaceElement(GameObject layout, MachineUIRole role, int index, Vector2 anchoredPosition)
        {
            string path = MachineUIFactoryPaths.PrefabPathFor(role);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[Machine UI Factory] 빌딩 블록 프리팹이 없습니다: {path}");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, layout.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Add Machine UI Element");
            instance.SetActive(true);
            instance.name = $"{role}_{index}";

            if (instance.transform is RectTransform rect)
                rect.anchoredPosition = anchoredPosition;

            MachineUIElement element = instance.GetComponent<MachineUIElement>();
            if (element == null) element = Undo.AddComponent<MachineUIElement>(instance);
            element.role = role;
            element.index = index;
            EditorUtility.SetDirty(element);
            return instance;
        }

        private void SaveAndLink(GameObject layout)
        {
            MachineUIFactoryPathsEnsure();
            string path = $"{MachineUIFactoryPaths.OutputFolder}/{target.name}_UI.prefab";

            GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(layout, path, InteractionMode.UserAction);
            if (saved == null)
            {
                EditorUtility.DisplayDialog("Machine UI Factory", "프리팹 저장에 실패했습니다.", "확인");
                return;
            }

            SerializedObject so = new(target);
            so.FindProperty("uiPrefab").objectReferenceValue = saved;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();

            issues = MachineUILayoutValidator.Validate(layout, target);
            EditorUtility.DisplayDialog("Machine UI Factory",
                $"저장 완료:\n{path}\n\n{target.name}.uiPrefab 에 연결되었습니다.", "확인");
        }

        // ── 헬퍼 ────────────────────────────────────────────────────
        private static MachineUIFactoryStage FindStage()
            => Object.FindFirstObjectByType<MachineUIFactoryStage>(FindObjectsInactive.Include);

        private static GameObject FindLayoutRoot(MachineUIFactoryStage stage)
        {
            if (stage == null) return null;
            RectTransform root = stage.Root;
            for (int i = 0; i < root.childCount; i++)
            {
                DefaultMachineUI ui = root.GetChild(i).GetComponent<DefaultMachineUI>();
                if (ui != null) return ui.gameObject;
            }
            return null;
        }

        private static bool IsFactorySceneOpen()
            => SceneManager.GetActiveScene().path == MachineUIFactoryPaths.FactoryScene;

        private static bool HasBuildingBlocks()
            => AssetDatabase.LoadAssetAtPath<GameObject>(MachineUIFactoryPaths.SlotPrefab) != null;

        private static int NextIndexFor(GameObject layout, MachineUIRole role)
        {
            int next = 0;
            foreach (MachineUIElement e in layout.GetComponentsInChildren<MachineUIElement>(true))
                if (e.role == role) next = Mathf.Max(next, e.index + 1);
            return next;
        }

        private static void ReindexAll(GameObject layout)
        {
            if (layout == null) return;
            Dictionary<MachineUIRole, int> counters = new();
            foreach (MachineUIElement e in layout.GetComponentsInChildren<MachineUIElement>(true))
            {
                counters.TryGetValue(e.role, out int n);
                Undo.RecordObject(e, "Reindex Machine UI Elements");
                e.index = n;
                counters[e.role] = n + 1;
                EditorUtility.SetDirty(e);
            }
        }

        private static void MachineUIFactoryPathsEnsure()
        {
            if (AssetDatabase.IsValidFolder(MachineUIFactoryPaths.OutputFolder)) return;
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            AssetDatabase.CreateFolder("Assets/Prefabs/UI", "Machines");
        }
    }
}
