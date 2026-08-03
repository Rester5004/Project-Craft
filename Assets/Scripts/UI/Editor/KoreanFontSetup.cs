using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectCraft.EditorTools
{
    /// <summary>
    /// 프로젝트 전체의 TMP 폰트를 한글 지원 폰트(Maplestory Bold SDF)로 교체한다.
    /// 폰트 에셋 자체는 이미 만들어져 있고(Dynamic 아틀라스), 이 도구는 참조만 갈아끼운다.
    /// 자동화가 멈추지 않도록 대화상자를 띄우지 않는다.
    /// </summary>
    public static class KoreanFontSetup
    {
        public const string KoreanFontPath = "Assets/TextMesh Pro/Fonts/Maplestory Bold SDF.asset";

        [MenuItem("Tools/Project Craft/Font/Apply Korean Font To All")]
        public static void ApplyMenu() => Apply();

        /// <summary>기본 폰트 설정 + 모든 프리팹/씬의 TMP 폰트를 교체한다. 교체한 컴포넌트 수를 반환.</summary>
        public static int Apply()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (font == null)
            {
                Debug.LogError("[KoreanFontSetup] 폰트 에셋을 찾을 수 없습니다: " + KoreanFontPath);
                return 0;
            }

            int changed = 0;
            changed += ApplyToSettings(font) ? 1 : 0;
            changed += ApplyToPrefabs(font);
            changed += ApplyToScenes(font);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[KoreanFontSetup] 폰트 교체 완료: " + changed + "곳 (" + font.name + ")", font);
            return changed;
        }

        /// <summary>TMP_Settings 의 기본 폰트. 런타임 생성 UI(CommandConsole 등)가 이 값을 폴백으로 쓴다.</summary>
        private static bool ApplyToSettings(TMP_FontAsset font)
        {
            if (TMP_Settings.instance == null)
            {
                Debug.LogWarning("[KoreanFontSetup] TMP Settings 가 없습니다.");
                return false;
            }
            if (TMP_Settings.defaultFontAsset == font) return false;

            SerializedObject so = new SerializedObject(TMP_Settings.instance);
            SerializedProperty property = so.FindProperty("m_defaultFontAsset");
            if (property == null)
            {
                Debug.LogWarning("[KoreanFontSetup] m_defaultFontAsset 프로퍼티를 찾지 못했습니다.");
                return false;
            }
            property.objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(TMP_Settings.instance);
            return true;
        }

        private static int ApplyToPrefabs(TMP_FontAsset font)
        {
            int changed = 0;
            string[] guids = AssetDatabase.FindAssets("t:Prefab");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.StartsWith("Packages/")) continue;

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || !NeedsChange(asset, font)) continue;   // 열기 전에 싸게 걸러낸다

                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int n = ApplyToHierarchy(contents, font);
                    if (n > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                        changed += n;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            return changed;
        }

        private static int ApplyToScenes(TMP_FontAsset font)
        {
            // 씬을 통째로 갈아 끼우기 전에 저장하지 않은 작업을 먼저 처리한다.
            // 이게 없으면 메뉴를 누르는 순간 열려 있던 씬의 미저장 변경이 확인 없이 날아간다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[KoreanFontSetup] 사용자가 취소했습니다. 씬은 건드리지 않았습니다.");
                return 0;
            }

            string previousScene = SceneManager.GetActiveScene().path;
            int changed = 0;

            try
            {
                string[] guids = AssetDatabase.FindAssets("t:Scene");
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (path.StartsWith("Packages/")) continue;

                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    int n = 0;
                    GameObject[] roots = scene.GetRootGameObjects();
                    for (int r = 0; r < roots.Length; r++) n += ApplyToHierarchy(roots[r], font);

                    if (n > 0)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        changed += n;
                    }
                }
            }
            finally
            {
                // 중간에 터져도 원래 씬으로는 반드시 돌아온다. 안 그러면 마지막으로 연 남의 씬이 열린 채 남는다.
                if (!string.IsNullOrEmpty(previousScene))
                    EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }

            return changed;
        }

        private static bool NeedsChange(GameObject root, TMP_FontAsset font)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                if (text.font != font) return true;
            foreach (TMP_InputField field in root.GetComponentsInChildren<TMP_InputField>(true))
                if (field.fontAsset != font) return true;
            return false;
        }

        private static int ApplyToHierarchy(GameObject root, TMP_FontAsset font)
        {
            int changed = 0;

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font == font) continue;
                text.font = font;
                EditorUtility.SetDirty(text);
                changed++;
            }

            foreach (TMP_InputField field in root.GetComponentsInChildren<TMP_InputField>(true))
            {
                if (field.fontAsset == font) continue;
                field.fontAsset = font;
                EditorUtility.SetDirty(field);
                changed++;
            }

            return changed;
        }

        /// <summary>교체가 남아 있는 곳을 목록으로 보고한다(검증용, 변경 없음).</summary>
        [MenuItem("Tools/Project Craft/Font/Report Remaining Fonts")]
        public static void Report()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            List<string> remaining = new List<string>();

            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.StartsWith("Packages/")) continue;
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;

                foreach (TMP_Text text in asset.GetComponentsInChildren<TMP_Text>(true))
                    if (text.font != font)
                        remaining.Add(path + " → " + text.name + " (" + (text.font != null ? text.font.name : "null") + ")");
            }

            Debug.Log("[KoreanFontSetup] 기본 폰트=" + (TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset.name : "null")
                + " / 미교체 프리팹 텍스트 " + remaining.Count + "개\n" + string.Join("\n", remaining.ToArray()));
        }
    }
}
