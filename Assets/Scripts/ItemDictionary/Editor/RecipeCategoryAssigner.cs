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
    /// <b>⚠ 이 표가 카테고리의 유일한 정본이다 — 돌릴 때마다 전부 다시 계산한다.</b>
    /// 예전에는 "이미 지정돼 있으면 건드리지 않는다" 였는데, 그래서 JSON 임포터가 붙여 둔 옛 값이
    /// 영영 남았다(실측: 기계 12개·파이프 6개가 '자원' 탭에 있었다). 손으로 고친 카테고리도 덮이므로,
    /// 규칙으로 표현되지 않는 것은 아래 <see cref="Overrides"/> 표에 줄을 더한다.
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

        /// <summary>
        /// 산출물 <c>itemName</c> → 카테고리. <b>규칙(<see cref="Classify"/>)으로 표현되지 않는 것만</b> 적는다.
        /// 규칙이 대부분을 맞추므로(기계는 <c>Machine:*</c>, 파이프는 <c>placeable</c>, 부품은 타입) 여기는 짧아야 한다.
        /// </summary>
        private static readonly string[,] Overrides =
        {
            // 기계에 꽂는 물건이라 기계 탭에서 찾는 것이 자연스럽다(아이템이라 규칙으로는 '자원'이 된다).
            { "upgrade_speed",      "machine" },
            { "upgrade_efficiency", "machine" },
            // 들고 쓰는 물건. 배치물도 부품도 아니라 규칙으로는 '자원'이 된다.
            { "dowsing_rod",        "tool" },
            // ⚠ 아직 정식 기계가 아니라 이름에 'Machine:' 이 없다(ItemAliases 의 '미승격' 목록).
            //    정식 기계로 올리면 이 줄은 지워도 규칙이 알아서 맞춘다.
            { "auto_crafter",       "machine" },
            { "cavity_scanner",     "machine" },
            // 조명은 <c>LightBlock : MachineBlock</c> 이라 이름이 'Machine:' 으로 시작하지만,
            // 플레이어에게는 기계가 아니라 <b>깔아 두는 물건</b>이다.
            { "Machine:Torch",      "block" },
            { "Machine:Lamp",       "block" },
            // ⚠ 산성·유리 파이프는 <b>PipeBlock 에셋이 없어</b> placeable 이 꺼져 있다(미구현).
            //    규칙대로면 '자원'이 되지만 나머지 파이프 4종과 갈라 놓으면 찾기 어렵다.
            { "acid_pipe",          "block" },
            { "glass_pipe",         "block" },
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

            int changed = 0, same = 0, noOutput = 0;
            Dictionary<string, int> tally = new Dictionary<string, int>();
            List<string> unresolved = new List<string>();
            List<string> moved = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Recipe"))
            {
                Recipe recipe = AssetDatabase.LoadAssetAtPath<Recipe>(AssetDatabase.GUIDToAssetPath(guid));
                if (recipe == null) continue;

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

                tally.TryGetValue(key, out int count);
                tally[key] = count + 1;

                if (recipe.category == category) { same++; continue; }

                string before = recipe.category != null ? recipe.category.DisplayName : "(없음)";
                recipe.category = category;
                EditorUtility.SetDirty(recipe);
                changed++;
                moved.Add($"- `{recipe.name}` : {before} → {category.DisplayName}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.AppendLine("## 결과");
            report.AppendLine();
            report.AppendLine($"- 바뀐 것 {changed}개 · 그대로 {same}개 · 산출물이 없어 건너뜀 {noOutput}개");
            report.AppendLine();
            foreach (KeyValuePair<string, int> pair in tally)
                report.AppendLine($"- {pair.Key} : {pair.Value}개");

            if (moved.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("## 옮겨진 레시피");
                report.AppendLine();
                foreach (string line in moved) report.AppendLine(line);
            }

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
        /// <see cref="Overrides"/> 에 적힌 것은 규칙보다 우선한다.
        /// </summary>
        private static string Classify(Items output)
        {
            for (int i = 0; i < Overrides.GetLength(0); i++)
                if (output.itemName == Overrides[i, 0]) return Overrides[i, 1];

            if (output.itemName.StartsWith("Machine:") || output.itemName == "CoreCrafter") return "machine";

            // ⚠ <b>판만은 부품이 아니라 자원이다.</b> 재질마다 하나라는 <i>구조</i>가 같아서
            //    ToolPartItem 으로 만들었을 뿐, 도구에 꽂는 부품이 아니라 온갖 레시피가 먹는 중간재다
            //    (철판 하나를 20개 레시피가 쓴다). 타입만 보면 도구 탭으로 가 버린다.
            if (output is ToolPartItem plate && plate.kind != null && plate.kind.kindId == "plate") return "resource";

            // ⚠ <see cref="WrenchItem"/> 도 도구다 — ToolItem 을 상속하지 않고 Items 를 직접 상속해서
            //    (타입으로만 판정하려고 만든 클래스라) 여기 없으면 렌치가 '자원' 으로 떨어진다.
            if (output is ToolItem || output is ToolPartItem || output is WrenchItem) return "tool";
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
