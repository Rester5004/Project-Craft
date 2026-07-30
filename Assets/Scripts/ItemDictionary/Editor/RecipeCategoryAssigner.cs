using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectCraft.EditorTools
{
    /// <summary>
    /// 레시피에 조합대 탭(<see cref="RecipeCategory"/>)을 붙인다.
    ///
    /// 카테고리가 비어 있으면 <see cref="RecipeDictionary.CollectCategories"/> 가 그 레시피를 건너뛰고,
    /// <see cref="RecipeDictionary.CollectRecipes"/> 는 탭과 정확히 일치하는 것만 모으므로
    /// <b>어느 탭에도 뜨지 않아 사실상 사라진다.</b> 임포터가 카테고리를 채우지 않으므로 여기서 채운다.
    ///
    /// 분류는 산출물의 종류로 정한다. 재실행 가능(이미 지정된 것은 건드리지 않는다).
    /// </summary>
    public static class RecipeCategoryAssigner
    {
        private const string CategoryFolder = "Assets/Prefabs/Recipes/Category";
        private const string ReportPath = "Assets/Prefabs/Recipes/_CategoryReport.md";

        // 에셋 파일 이름 → 화면에 보일 한글 이름 · 정렬 순서
        private static readonly string[,] Categories =
        {
            { "resource", "자원", "0" },
            { "machine",  "기계", "1" },
            { "block",    "블록", "2" },
            { "tool",     "도구", "3" },
        };

        [MenuItem("Tools/Project Craft/Recipes/Assign Recipe Categories")]
        public static void Assign()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("# 레시피 카테고리 배정 보고서");
            report.AppendLine();
            report.AppendLine("`Tools/Project Craft/Recipes/Assign Recipe Categories` 가 자동 생성한 파일입니다.");
            report.AppendLine();

            Dictionary<string, RecipeCategory> categories = EnsureCategories(report);

            int assigned = 0, kept = 0, noOutput = 0;
            Dictionary<string, int> tally = new Dictionary<string, int>();
            List<string> unresolved = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Recipe"))
            {
                Recipe recipe = AssetDatabase.LoadAssetAtPath<Recipe>(AssetDatabase.GUIDToAssetPath(guid));
                if (recipe == null) continue;

                if (recipe.category != null) { kept++; continue; }

                Items output = recipe.PrimaryOutput;
                if (output == null)
                {
                    // 산출물이 없으면 어차피 등록되지 않는다. 분류할 근거도 없다.
                    noOutput++;
                    continue;
                }

                string key = Classify(output);
                if (!categories.TryGetValue(key, out RecipeCategory category))
                {
                    unresolved.Add($"- `{AssetDatabase.GetAssetPath(recipe)}` → '{key}' 카테고리 에셋이 없음");
                    continue;
                }

                recipe.category = category;
                EditorUtility.SetDirty(recipe);
                assigned++;

                tally.TryGetValue(key, out int count);
                tally[key] = count + 1;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.AppendLine("## 결과");
            report.AppendLine();
            report.AppendLine($"- 새로 배정 {assigned}개 · 이미 지정돼 있어 유지 {kept}개 · 산출물이 없어 건너뜀 {noOutput}개");
            report.AppendLine();
            foreach (KeyValuePair<string, int> pair in tally)
                report.AppendLine($"- {pair.Key} : {pair.Value}개");

            if (unresolved.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("## ⚠ 배정하지 못한 레시피");
                report.AppendLine();
                foreach (string line in unresolved) report.AppendLine(line);
            }

            File.WriteAllText(ReportPath, report.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ReportPath);
            Debug.Log("[RecipeCategoryAssigner] 완료. 자세한 내역은 " + ReportPath);
        }

        /// <summary>
        /// 산출물의 종류로 탭을 정한다. 기계도 배치 가능하므로 기계를 먼저 걸러야 한다.
        /// </summary>
        private static string Classify(Items output)
        {
            if (output.itemName.StartsWith("Machine:") || output.itemName == "CoreCrafter") return "machine";
            if (output is ToolItem || output is ToolPartItem) return "tool";
            if (output.placeable) return "block";
            return "resource";
        }

        /// <summary>카테고리 에셋을 확보하고 표시 이름을 한글로 맞춘다.</summary>
        private static Dictionary<string, RecipeCategory> EnsureCategories(StringBuilder report)
        {
            Dictionary<string, RecipeCategory> result = new Dictionary<string, RecipeCategory>();
            List<string> touched = new List<string>();

            for (int i = 0; i < Categories.GetLength(0); i++)
            {
                string id = Categories[i, 0];
                string korean = Categories[i, 1];
                int sortOrder = int.Parse(Categories[i, 2]);
                string path = $"{CategoryFolder}/{id}.asset";

                RecipeCategory category = AssetDatabase.LoadAssetAtPath<RecipeCategory>(path);
                if (category == null)
                {
                    category = ScriptableObject.CreateInstance<RecipeCategory>();
                    category.displayName = korean;
                    category.sortOrder = sortOrder;
                    AssetDatabase.CreateAsset(category, path);
                    touched.Add($"- `{id}` 새로 만듦 ({korean})");
                }
                else if (category.displayName != korean)
                {
                    // 기존 탭 이름이 영어(Block/Machine/Resource)라 도구 탭과 어긋나 있었다.
                    touched.Add($"- `{id}` 표시 이름 '{category.displayName}' → '{korean}'");
                    category.displayName = korean;
                    category.sortOrder = sortOrder;
                    EditorUtility.SetDirty(category);
                }

                result[id] = category;
            }

            if (touched.Count > 0)
            {
                report.AppendLine("## 카테고리 에셋");
                report.AppendLine();
                foreach (string line in touched) report.AppendLine(line);
                report.AppendLine();
            }
            return result;
        }
    }
}
