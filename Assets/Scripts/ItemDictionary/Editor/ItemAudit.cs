using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectCraft.EditorTools
{
    /// <summary>
    /// 아이템 에셋 전수 조사. <b>아무것도 바꾸지 않는다.</b>
    ///
    /// 중복을 지우기 전에 "무엇이 무엇과 겹치는가"와 "그것이 어디서 쓰이는가"를 근거와 함께 남긴다.
    /// 통합을 돌린 뒤 다시 실행해 중복이 0이 됐는지 확인하는 <b>사후 검증 수단</b>이기도 하다.
    ///
    /// ⚠ 참조를 셀 때 <see cref="Recipe.importNote"/> 는 보지 않는다 — 원문 JSON 이 통째로 들어 있어
    /// 문자열로 세면 모든 아이템이 "참조됨" 으로 나온다. 그래서 YAML 이 아니라 <b>객체 참조</b>를 센다.
    /// </summary>
    public static class ItemAudit
    {
        private const string ReportPath = "Assets/Prefabs/Items/_AuditReport.md";
        private const string PlaceholderFolder = "Assets/Prefabs/Items/Placeholder";

        /// <summary>한 아이템의 조사 결과.</summary>
        private class Entry
        {
            public Items item;
            public string folder;
            public int registeredUses;   // RecipeDictionary 에 등록된 레시피가 쓰는 횟수
            public int incompleteUses;   // 그 밖의 레시피(Incomplete 등)
            public int blockUses;        // BlockBase.dropItem 등 블록이 거는 참조
            public int Total => registeredUses + incompleteUses + blockUses;
        }

        [MenuItem("Tools/Project Craft/Dictionary/아이템 중복 조사")]
        public static void Run()
        {
            Dictionary<Items, Entry> entries = CollectItems();
            HashSet<Recipe> registered = CollectRegisteredRecipes();
            CountUses(entries, registered);

            StringBuilder report = new StringBuilder();
            report.AppendLine("# 아이템 중복 조사");
            report.AppendLine();
            report.AppendLine("`Tools/Project Craft/Dictionary/아이템 중복 조사` 가 자동 생성한 파일입니다. 아무것도 바꾸지 않습니다.");
            report.AppendLine();

            WriteSummary(report, entries, registered);
            int duplicates = WriteDuplicates(report, entries);
            WriteUnused(report, entries);
            WritePending(report, entries);
            WriteContradictions(report);

            File.WriteAllText(ReportPath, report.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ReportPath);
            Debug.Log($"[ItemAudit] 아이템 {entries.Count}개 / 중복 후보 {duplicates}건. 리포트: {ReportPath}");
        }

        // ── 수집 ─────────────────────────────────────────────────

        private static Dictionary<Items, Entry> CollectItems()
        {
            Dictionary<Items, Entry> result = new Dictionary<Items, Entry>();
            foreach (string guid in AssetDatabase.FindAssets("t:Items"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Items item = AssetDatabase.LoadAssetAtPath<Items>(path);
                if (item == null || result.ContainsKey(item)) continue;

                result[item] = new Entry
                {
                    item = item,
                    folder = Path.GetFileName(Path.GetDirectoryName(path)),
                };
            }
            return result;
        }

        /// <summary>
        /// 씬의 RecipeDictionary 에 등록된 레시피. 등록된 것만이 실제로 게임에서 도는 레시피라,
        /// 여기 걸린 아이템은 지우면 바로 티가 난다.
        /// </summary>
        private static HashSet<Recipe> CollectRegisteredRecipes()
        {
            HashSet<Recipe> result = new HashSet<Recipe>();
            RecipeDictionary dictionary = Object.FindFirstObjectByType<RecipeDictionary>(FindObjectsInactive.Include);
            if (dictionary == null) return result;

            SerializedProperty list = new SerializedObject(dictionary).FindProperty("recipesList");
            if (list == null) return result;

            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is Recipe recipe) result.Add(recipe);
            return result;
        }

        private static void CountUses(Dictionary<Items, Entry> entries, HashSet<Recipe> registered)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Recipe"))
            {
                Recipe recipe = AssetDatabase.LoadAssetAtPath<Recipe>(AssetDatabase.GUIDToAssetPath(guid));
                if (recipe == null) continue;

                bool live = registered.Contains(recipe);
                CountStacks(entries, recipe.inputs, live);
                CountStacks(entries, recipe.outputs, live);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:BlockBase"))
            {
                BlockBase block = AssetDatabase.LoadAssetAtPath<BlockBase>(AssetDatabase.GUIDToAssetPath(guid));
                if (block == null || block.dropItem == null) continue;
                if (entries.TryGetValue(block.dropItem, out Entry entry)) entry.blockUses++;
            }
        }

        private static void CountStacks(Dictionary<Items, Entry> entries, List<ItemStack> stacks, bool live)
        {
            if (stacks == null) return;
            for (int i = 0; i < stacks.Count; i++)
            {
                ItemStack stack = stacks[i];
                if (stack == null || stack.item == null) continue;
                if (!entries.TryGetValue(stack.item, out Entry entry)) continue;

                if (live) entry.registeredUses++;
                else entry.incompleteUses++;
            }
        }

        // ── 중복 판정 ─────────────────────────────────────────────

        /// <summary>비교용 키. 공백·`(invar)` 같은 표기 흔들림을 지운다.</summary>
        private static string Key(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string value = ItemDictionary.NormalizeName(name);
            return value.Replace(" ", "").Replace("(invar)", "").Replace("(", "").Replace(")", "").ToLowerInvariant();
        }

        private static int WriteDuplicates(StringBuilder report, Dictionary<Items, Entry> entries)
        {
            report.AppendLine("## 중복 후보");
            report.AppendLine();

            int count = 0;    // ①+② — 기계적으로 확실한 중복
            int loose = 0;    // ③ — 오탐이 섞여 사람이 봐야 하는 것

            // ① 표시 이름이 정규화 후 같은 것끼리
            Dictionary<string, List<Entry>> byKey = new Dictionary<string, List<Entry>>();
            foreach (Entry entry in entries.Values)
            {
                string key = Key(entry.item.DisplayName);
                if (!byKey.TryGetValue(key, out List<Entry> list)) byKey[key] = list = new List<Entry>();
                list.Add(entry);
            }

            report.AppendLine("### ① 표시 이름이 같은 짝 (표기 흔들림)");
            report.AppendLine();
            report.AppendLine("| 키 | 아이템 | 폴더 | 쓰임(등록/미완/블록) |");
            report.AppendLine("|---|---|---|---|");
            foreach (KeyValuePair<string, List<Entry>> pair in byKey)
            {
                if (pair.Value.Count < 2) continue;
                count += pair.Value.Count - 1;
                foreach (Entry entry in pair.Value) report.AppendLine(Row(pair.Key, entry));
            }
            report.AppendLine();

            // ② 플레이스홀더 이름이 기계의 표시 이름과 같은 것
            Dictionary<string, MachineBlock> machines = new Dictionary<string, MachineBlock>();
            foreach (string guid in AssetDatabase.FindAssets("t:MachineBlock"))
            {
                MachineBlock block = AssetDatabase.LoadAssetAtPath<MachineBlock>(AssetDatabase.GUIDToAssetPath(guid));
                if (block == null) continue;
                machines[Key(block.DisplayName)] = block;
            }

            report.AppendLine("### ② 플레이스홀더 ↔ 이미 있는 기계 (이름이 정확히 같음)");
            report.AppendLine();
            report.AppendLine("| 플레이스홀더 | 겹치는 기계 | 쓰임 |");
            report.AppendLine("|---|---|---|");
            foreach (Entry entry in entries.Values)
            {
                if (entry.folder != "Placeholder") continue;
                if (!machines.TryGetValue(Key(entry.item.DisplayName), out MachineBlock block)) continue;
                count++;
                report.AppendLine($"| `{entry.item.itemName}` | `{block.blockName}` ({block.DisplayName}) | {Uses(entry)} |");
            }
            report.AppendLine();

            // ③ 한쪽 이름이 다른 쪽에 통째로 들어 있는 짝.
            //    "분쇄기 ↔ 전기 분쇄기" 처럼 ②가 못 잡는 진짜 중복을 여기서 잡는다.
            //    대신 "유리 ↔ 유리 가공기"(재료와 기계) 같은 오탐이 섞이므로 사람이 판단해야 한다.
            report.AppendLine("### ③ 이름이 포함 관계인 짝 — ⚠ 오탐 섞임, 사람이 판단할 것");
            report.AppendLine();
            report.AppendLine("| 플레이스홀더 | 겹칠 수 있는 기계 | 쓰임 |");
            report.AppendLine("|---|---|---|");
            foreach (Entry entry in entries.Values)
            {
                if (entry.folder != "Placeholder") continue;

                string key = Key(entry.item.DisplayName);
                if (key.Length < 2 || machines.ContainsKey(key)) continue;   // ②에서 이미 잡힌 것은 뺀다

                foreach (KeyValuePair<string, MachineBlock> pair in machines)
                {
                    if (!pair.Key.Contains(key) && !key.Contains(pair.Key)) continue;
                    loose++;
                    report.AppendLine($"| `{entry.item.itemName}` | `{pair.Value.blockName}` ({pair.Value.DisplayName}) | {Uses(entry)} |");
                }
            }
            report.AppendLine();

            // ③은 오탐이 섞이므로 합계에 넣지 않는다. 섞어 세면 "몇 건을 고쳐야 하는지" 를 알 수 없다.
            report.AppendLine($"**확실한 중복 {count}건** (①+②) · 검토 필요 {loose}건 (③)");
            report.AppendLine();
            return count;
        }

        private static string Row(string key, Entry entry)
            => $"| {key} | `{entry.item.itemName}` | {entry.folder} | {Uses(entry)} |";

        private static string Uses(Entry entry)
            => $"{entry.registeredUses}/{entry.incompleteUses}/{entry.blockUses}";

        // ── 그 밖의 절 ────────────────────────────────────────────

        private static void WriteSummary(StringBuilder report, Dictionary<Items, Entry> entries, HashSet<Recipe> registered)
        {
            Dictionary<string, int> byFolder = new Dictionary<string, int>();
            foreach (Entry entry in entries.Values)
            {
                byFolder.TryGetValue(entry.folder, out int n);
                byFolder[entry.folder] = n + 1;
            }

            report.AppendLine("## 요약");
            report.AppendLine();
            report.AppendLine($"- 아이템 **{entries.Count}개**, 등록된 레시피 {registered.Count}개");
            foreach (KeyValuePair<string, int> pair in byFolder)
                report.AppendLine($"  - {pair.Key} {pair.Value}개");
            report.AppendLine();
            report.AppendLine("쓰임 표기는 `등록/미완/블록` — 등록된 레시피 / 그 밖의 레시피 / 블록의 dropItem.");
            report.AppendLine("`Recipe.importNote` 는 세지 않는다(원문 JSON 이 들어 있어 세면 전부 참조됨으로 나온다).");
            report.AppendLine();
        }

        private static void WriteUnused(StringBuilder report, Dictionary<Items, Entry> entries)
        {
            report.AppendLine("## 레시피·블록이 참조하지 않는 아이템");
            report.AppendLine();
            report.AppendLine("⚠ **참조가 없다 ≠ 필요 없다.** 두 가지 이유로 안 잡힌다:");
            report.AppendLine();
            report.AppendLine("1. **설계에는 있는데 레시피가 아직 안 쓰였다.** 추출기 산출물(철·구리·금…)이 그랬다.");
            report.AppendLine("2. **참조가 아니라 이름·딕셔너리로 연결된다.** 기계 배치 아이템은 `blockId == itemName`");
            report.AppendLine("   문자열로 이어지고, 도구 부품은 `ToolDictionary` 와 부품 칸 필터가 들고 있다.");
            report.AppendLine();
            report.AppendLine("그래서 아래 목록에서 **Machines · ToolParts · Tools 는 거의 전부 오탐**이다.");
            report.AppendLine("실제로 살펴볼 값어치가 있는 것은 `Placeholder` 와 `Resource1` 뿐이다.");
            report.AppendLine();

            foreach (string folder in new[] { "Placeholder", "Resource1", "Machines", "ToolParts", "Tools" })
            {
                List<string> names = new List<string>();
                foreach (Entry entry in entries.Values)
                    if (entry.Total == 0 && entry.folder == folder) names.Add(entry.item.itemName);
                if (names.Count == 0) continue;

                names.Sort(System.StringComparer.Ordinal);
                bool noisy = folder != "Placeholder" && folder != "Resource1";
                report.AppendLine($"- **{folder}** {names.Count}종{(noisy ? " *(이름으로 연결 — 대부분 오탐)*" : "")}: "
                    + string.Join(", ", names));
            }
            report.AppendLine();
        }

        private static void WritePending(StringBuilder report, Dictionary<Items, Entry> entries)
        {
            report.AppendLine("## 승격 대기 — 정본이 없는 플레이스홀더");
            report.AppendLine();
            report.AppendLine("| 아이템 | 쓰임 | 아이콘 | 배치 가능 |");
            report.AppendLine("|---|---|---|---|");

            List<Entry> pending = new List<Entry>();
            foreach (Entry entry in entries.Values)
                if (entry.folder == "Placeholder" && entry.Total > 0) pending.Add(entry);

            pending.Sort((a, b) => b.Total.CompareTo(a.Total));
            foreach (Entry entry in pending)
                report.AppendLine($"| `{entry.item.itemName}` | {Uses(entry)} | {(entry.item.Icon != null ? "있음" : "없음")} | {(entry.item.placeable ? "예" : "아니오")} |");
            report.AppendLine();
            report.AppendLine($"合 {pending.Count}종. `{PlaceholderFolder}` 에는 이미 정본인 것(돌·마력석·파이프)도 섞여 있으니 폴더로 판단하지 말 것.");
            report.AppendLine();
        }

        /// <summary>설계 문서와 코드가 어긋난 곳. 사람이 판단해야 하므로 고치지 않고 적기만 한다.</summary>
        private static void WriteContradictions(StringBuilder report)
        {
            report.AppendLine("## 설계와 어긋난 곳 (사람 판단 필요)");
            report.AppendLine();
            report.AppendLine("- `MachineAliases` 의 `{ 용광로 → 화로 }` — 설계 JSON 은");
            report.AppendLine("  \"화로와 별개인 상위 제련 기계(강철·티타늄)\" 라고 적고 있다. 이미 합쳐진 상태다.");
            report.AppendLine("- `MachineAliases` 의 `{ 수동 분쇄기 → 전기 분쇄기 }` — 수동 분쇄기는 별개 기계로");
            report.AppendLine("  두기로 했지만 아직 MachineBlock 이 없어 임시로 보내고 있다. 정식 기계가 되면 그 줄을 지운다.");
            report.AppendLine("- `extract_conductor_powder` 는 지형 '모래' 산출인데, 캔버스에서는");
            report.AppendLine("  수동 0-0티어 추출기의 3회 분쇄 산출이다.");
            report.AppendLine("- `extraction.json` 7개는 `terrain` 필드가 붙은 **지형 설치형**이라,");
            report.AppendLine("  분쇄물을 넣는 캔버스의 추출기와 개념이 다르다.");
            report.AppendLine();
            report.AppendLine("## 다음 작업으로 미룬 것");
            report.AppendLine();
            report.AppendLine("- 칼 계열(칼·돌 칼·철 칼·칼날·돌 칼날·철 칼날)의 도구 체계 흡수");
            report.AppendLine("- 마력석·운석의 1/2/3회 분쇄 아이템 6종 (돌은 조각난·부숴진·바스라진이 이미 있다)");
            report.AppendLine("- 캔버스의 추출 레시피 36종과 확률 산출 동작");
            report.AppendLine("- 1·2티어 자원 생성기");
            report.AppendLine("- 2티어(운석) 지형 — `TerrainPalette` 는 아직 stage1(돌)·stage2(마력석) 뿐이다");
        }
    }
}
