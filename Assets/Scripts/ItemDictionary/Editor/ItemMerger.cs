using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectCraft.EditorTools
{
    /// <summary>
    /// <see cref="ItemAliases"/> 표를 근거로 중복 아이템을 실제로 합친다.
    ///
    /// 하는 일은 <see cref="ToolAssetGenerator"/> 의 중복 플레이스홀더 정리와 같다 —
    /// <b>참조를 먼저 정본으로 갈아 끼우고, 그러고도 참조가 0 인 것만 지운다.</b>
    /// 반대 순서로 하면 레시피에 <c>{fileID: 0}</c> 이 남아 조용히 재료 한 줄이 사라진다.
    ///
    /// 재실행 안전하다. 이미 합쳐진 표는 "건너뜀" 으로 보고하고 아무것도 하지 않는다.
    /// </summary>
    public static class ItemMerger
    {
        private const string ReportPath = "Assets/Prefabs/Items/_MergeReport.md";

        private static readonly StringBuilder Report = new StringBuilder();

        [MenuItem("Tools/Project Craft/Dictionary/중복 아이템 통합")]
        public static void Run()
        {
            Report.Clear();
            Report.AppendLine("# 중복 아이템 통합");
            Report.AppendLine();
            Report.AppendLine("`Tools/Project Craft/Dictionary/중복 아이템 통합` 이 자동 생성한 파일입니다.");
            Report.AppendLine("근거는 `ItemAliases` 표이고, 표를 고치면 이 툴을 다시 돌리면 됩니다.");
            Report.AppendLine();

            Dictionary<string, Items> byName = LoadItemsByName();

            // 1) 정본이 아직 없는 짝은 '이름 변경' 이다. 통합보다 먼저 해야 아래에서 정본이 찾아진다.
            int renamed = RenameMissingTargets(byName);
            if (renamed > 0)
            {
                AssetDatabase.SaveAssets();
                byName = LoadItemsByName();
            }

            // 2) 옛 아이템 → 정본 아이템 짝을 확정한다.
            Dictionary<Items, Items> replacement = BuildReplacements(byName);
            if (replacement.Count == 0)
            {
                Report.AppendLine("합칠 것이 없습니다(이미 정리됨).");
                Finish();
                return;
            }

            // 3) 참조 재작성 → 저장 → 그 다음에만 삭제.
            int recipes = RewriteRecipes(replacement);
            int blocks = RewriteBlocks(replacement);
            AssetDatabase.SaveAssets();

            int deleted = DeleteUnreferenced(replacement);
            ReportSelfRecipes(replacement);

            Report.AppendLine();
            Report.AppendLine($"- 이름 변경 {renamed}개 · 레시피 {recipes}개 수정 · 블록 {blocks}개 수정 · 아이템 {deleted}개 삭제");

            Finish();
        }

        private static void Finish()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Report.AppendLine();
            Report.AppendLine("딕셔너리 등록을 이어서 돌립니다 (Register All Assets).");
            File.WriteAllText(ReportPath, Report.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ReportPath);
            Debug.Log("[ItemMerger] 완료. 자세한 내역은 " + ReportPath);

            EditorApplication.ExecuteMenuItem("Tools/Project Craft/Dictionary/Register All Assets");
        }

        // ── 색인 ─────────────────────────────────────────────────

        /// <summary>한글이 섞여 있는가. 영문 ID 로의 개명과 한글 표기 다듬기를 가르는 데 쓴다.</summary>
        private static bool HasHangul(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < value.Length; i++)
                if (value[i] >= 0xAC00 && value[i] <= 0xD7A3) return true;
            return false;
        }

        private static Dictionary<string, Items> LoadItemsByName()
        {
            Dictionary<string, Items> result = new Dictionary<string, Items>();
            foreach (string guid in AssetDatabase.FindAssets("t:Items"))
            {
                Items item = AssetDatabase.LoadAssetAtPath<Items>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null || string.IsNullOrEmpty(item.itemName)) continue;

                string key = ItemDictionary.NormalizeName(item.itemName);
                if (!result.ContainsKey(key)) result[key] = item;
            }
            return result;
        }

        // ── 1) 이름 변경 ──────────────────────────────────────────

        /// <summary>
        /// 정본이 아직 없는 짝은 합칠 상대가 없다는 뜻이다 — 표기만 다듬는 경우(`인바(invar)` → `인바`)라
        /// 옛 아이템의 이름을 정본 이름으로 바꿔 준다. 세이브 호환은 <see cref="ItemAliases"/> 폴백이 맡는다.
        /// </summary>
        private static int RenameMissingTargets(Dictionary<string, Items> byName)
        {
            List<string> lines = new List<string>();

            foreach (KeyValuePair<string, string> pair in ItemAliases.All)
            {
                if (!byName.TryGetValue(pair.Key, out Items source)) continue;   // 옛 이름이 이미 없다
                if (byName.ContainsKey(pair.Value)) continue;                    // 정본이 있다 → 통합 대상

                string path = AssetDatabase.GetAssetPath(source);
                source.itemName = pair.Value;

                // 표시 이름은 <b>한글이어야 한다</b>. 영문 ID 로 바꾸는 경우 표시명까지 따라가면
                // UI 에 영문이 뜬다 — 비어 있을 때만 옛 이름(한글)을 표시명으로 옮긴다.
                if (string.IsNullOrEmpty(source.displayName)) source.displayName = pair.Key;
                else if (HasHangul(pair.Value) && source.displayName == pair.Key) source.displayName = pair.Value;
                EditorUtility.SetDirty(source);

                string error = AssetDatabase.RenameAsset(path, pair.Value);
                lines.Add($"- `{pair.Key}` → `{pair.Value}` (에셋 이름 변경{(string.IsNullOrEmpty(error) ? "" : " 실패: " + error)})");
            }

            if (lines.Count > 0)
            {
                Report.AppendLine("## 이름 변경 (정본이 없어 합칠 상대가 없던 것)");
                Report.AppendLine();
                foreach (string line in lines) Report.AppendLine(line);
                Report.AppendLine();
            }
            return lines.Count;
        }

        // ── 2) 짝 확정 ────────────────────────────────────────────

        private static Dictionary<Items, Items> BuildReplacements(Dictionary<string, Items> byName)
        {
            Dictionary<Items, Items> result = new Dictionary<Items, Items>();

            Report.AppendLine("## 통합 대상");
            Report.AppendLine();
            Report.AppendLine("| 옛 이름 | 정본 | 상태 |");
            Report.AppendLine("|---|---|---|");

            foreach (KeyValuePair<string, string> pair in ItemAliases.All)
            {
                bool hasSource = byName.TryGetValue(pair.Key, out Items source);
                bool hasTarget = byName.TryGetValue(pair.Value, out Items target);

                if (!hasSource)
                {
                    Report.AppendLine($"| `{pair.Key}` | `{pair.Value}` | 건너뜀 — 옛 아이템이 이미 없음 |");
                    continue;
                }
                if (!hasTarget)
                {
                    Report.AppendLine($"| `{pair.Key}` | `{pair.Value}` | ⚠ 정본을 찾지 못함 |");
                    continue;
                }
                if (source == target)
                {
                    Report.AppendLine($"| `{pair.Key}` | `{pair.Value}` | 건너뜀 — 이름 변경으로 이미 같아짐 |");
                    continue;
                }

                result[source] = target;
                Report.AppendLine($"| `{pair.Key}` | `{pair.Value}` | 통합 |");
            }
            Report.AppendLine();
            return result;
        }

        // ── 3) 참조 재작성 ────────────────────────────────────────

        private static int RewriteRecipes(Dictionary<Items, Items> replacement)
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Recipe"))
            {
                Recipe recipe = AssetDatabase.LoadAssetAtPath<Recipe>(AssetDatabase.GUIDToAssetPath(guid));
                if (recipe == null) continue;

                bool touched = ReplaceIn(recipe.inputs, replacement);
                touched |= ReplaceIn(recipe.outputs, replacement);
                if (!touched) continue;

                EditorUtility.SetDirty(recipe);
                changed++;
            }
            return changed;
        }

        /// <summary>
        /// 목록의 아이템 참조를 정본으로 바꾸고, 그 결과 같은 아이템이 두 줄이 되면 개수를 합친다.
        /// 합치지 않으면 "돌 2 + 돌 4" 처럼 한 레시피에 같은 재료가 두 줄로 남는다.
        /// </summary>
        private static bool ReplaceIn(List<ItemStack> stacks, Dictionary<Items, Items> replacement)
        {
            if (stacks == null) return false;

            bool touched = false;
            for (int i = 0; i < stacks.Count; i++)
            {
                ItemStack stack = stacks[i];
                if (stack == null || stack.item == null) continue;
                if (!replacement.TryGetValue(stack.item, out Items canonical)) continue;

                stack.item = canonical;
                touched = true;
            }
            if (!touched) return false;

            for (int i = stacks.Count - 1; i > 0; i--)
            {
                ItemStack later = stacks[i];
                if (later == null || later.item == null || later.instance != null) continue;

                for (int j = 0; j < i; j++)
                {
                    ItemStack earlier = stacks[j];
                    if (earlier == null || earlier.item != later.item || earlier.instance != null) continue;

                    earlier.count += later.count;
                    stacks.RemoveAt(i);
                    break;
                }
            }
            return true;
        }

        /// <summary>
        /// 지형·파이프의 배치 역인덱스가 <see cref="BlockBase.dropItem"/> 에 걸려 있어 함께 옮긴다.
        /// <b>아이템을 지우는 다른 툴도 이걸 써야 한다</b>(MachineBlockFiller) — 안 그러면 dropItem 이 끊긴다.
        /// </summary>
        internal static int RewriteBlocks(Dictionary<Items, Items> replacement)
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:BlockBase"))
            {
                BlockBase block = AssetDatabase.LoadAssetAtPath<BlockBase>(AssetDatabase.GUIDToAssetPath(guid));
                if (block == null || block.dropItem == null) continue;
                if (!replacement.TryGetValue(block.dropItem, out Items canonical)) continue;

                block.dropItem = canonical;
                EditorUtility.SetDirty(block);
                changed++;
            }
            return changed;
        }

        // ── 4) 삭제 ──────────────────────────────────────────────

        /// <summary>참조가 남아 있으면 지우지 않고 보고한다. 조용히 지우면 재료 한 줄이 사라진다.</summary>
        private static int DeleteUnreferenced(Dictionary<Items, Items> replacement)
        {
            HashSet<Items> stillUsed = CollectReferenced();

            Report.AppendLine("## 삭제");
            Report.AppendLine();

            int deleted = 0;
            foreach (KeyValuePair<Items, Items> pair in replacement)
            {
                string path = AssetDatabase.GetAssetPath(pair.Key);
                if (stillUsed.Contains(pair.Key))
                {
                    Report.AppendLine($"- ⚠ `{path}` 는 아직 참조가 남아 지우지 않았습니다.");
                    continue;
                }

                Report.AppendLine($"- `{path}` 삭제 → `{pair.Value.itemName}` 로 대체");
                if (AssetDatabase.DeleteAsset(path)) deleted++;
            }
            return deleted;
        }

        /// <summary>
        /// 통합이 만들어 낸 <b>자기 자신을 만드는 레시피</b>를 알려 준다.
        /// `암석 → 돌` 처럼 서로 다른 두 아이템을 잇던 레시피는 둘을 합치는 순간 뜻을 잃는다.
        /// 자동으로 지우지 않는다 — 재료 비율을 바꿔 살릴지 버릴지는 사람이 정할 일이다.
        ///
        /// <b>이번 통합이 만든 것만</b> 센다. 복제 레시피(마력 파편 → 마력 파편)처럼
        /// 원래부터 입출력이 같은 것은 정상이라 걸러 낸다.
        /// </summary>
        private static void ReportSelfRecipes(Dictionary<Items, Items> replacement)
        {
            HashSet<Items> merged = new HashSet<Items>(replacement.Values);
            List<string> lines = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Recipe"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Recipe recipe = AssetDatabase.LoadAssetAtPath<Recipe>(path);
                if (recipe == null || recipe.inputs == null || recipe.outputs == null) continue;

                HashSet<Items> inputs = new HashSet<Items>();
                Collect(recipe.inputs, inputs);

                for (int i = 0; i < recipe.outputs.Count; i++)
                {
                    ItemStack produce = recipe.outputs[i];
                    if (produce == null || produce.item == null || !inputs.Contains(produce.item)) continue;
                    if (!merged.Contains(produce.item)) continue;   // 원래부터 같았던 복제 레시피

                    lines.Add($"- `{path}` — `{produce.item.itemName}` 이 입력이자 산출");
                    break;
                }
            }

            if (lines.Count == 0) return;

            Report.AppendLine();
            Report.AppendLine("## ⚠ 입력과 산출이 같아진 레시피 (사람이 판단할 것)");
            Report.AppendLine();
            Report.AppendLine("서로 다른 두 아이템을 잇던 레시피가 통합으로 뜻을 잃은 것입니다. 지우지 않았습니다.");
            Report.AppendLine();
            foreach (string line in lines) Report.AppendLine(line);
        }

        /// <summary>
        /// 지금 어디선가 참조되고 있는 아이템 전부(레시피 재료·산출 + 블록 dropItem).
        /// <b>아이템을 지우는 툴은 반드시 이걸로 먼저 확인한다</b> — 참조가 남은 채 지우면
        /// 그 자리에 <c>{fileID: 0}</c> 이 남아 재료 한 줄이 조용히 사라진다.
        /// </summary>
        internal static HashSet<Items> CollectReferenced()
        {
            HashSet<Items> result = new HashSet<Items>();

            foreach (string guid in AssetDatabase.FindAssets("t:Recipe"))
            {
                Recipe recipe = AssetDatabase.LoadAssetAtPath<Recipe>(AssetDatabase.GUIDToAssetPath(guid));
                if (recipe == null) continue;
                Collect(recipe.inputs, result);
                Collect(recipe.outputs, result);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:BlockBase"))
            {
                BlockBase block = AssetDatabase.LoadAssetAtPath<BlockBase>(AssetDatabase.GUIDToAssetPath(guid));
                if (block != null && block.dropItem != null) result.Add(block.dropItem);
            }
            return result;
        }

        private static void Collect(List<ItemStack> stacks, HashSet<Items> into)
        {
            if (stacks == null) return;
            for (int i = 0; i < stacks.Count; i++)
                if (stacks[i] != null && stacks[i].item != null) into.Add(stacks[i].item);
        }
    }
}
