using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectCraft.EditorTools
{
    /// <summary>
    /// 두 벌로 갈라져 있던 레시피 트리를 하나로 합친다.
    /// <c>Recipes/Notion</c>(최신 기획본)을 <c>Recipes/Incomplete</c>(구 임포트본)로 흡수시키고 Notion 폴더를 없앤다.
    ///
    /// 같은 레시피인지는 <b>(기계 이름, 대표 산출물)</b> 로 판정한다. 기계 이름은 두 트리 사이에 개편이 있어
    /// <see cref="MachineAlias"/> 로 구 이름을 신 이름에 맞춘 뒤 비교한다.
    /// 겹치는 레시피의 <b>필요 재료는 합집합</b>이 되어 어느 쪽 정보도 잃지 않는다.
    ///
    /// 살아남는 쪽은 Notion 에셋이다 — 현재 딕셔너리에 등록된 레시피가 전부 Notion 에셋이고,
    /// <see cref="AssetDatabase.MoveAsset"/> 는 GUID 를 보존하므로 옮겨도 참조가 깨지지 않는다.
    ///
    /// 대화상자를 띄우지 않는다. 재실행하면 Notion 폴더가 없으므로 아무 일도 하지 않는다.
    /// </summary>
    public static class RecipeTreeMerger
    {
        private const string NotionFolder = "Assets/Prefabs/Recipes/Notion";
        private const string TargetFolder = "Assets/Prefabs/Recipes/Incomplete";
        private const string ReportPath = "Assets/Prefabs/Recipes/_MergeReport.md";

        /// <summary>구 Incomplete 기계 어휘 → Notion 어휘. Notion 개편 때의 이름 변경·통합 내역이다.</summary>
        private static readonly string[,] MachineAlias =
        {
            { "용광로", "화로" },
            { "유리 제조기", "화로" },
            { "가공대", "조합대" },
            { "철근 공장", "조합대" },
            { "파이프 공장", "조합대" },
            { "파이프 공장 (2티어 업그레이드)", "조합대" },
            { "망치", "조합대" },
            { "수동 0-0티어 추출기", "조합대" },
            { "수전해기", "전기 분해기" },
            { "벽돌 공장", "압연기" },
            { "벽돌 공장 (1티어 업그레이드)", "시멘트 공장" },
            { "파이프 공장 (1티어 업그레이드)", "유리 가공기" },
            { "화력발전소", "화력 발전기" },
            { "화력발전소 (1티어 업그레이드)", "화력 발전기" },
        };

        /// <summary>키가 같은 레시피들의 묶음.</summary>
        private class Group
        {
            public string key;
            public Recipe notion;                       // Notion 쪽 대표(있으면 이쪽이 생존)
            public readonly List<Recipe> incomplete = new List<Recipe>();
            public readonly List<Recipe> extraNotion = new List<Recipe>();   // 같은 키의 Notion 이 둘 이상일 때

            /// <summary>흡수되어 비워지는 Incomplete 에셋의 경로. 생존 에셋이 이 자리를 물려받는다.</summary>
            public string slot;
        }

        private static readonly StringBuilder Report = new StringBuilder();

        [MenuItem("Tools/Project Craft/Recipes/Merge Notion Into Incomplete")]
        public static void Merge()
        {
            if (!AssetDatabase.IsValidFolder(NotionFolder))
            {
                Debug.Log("[RecipeTreeMerger] Notion 폴더가 없습니다. 이미 통합된 상태입니다.");
                return;
            }

            Report.Clear();
            Report.AppendLine("# 레시피 트리 통합 보고서");
            Report.AppendLine();
            Report.AppendLine("`Tools/Project Craft/Recipes/Merge Notion Into Incomplete` 가 자동 생성한 파일입니다.");
            Report.AppendLine();

            List<Recipe> notion = LoadFolder(NotionFolder);
            List<Recipe> incomplete = LoadFolder(TargetFolder);
            Report.AppendLine($"- 시작: Notion {notion.Count}개 / Incomplete {incomplete.Count}개");

            List<Group> groups = BuildGroups(notion, incomplete);

            MergeFields(groups);
            AssetDatabase.SaveAssets();

            DeleteAbsorbed(groups);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            MoveSurvivors(groups);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RemoveNotionFolder();

            Report.AppendLine();
            Report.AppendLine($"- 최종: `{TargetFolder}` 아래 레시피 {LoadFolder(TargetFolder).Count}개");

            File.WriteAllText(ReportPath, Report.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ReportPath);
            AssetDatabase.Refresh();

            Debug.Log("[RecipeTreeMerger] 완료. 자세한 내역은 " + ReportPath);
        }

        /// <summary>
        /// 트리 하나 안에 남은 중복을 합친다. 기계가 나중에 배정되면서
        /// 서로 다른 JSON 에서 온 같은 레시피가 같은 기계·같은 산출물로 겹치는 일이 생긴다
        /// (예: `crafting/furnace` 와 `machines/build_furnace`).
        /// 재료는 여기서도 합집합이고, 노션 원본에서 온 쪽을 남긴다.
        /// </summary>
        [MenuItem("Tools/Project Craft/Recipes/Merge Duplicate Recipes")]
        public static void MergeDuplicates()
        {
            Report.Clear();
            Report.AppendLine("# 중복 레시피 병합 보고서");
            Report.AppendLine();
            Report.AppendLine("`Tools/Project Craft/Recipes/Merge Duplicate Recipes` 가 자동 생성한 파일입니다.");
            Report.AppendLine();

            List<Recipe> all = LoadFolder(TargetFolder);
            Dictionary<string, List<Recipe>> byKey = new Dictionary<string, List<Recipe>>();

            foreach (Recipe recipe in all)
            {
                // 기계와 산출물이 둘 다 확정된 것만 중복 판정 대상이다.
                if (recipe.machine == null || recipe.PrimaryOutput == null) continue;

                string key = recipe.MachineBlockId + "|" + recipe.PrimaryOutput.itemName;
                if (!byKey.TryGetValue(key, out List<Recipe> list))
                {
                    list = new List<Recipe>();
                    byKey[key] = list;
                }
                list.Add(recipe);
            }

            int mergedGroups = 0, absorbedCount = 0;
            List<string> lines = new List<string>();

            foreach (KeyValuePair<string, List<Recipe>> pair in byKey)
            {
                if (pair.Value.Count < 2) continue;
                mergedGroups++;

                Recipe survivor = PickSurvivor(pair.Value);
                foreach (Recipe other in pair.Value)
                {
                    if (other == survivor) continue;

                    UnionInputs(survivor, other);
                    UnionTools(survivor, other);
                    FillMissingScalars(survivor, other);
                    survivor.importNote = MergeNote(survivor, other);

                    lines.Add($"- `{AssetDatabase.GetAssetPath(other)}` → `{AssetDatabase.GetAssetPath(survivor)}` 로 흡수");
                    absorbedCount++;
                }
                EditorUtility.SetDirty(survivor);
            }
            AssetDatabase.SaveAssets();

            // 흡수된 것들을 지운다(위에서 survivor 로 뽑히지 않은 것 전부).
            foreach (KeyValuePair<string, List<Recipe>> pair in byKey)
            {
                if (pair.Value.Count < 2) continue;

                Recipe survivor = PickSurvivor(pair.Value);
                foreach (Recipe other in pair.Value)
                    if (other != survivor) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(other));
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Report.AppendLine($"- 중복 그룹 {mergedGroups}개 · 흡수·삭제한 에셋 {absorbedCount}개");
            Report.AppendLine($"- 남은 레시피 {LoadFolder(TargetFolder).Count}개");
            if (lines.Count > 0)
            {
                Report.AppendLine();
                Report.AppendLine("## 흡수 목록");
                Report.AppendLine();
                foreach (string line in lines) Report.AppendLine(line);
            }

            File.WriteAllText(DuplicateReportPath, Report.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(DuplicateReportPath);
            Debug.Log("[RecipeTreeMerger] 중복 병합 완료. 자세한 내역은 " + DuplicateReportPath);
        }

        private const string DuplicateReportPath = "Assets/Prefabs/Recipes/_DuplicateMergeReport.md";

        /// <summary>노션 원본에서 온 쪽을 남긴다(최신 기획본). 없으면 첫 번째.</summary>
        private static Recipe PickSurvivor(List<Recipe> group)
        {
            foreach (Recipe recipe in group)
                if ((recipe.importNote ?? "").Contains("[출처] notion_")) return recipe;
            return group[0];
        }

        // ── 묶기 ──────────────────────────────────────────────────
        private static List<Group> BuildGroups(List<Recipe> notion, List<Recipe> incomplete)
        {
            Dictionary<string, Group> byKey = new Dictionary<string, Group>();
            List<Group> ordered = new List<Group>();

            // Notion 을 먼저 넣어 각 그룹의 생존 에셋을 확정한다.
            foreach (Recipe recipe in notion)
            {
                Group group = Take(byKey, ordered, KeyOf(recipe, false), recipe);
                if (group.notion == null) group.notion = recipe;
                else group.extraNotion.Add(recipe);
            }
            foreach (Recipe recipe in incomplete)
                Take(byKey, ordered, KeyOf(recipe, true), recipe).incomplete.Add(recipe);

            return ordered;
        }

        private static Group Take(Dictionary<string, Group> byKey, List<Group> ordered, string key, Recipe recipe)
        {
            // 산출물이 없어 키를 만들 수 없는 레시피는 자기 자신만의 그룹이 된다(병합 대상이 아님).
            if (key == null) key = "@" + AssetDatabase.GetAssetPath(recipe);

            if (!byKey.TryGetValue(key, out Group group))
            {
                group = new Group { key = key };
                byKey[key] = group;
                ordered.Add(group);
            }
            return group;
        }

        /// <summary>(기계 이름, 대표 산출물). 산출물이 없으면 null.</summary>
        private static string KeyOf(Recipe recipe, bool applyAlias)
        {
            if (recipe.PrimaryOutput == null) return null;

            string machine = MachineNameOf(recipe);
            if (applyAlias) machine = Normalize(machine);
            return machine + "|" + recipe.PrimaryOutput.itemName;
        }

        /// <summary>
        /// 이 레시피가 어느 기계 것인지. MachineBlock 이 아직 없어 <c>machine</c> 이 비어 있어도
        /// importNote 의 <c>[기계] '이름'</c> 줄에 원본 이름이 남아 있다.
        /// </summary>
        private static string MachineNameOf(Recipe recipe)
        {
            if (recipe.machine != null) return recipe.machine.DisplayName;

            string note = recipe.importNote;
            if (string.IsNullOrEmpty(note)) return "";

            const string marker = "[기계] '";
            int start = note.IndexOf(marker);
            if (start < 0) return "";

            start += marker.Length;
            int end = note.IndexOf('\'', start);
            return end > start ? note.Substring(start, end - start) : "";
        }

        private static string Normalize(string machine)
        {
            for (int i = 0; i < MachineAlias.GetLength(0); i++)
                if (MachineAlias[i, 0] == machine) return MachineAlias[i, 1];
            return machine;
        }

        // ── 필드 병합 ─────────────────────────────────────────────
        private static void MergeFields(List<Group> groups)
        {
            int merged = 0;
            List<string> enriched = new List<string>();   // 재료가 실제로 늘어난 것

            foreach (Group group in groups)
            {
                Recipe survivor = Survivor(group);
                List<Recipe> absorbed = Absorbed(group, survivor);
                if (absorbed.Count == 0) continue;

                merged++;
                string before = DescribeInputs(survivor);

                foreach (Recipe other in absorbed)
                {
                    UnionInputs(survivor, other);
                    UnionTools(survivor, other);
                    FillMissingScalars(survivor, other);
                    survivor.importNote = MergeNote(survivor, other);
                }

                string after = DescribeInputs(survivor);
                if (before != after)
                    enriched.Add($"- **{survivor.PrimaryOutput.DisplayName}** [{Normalize(MachineNameOf(survivor))}]\n"
                        + $"  - 전: {before}\n  - 후: {after}");

                EditorUtility.SetDirty(survivor);
            }

            Report.AppendLine($"- 병합한 그룹 {merged}개, 그중 재료가 늘어난 것 {enriched.Count}개");
            if (enriched.Count > 0)
            {
                Report.AppendLine();
                Report.AppendLine("## 재료가 합집합으로 늘어난 레시피");
                Report.AppendLine();
                foreach (string line in enriched) Report.AppendLine(line);
            }
        }

        private static Recipe Survivor(Group group)
            => group.notion != null ? group.notion : (group.incomplete.Count > 0 ? group.incomplete[0] : null);

        private static List<Recipe> Absorbed(Group group, Recipe survivor)
        {
            List<Recipe> result = new List<Recipe>();
            foreach (Recipe recipe in group.incomplete) if (recipe != survivor) result.Add(recipe);
            foreach (Recipe recipe in group.extraNotion) if (recipe != survivor) result.Add(recipe);
            return result;
        }

        /// <summary>재료 합집합. 같은 아이템은 많은 쪽 개수를 쓴다.</summary>
        private static void UnionInputs(Recipe survivor, Recipe other)
        {
            if (other.inputs == null) return;
            if (survivor.inputs == null) survivor.inputs = new List<ItemStack>();

            foreach (ItemStack need in other.inputs)
            {
                if (need == null || need.item == null || need.count <= 0) continue;

                ItemStack existing = survivor.inputs.Find(s => s != null && s.item == need.item);
                if (existing == null) survivor.inputs.Add(new ItemStack { item = need.item, count = need.count });
                else if (need.count > existing.count) existing.count = need.count;
            }
        }

        /// <summary>필요 도구 합집합. 같은 도구는 내구도 소모가 큰 쪽을 쓴다.</summary>
        private static void UnionTools(Recipe survivor, Recipe other)
        {
            if (other.requiredTools == null) return;
            if (survivor.requiredTools == null) survivor.requiredTools = new List<ToolRequirement>();

            foreach (ToolRequirement need in other.requiredTools)
            {
                if (need == null || need.tool == null) continue;

                ToolRequirement existing = survivor.requiredTools.Find(t => t != null && t.tool == need.tool);
                if (existing == null)
                    survivor.requiredTools.Add(new ToolRequirement { tool = need.tool, durabilityCost = need.durabilityCost });
                else if (need.durabilityCost > existing.durabilityCost)
                    existing.durabilityCost = need.durabilityCost;
            }
        }

        /// <summary>
        /// 스칼라 필드는 Notion(=생존 에셋) 값을 그대로 둔다.
        /// 다만 생존 쪽이 <b>비어 있는</b> 값은 흡수되는 쪽에서 채운다 — 특히 machine 참조.
        /// </summary>
        private static void FillMissingScalars(Recipe survivor, Recipe other)
        {
            if (survivor.machine == null && other.machine != null) survivor.machine = other.machine;
            if (survivor.category == null && other.category != null) survivor.category = other.category;
            if (!HasOutput(survivor) && HasOutput(other)) survivor.outputs = new List<ItemStack>(other.outputs);
        }

        private static bool HasOutput(Recipe recipe)
        {
            if (recipe.outputs == null) return false;
            foreach (ItemStack stack in recipe.outputs)
                if (stack != null && stack.item != null && stack.count > 0) return true;
            return false;
        }

        private static string MergeNote(Recipe survivor, Recipe other)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("[병합] " + AssetDatabase.GetAssetPath(other) + " 를 흡수함");
            builder.Append(survivor.importNote);
            if (!string.IsNullOrEmpty(other.importNote))
            {
                builder.AppendLine();
                builder.AppendLine("--- 흡수된 원본 ---");
                builder.Append(other.importNote);
            }
            return builder.ToString();
        }

        private static string DescribeInputs(Recipe recipe)
        {
            if (recipe.inputs == null || recipe.inputs.Count == 0) return "(없음)";

            List<string> parts = new List<string>();
            foreach (ItemStack stack in recipe.inputs)
                if (stack != null && stack.item != null) parts.Add(stack.item.DisplayName + " x" + stack.count);
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "(없음)";
        }

        // ── 흡수된 에셋 삭제 ──────────────────────────────────────
        private static void DeleteAbsorbed(List<Group> groups)
        {
            int deleted = 0;
            List<string> lines = new List<string>();

            foreach (Group group in groups)
            {
                Recipe survivor = Survivor(group);
                foreach (Recipe recipe in Absorbed(group, survivor))
                {
                    string path = AssetDatabase.GetAssetPath(recipe);

                    // 첫 번째로 비워지는 Incomplete 자리를 생존 에셋이 물려받는다(폴더 구성을 그대로 유지).
                    if (string.IsNullOrEmpty(group.slot) && path.StartsWith(TargetFolder + "/")) group.slot = path;

                    lines.Add($"- `{path}` → `{AssetDatabase.GetAssetPath(survivor)}` 로 흡수");
                    if (AssetDatabase.DeleteAsset(path)) deleted++;
                }
            }

            Report.AppendLine($"- 흡수되어 삭제한 에셋 {deleted}개");
            if (lines.Count > 0)
            {
                Report.AppendLine();
                Report.AppendLine("## 흡수·삭제 목록");
                Report.AppendLine();
                foreach (string line in lines) Report.AppendLine(line);
            }
        }

        // ── 생존 에셋 이동 ────────────────────────────────────────
        /// <summary>
        /// Notion 에 남아 있는 생존 에셋을 Incomplete 로 옮긴다.
        /// 흡수한 Incomplete 에셋이 있었으면 그 자리를, 없으면 같은 이름의 하위 폴더를 쓴다.
        /// </summary>
        private static void MoveSurvivors(List<Group> groups)
        {
            int moved = 0;
            List<string> clashes = new List<string>();
            List<string> failures = new List<string>();

            foreach (Group group in groups)
            {
                Recipe survivor = Survivor(group);
                if (survivor == null) continue;

                string from = AssetDatabase.GetAssetPath(survivor);
                if (!from.StartsWith(NotionFolder + "/")) continue;   // 이미 Incomplete 에 있음

                string target = group.slot;
                if (string.IsNullOrEmpty(target))
                {
                    // Notion 단독 — 하위 폴더 이름을 그대로 유지한다(machines/ · magic/ 은 새로 만들어진다).
                    string relative = from.Substring(NotionFolder.Length + 1);
                    target = TargetFolder + "/" + relative;
                }

                EnsureFolder(Path.GetDirectoryName(target).Replace('\\', '/'));

                string unique = MakeUnique(target);
                if (unique != target) clashes.Add($"- `{from}` → 이름 충돌로 `{unique}`");

                string error = AssetDatabase.MoveAsset(from, unique);
                if (string.IsNullOrEmpty(error)) moved++;
                else failures.Add($"- `{from}` → `{unique}` 실패: {error}");
            }

            Report.AppendLine($"- Incomplete 로 옮긴 에셋 {moved}개 (이름 충돌 {clashes.Count}건)");
            if (clashes.Count > 0)
            {
                Report.AppendLine();
                Report.AppendLine("## 이름 충돌로 번호를 붙인 에셋");
                Report.AppendLine();
                foreach (string line in clashes) Report.AppendLine(line);
            }
            if (failures.Count > 0)
            {
                Report.AppendLine();
                Report.AppendLine("## ⚠ 이동 실패");
                Report.AppendLine();
                foreach (string line in failures) Report.AppendLine(line);
            }
        }

        /// <summary>이미 있는 경로면 _2, _3 … 을 붙인다.</summary>
        private static string MakeUnique(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) == null) return path;

            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(path);
            for (int i = 2; i < 100; i++)
            {
                string candidate = $"{folder}/{name}_{i}.asset";
                if (AssetDatabase.LoadAssetAtPath<Object>(candidate) == null) return candidate;
            }
            return path;
        }

        private static void RemoveNotionFolder()
        {
            List<Recipe> left = LoadFolder(NotionFolder);
            if (left.Count > 0)
            {
                Report.AppendLine();
                Report.AppendLine($"## ⚠ Notion 폴더에 {left.Count}개가 남아 삭제하지 않았습니다");
                Report.AppendLine();
                foreach (Recipe recipe in left) Report.AppendLine($"- `{AssetDatabase.GetAssetPath(recipe)}`");
                Debug.LogWarning("[RecipeTreeMerger] Notion 폴더가 비지 않아 삭제하지 않았습니다. 보고서를 확인하세요.");
                return;
            }

            if (AssetDatabase.DeleteAsset(NotionFolder))
                Report.AppendLine($"- `{NotionFolder}` 폴더 삭제");
            else
                Report.AppendLine($"- ⚠ `{NotionFolder}` 폴더를 지우지 못했습니다(수동 삭제 필요).");
        }

        // ── 공용 ──────────────────────────────────────────────────
        private static List<Recipe> LoadFolder(string folder)
        {
            List<Recipe> result = new List<Recipe>();
            if (!AssetDatabase.IsValidFolder(folder)) return result;

            foreach (string guid in AssetDatabase.FindAssets("t:Recipe", new[] { folder }))
            {
                Recipe recipe = AssetDatabase.LoadAssetAtPath<Recipe>(AssetDatabase.GUIDToAssetPath(guid));
                if (recipe != null) result.Add(recipe);
            }
            return result;
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
