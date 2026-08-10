using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectCraft.UIFactory.EditorTools
{
    /// <summary>
    /// 기존 MachinePanel(하드코딩 레이아웃)을 요소 기반으로 전환하고,
    /// 제작 도구가 사용할 빌딩 블록 프리팹을 씬 오브젝트에서 추출한다. 일회성 도구.
    /// </summary>
    public static class MachineUIMigration
    {
        [MenuItem("Tools/Project Craft/Machine UI/1. 기존 MachinePanel 마이그레이션 + 빌딩 블록 추출")]
        public static void Migrate() => Migrate(true);

        /// <summary>showDialog=false 로 호출하면 모달 없이 실행된다(자동화용).</summary>
        public static void Migrate(bool showDialog)
        {
            DefaultMachineUI panel = Object.FindFirstObjectByType<DefaultMachineUI>(FindObjectsInactive.Include);
            if (panel == null)
            {
                const string missing = "현재 씬에서 DefaultMachineUI(MachinePanel)를 찾지 못했습니다. MapTest 씬을 연 뒤 다시 실행하세요.";
                if (showDialog) EditorUtility.DisplayDialog("Machine UI 마이그레이션", missing, "확인");
                else Debug.LogError("[MachineUIMigration] " + missing);
                return;
            }

            Transform root = panel.transform;
            int tagged = 0;

            tagged += TagChildren(root.Find("Inputs"), MachineUIRole.InputSlot);
            tagged += TagChildren(root.Find("Outputs"), MachineUIRole.OutputSlot);
            // 레거시 패널의 가스바 2개는 입력/출력 하나씩으로 매핑한다.
            tagged += Tag(root.Find("GasBar1"), MachineUIRole.InputFluidBar, 0);
            tagged += Tag(root.Find("GasBar2"), MachineUIRole.OutputFluidBar, 0);
            tagged += Tag(root.Find("EnergyBar"), MachineUIRole.EnergyBar, 0);
            tagged += Tag(root.Find("ProgressBar"), MachineUIRole.ProgressBar, 0);
            tagged += Tag(root.Find("MachineName"), MachineUIRole.MachineName, 0);

            EnsureFolder(MachineUIFactoryPaths.BuildingBlockFolder);
            EnsureFolder(MachineUIFactoryPaths.OutputFolder);

            int extracted = 0;
            extracted += ExtractPanelBase(panel.gameObject, MachineUIFactoryPaths.PanelBasePrefab);
            Transform inputs = root.Find("Inputs");
            if (inputs != null && inputs.childCount > 0)
                extracted += Extract(inputs.GetChild(0).gameObject, MachineUIFactoryPaths.SlotPrefab, MachineUIRole.InputSlot);
            extracted += Extract(root.Find("ProgressBar"), MachineUIFactoryPaths.ProgressBarPrefab, MachineUIRole.ProgressBar);
            extracted += Extract(root.Find("EnergyBar"), MachineUIFactoryPaths.EnergyBarPrefab, MachineUIRole.EnergyBar);
            extracted += Extract(root.Find("GasBar1"), MachineUIFactoryPaths.FluidBarPrefab, MachineUIRole.InputFluidBar);
            extracted += Extract(root.Find("MachineName"), MachineUIFactoryPaths.NameTextPrefab, MachineUIRole.MachineName);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.SetDirty(panel.gameObject);

            string summary = $"요소 태그 {tagged}개 부착, 빌딩 블록 {extracted}개 추출 완료. 씬을 저장하세요.";
            if (showDialog) EditorUtility.DisplayDialog("Machine UI 마이그레이션", summary, "확인");
            else Debug.Log("[MachineUIMigration] " + summary);
        }

        private static int TagChildren(Transform parent, MachineUIRole role)
        {
            if (parent == null) return 0;
            int count = 0;
            for (int i = 0; i < parent.childCount; i++)
                count += Tag(parent.GetChild(i), role, i);
            return count;
        }

        private static int Tag(Transform target, MachineUIRole role, int index)
        {
            if (target == null) return 0;
            MachineUIElement element = target.GetComponent<MachineUIElement>();
            if (element == null)
                element = Undo.AddComponent<MachineUIElement>(target.gameObject);
            Undo.RecordObject(element, "Tag Machine UI Element");
            element.role = role;
            element.index = index;
            EditorUtility.SetDirty(element);
            return 1;
        }

        /// <summary>
        /// MachinePanel 의 배경(패널 이미지)만 남긴 프리팹을 추출한다.
        /// 새 레이아웃의 기본 배경으로 사용되며, 스트레치 앵커는 실제 렌더 크기의 고정 사각형으로 변환한다.
        /// </summary>
        private static int ExtractPanelBase(GameObject source, string path)
        {
            if (source == null || File.Exists(path)) return 0;

            RectTransform sourceRect = source.transform as RectTransform;
            Vector2 size = sourceRect != null ? sourceRect.rect.size : Vector2.zero;
            if (size.x < 1f || size.y < 1f) size = new Vector2(1960f, 853f); // 레이아웃 미계산 대비 폴백

            GameObject copy = Object.Instantiate(source);
            copy.name = Path.GetFileNameWithoutExtension(path);
            copy.SetActive(true);

            if (PrefabUtility.IsPartOfPrefabInstance(copy))
                PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // 배경만 남기고 자식(슬롯/바/이름) 제거
            for (int i = copy.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(copy.transform.GetChild(i).gameObject);

            if (copy.transform is RectTransform rect)
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                rect.anchoredPosition = Vector2.zero;
            }

            MachineUIElement stray = copy.GetComponent<MachineUIElement>();
            if (stray != null) Object.DestroyImmediate(stray);
            if (copy.GetComponent<DefaultMachineUI>() == null) copy.AddComponent<DefaultMachineUI>();

            PrefabUtility.SaveAsPrefabAsset(copy, path);
            Object.DestroyImmediate(copy);
            Debug.Log($"[MachineUIMigration] 패널 배경 추출: {path} (size={size})");
            return 1;
        }

        /// <summary>씬 오브젝트를 독립 프리팹으로 추출한다(이미 있으면 건너뜀).</summary>
        private static int Extract(Transform source, string path, MachineUIRole role)
            => source == null ? 0 : Extract(source.gameObject, path, role);

        private static int Extract(GameObject source, string path, MachineUIRole role)
        {
            if (source == null) return 0;
            if (File.Exists(path)) return 0;

            GameObject copy = Object.Instantiate(source);
            copy.name = Path.GetFileNameWithoutExtension(path);
            copy.SetActive(true);

            if (PrefabUtility.IsPartOfPrefabInstance(copy))
                PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            MachineUIElement element = copy.GetComponent<MachineUIElement>();
            if (element == null) element = copy.AddComponent<MachineUIElement>();
            element.role = role;
            element.index = 0;

            PrefabUtility.SaveAsPrefabAsset(copy, path);
            Object.DestroyImmediate(copy);
            return 1;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
