using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectCraft.EditorTools
{
    /// <summary>
    /// StreamingAssets/Recipes 의 기획 JSON 을 Recipe 에셋으로 옮긴다.
    ///
    /// 결과는 "미완성" 폴더에만 만들고 RecipeDictionary 에는 등록하지 않는다.
    /// 대응하는 Items 가 없는 재료는 Placeholder 폴더에 임시 아이템을 만들어 연결하므로,
    /// 그 목록이 곧 "앞으로 만들어야 할 아이템" 할 일 목록이 된다.
    ///
    /// 재실행 가능(idempotent): 같은 경로의 에셋은 새로 만들지 않고 갱신한다.
    /// 대화상자는 띄우지 않는다(자동화가 멈춘다).
    /// </summary>
    public static class RecipeJsonImporter
    {
        private const string JsonFolder = "Assets/StreamingAssets/Recipes";
        private const string OutputFolder = "Assets/Prefabs/Recipes/Incomplete";

        /// <summary>
        /// 노션 정본에서 나온 JSON 의 접두사. 예전에는 별도 폴더에 만들었지만
        /// 두 트리를 통합한 뒤로는 하위 폴더 이름에서 접두사만 떼고 같은 곳에 만든다.
        /// </summary>
        private const string NotionPrefix = "notion_";
        private const string PlaceholderFolder = "Assets/Prefabs/Items/Placeholder";
        private const string ReportPath = OutputFolder + "/_ImportReport.md";

        private const int PlaceholderMaxStack = 64;
        private const float DefaultCraftTime = 1f;

        /// <summary>플레이스홀더 아이콘을 찾을 스프라이트 시트(임시 위치. 옮기면 이 경로만 고치면 된다).</summary>
        private static readonly string[] SpriteSheets =
        {
            "Assets/Prefabs/Blocks/Machines/hammer_and_@.png",
            "Assets/Prefabs/Blocks/Machines/machine2.png",
            "Assets/Prefabs/Blocks/Machines/tmp_crafter.png",
        };

        /// <summary>한글 아이템 이름 → 서브스프라이트 이름. 이름이 불명확한 시트는 넣지 않는다.</summary>
        private static readonly string[,] IconMap =
        {
            { "막대", "wood_rod" },
            { "망치 머리", "wood_hammer_head" },
            { "철 망치 머리", "iron_hammer_head" },
            { "망치", "wood_heammer_rod" },
            { "철 망치", "iron_hammer_rod" },
            { "양동이", "buket" },
            { "드라이버", "driver" },
            { "컴퓨터 칩", "computer_chip" },
            { "다우징 로드", "dowsing_rod" },
            { "0티어 다우징 로드", "dowsing_rod" },
            { "화로", "furnace_icon" },
            { "수동 분쇄기", "manual_pulverizer_icon" },
            { "초급 재단", "basic_altar_icon" },
            { "가공대", "tmp_crafter" },
        };

        /// <summary>JSON 기계 이름 → MachineBlock 표시 이름(나머지는 표시 이름이 그대로 일치한다).</summary>
        private static readonly string[,] MachineAlias =
        {
            { "분쇄기", "전기 분쇄기" },
            { "수동 분쇄기", "전기 분쇄기" },
            { "전기분해기", "전기 분해기" },
        };

        // ── 임포트 상태 ───────────────────────────────────────
        private static Dictionary<string, Items> itemsByDisplay;
        private static Dictionary<string, Items> itemsById;
        private static Dictionary<string, MachineBlock> machinesByDisplay;
        private static Dictionary<string, Sprite> sprites;
        private static Dictionary<string, string> aliases;

        private static Dictionary<string, int> placeholderUses;   // 플레이스홀더 이름 → 사용 횟수
        private static Dictionary<string, bool> placeholderIcons; // 플레이스홀더 이름 → 아이콘 연결 여부
        private static Dictionary<string, int> unknownMachines;   // 대응 없는 기계 이름 → 레시피 수
        private static HashSet<string> usedPaths;
        private static int skipped;                                // 이미 있어 건드리지 않은 레시피 수

        [MenuItem("Tools/Project Craft/Recipes/Import JSON Recipes")]
        public static void ImportMenu() => Import();

        /// <summary>JSON 전체를 임포트한다. 생성/갱신한 레시피 수를 반환.</summary>
        public static int Import()
        {
            if (!Directory.Exists(JsonFolder))
            {
                Debug.LogError("[RecipeJsonImporter] JSON 폴더가 없습니다: " + JsonFolder);
                return 0;
            }

            BuildLookups();
            EnsureFolder(OutputFolder);
            EnsureFolder(PlaceholderFolder);

            StringBuilder report = new StringBuilder();
            report.AppendLine("# 레시피 임포트 리포트");
            report.AppendLine();
            report.AppendLine("`Tools/Project Craft/Recipes/Import JSON Recipes` 로 생성됨. **RecipeDictionary 에는 등록되지 않았다.**");
            report.AppendLine();
            report.AppendLine("| 파일 | 레시피 |");
            report.AppendLine("|---|---|");

            int total = 0;
            string[] files = Directory.GetFiles(JsonFolder, "*.json");
            System.Array.Sort(files);

            for (int i = 0; i < files.Length; i++)
            {
                string fileBase = Path.GetFileNameWithoutExtension(files[i]);
                int n = ImportFile(files[i], fileBase);
                total += n;
                report.AppendLine("| " + fileBase + " | " + n + " |");
            }
            report.AppendLine("| **합계** | **" + total + "** |");

            AppendPlaceholderSection(report);
            AppendMachineSection(report);

            AssetDatabase.SaveAssets();
            File.WriteAllText(ReportPath, report.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();

            Debug.Log("[RecipeJsonImporter] 새로 만든 레시피 " + total + "개 / 이미 있어 건너뜀 " + skipped
                + "개 / 플레이스홀더 아이템 " + placeholderUses.Count + "개. 리포트: " + ReportPath);
            return total;
        }

        // ── 조회 테이블 ───────────────────────────────────────
        private static void BuildLookups()
        {
            itemsByDisplay = new Dictionary<string, Items>();
            itemsById = new Dictionary<string, Items>();
            machinesByDisplay = new Dictionary<string, MachineBlock>();
            sprites = new Dictionary<string, Sprite>();
            aliases = new Dictionary<string, string>();
            placeholderUses = new Dictionary<string, int>();
            placeholderIcons = new Dictionary<string, bool>();
            unknownMachines = new Dictionary<string, int>();
            usedPaths = new HashSet<string>();
            skipped = 0;

            string[] itemGuids = AssetDatabase.FindAssets("t:Items");
            for (int i = 0; i < itemGuids.Length; i++)
            {
                Items item = AssetDatabase.LoadAssetAtPath<Items>(AssetDatabase.GUIDToAssetPath(itemGuids[i]));
                if (item == null) continue;

                string id = ItemDictionary.NormalizeName(item.itemName);
                if (!string.IsNullOrEmpty(id) && !itemsById.ContainsKey(id)) itemsById[id] = item;

                string display = ItemDictionary.NormalizeName(item.DisplayName);
                if (!string.IsNullOrEmpty(display) && !itemsByDisplay.ContainsKey(display)) itemsByDisplay[display] = item;
            }

            string[] machineGuids = AssetDatabase.FindAssets("t:MachineBlock");
            for (int i = 0; i < machineGuids.Length; i++)
            {
                MachineBlock block = AssetDatabase.LoadAssetAtPath<MachineBlock>(AssetDatabase.GUIDToAssetPath(machineGuids[i]));
                if (block == null) continue;
                string display = ItemDictionary.NormalizeName(block.DisplayName);
                if (!string.IsNullOrEmpty(display) && !machinesByDisplay.ContainsKey(display)) machinesByDisplay[display] = block;
            }

            for (int i = 0; i < SpriteSheets.GetLength(0); i++)
            {
                Object[] all = AssetDatabase.LoadAllAssetsAtPath(SpriteSheets[i]);
                for (int j = 0; j < all.Length; j++)
                {
                    Sprite sprite = all[j] as Sprite;
                    if (sprite != null && !sprites.ContainsKey(sprite.name)) sprites[sprite.name] = sprite;
                }
            }

            for (int i = 0; i < MachineAlias.GetLength(0); i++)
                aliases[ItemDictionary.NormalizeName(MachineAlias[i, 0])] = ItemDictionary.NormalizeName(MachineAlias[i, 1]);
        }

        // ── 파일 하나 ────────────────────────────────────────
        private static int ImportFile(string path, string fileBase)
        {
            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (System.Exception e)
            {
                Debug.LogError("[RecipeJsonImporter] " + fileBase + " 파싱 실패: " + e.Message);
                return 0;
            }

            JArray recipes = root["recipes"] as JArray;
            if (recipes == null) return 0;

            // 트리는 하나뿐이다. notion_* 도 접두사만 떼고 같은 폴더 아래에 만든다.
            string subFolder = fileBase.StartsWith(NotionPrefix) ? fileBase.Substring(NotionPrefix.Length) : fileBase;
            string folder = OutputFolder + "/" + subFolder;
            EnsureFolder(folder);
            string fileMachine = (string)root["machine"];   // 파일 상단 공통 기계

            int count = 0;
            for (int i = 0; i < recipes.Count; i++)
            {
                JObject entry = recipes[i] as JObject;
                if (entry == null) continue;

                string id = (string)entry["id"];
                if (string.IsNullOrEmpty(id)) id = fileBase + "_" + i;

                if (BuildRecipe(entry, fileBase, fileMachine, folder + "/" + Sanitize(id) + ".asset")) count++;
            }
            return count;
        }

        private static bool BuildRecipe(JObject entry, string fileBase, string fileMachine, string assetPath)
        {
            // 이미 있으면 손대지 않는다. 손으로 다듬은 내용을 덮어쓰지 않기 위함.
            if (AssetDatabase.LoadAssetAtPath<Recipe>(assetPath) != null)
            {
                skipped++;
                return false;
            }

            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();

            StringBuilder note = new StringBuilder();
            note.AppendLine("[출처] " + fileBase + ".json / id=" + (string)entry["id"]);

            recipe.inputs = new List<ItemStack>();
            recipe.outputs = new List<ItemStack>();
            recipe.craftTime = DefaultCraftTime;
            recipe.category = null;                       // RecipeCategory 에셋은 아직 없다
            recipe.tier = entry["tier"] != null ? (int)entry["tier"] : 0;
            recipe.machine = ResolveMachine((string)entry["machine"] ?? fileMachine, note);

            ReadInputs(entry, recipe, note);
            ReadOutputs(entry, recipe, note);
            ReadExtras(entry, note);

            note.AppendLine("[원문] " + entry.ToString(Newtonsoft.Json.Formatting.None));
            recipe.importNote = note.ToString();

            AssetDatabase.CreateAsset(recipe, assetPath);
            return true;
        }

        // ── 입력 ─────────────────────────────────────────────
        private static void ReadInputs(JObject entry, Recipe recipe, StringBuilder note)
        {
            JToken token = entry["inputs"] ?? entry["input"];
            if (token == null) return;

            if (token.Type == JTokenType.String)
            {
                AddStack(recipe.inputs, (string)token, 1);
                return;
            }

            JArray array = token as JArray;
            if (array == null) return;

            for (int i = 0; i < array.Count; i++)
            {
                JObject item = array[i] as JObject;
                if (item == null)
                {
                    if (array[i].Type == JTokenType.String) AddStack(recipe.inputs, (string)array[i], 1);
                    continue;
                }

                string name = (string)item["item"];
                if (string.IsNullOrEmpty(name)) continue;

                if (IsTrue(item["fluid"]))
                {
                    note.AppendLine("[유체 재료] " + name + " x" + QuantityText(item["qty"]) + " (아이템이 아니라 옮기지 않음)");
                    continue;
                }

                if (item["qty"] == null || item["qty"].Type == JTokenType.Null)
                    note.AppendLine("[수량 미표기] 재료 '" + name + "' 을 1개로 간주");

                AddStack(recipe.inputs, name, Quantity(item["qty"]));
            }
        }

        // ── 출력 ─────────────────────────────────────────────
        private static void ReadOutputs(JObject entry, Recipe recipe, StringBuilder note)
        {
            // 레시피 전체가 확률 산출인 경우(예: 10% 확률로 생성) — 산출물은 남기되 확률을 기록한다.
            JToken recipeChance = entry["chance"];
            if (recipeChance != null && recipeChance.Type != JTokenType.Null && (double)recipeChance < 1.0)
                note.AppendLine("[산출 확률] 이 레시피는 " + ((double)recipeChance * 100.0) + "% 확률로만 산출된다");

            JToken single = entry["output"];
            if (single != null && single.Type == JTokenType.String)
            {
                string name = (string)single;
                int qty = Quantity(entry["outputQty"]);

                if (IsTrue(entry["outputFluid"]))
                    note.AppendLine("[유체 산출] " + name + " x" + qty + " (아이템이 아니라 옮기지 않음)");
                else
                    AddStack(recipe.outputs, name, qty);
            }

            JArray outputs = entry["outputs"] as JArray;
            if (outputs == null) return;

            for (int i = 0; i < outputs.Count; i++)
            {
                JObject output = outputs[i] as JObject;
                if (output == null) continue;

                if (string.Equals((string)output["type"], "power"))
                {
                    note.AppendLine("[전력 산출] " + output.ToString(Newtonsoft.Json.Formatting.None));
                    continue;
                }

                string name = (string)output["item"];
                if (string.IsNullOrEmpty(name)) continue;

                if (IsTrue(output["fluid"]))
                {
                    note.AppendLine("[유체 산출] " + name + " x" + QuantityText(output["qty"]));
                    continue;
                }

                // chance 가 없거나 1.0 일 때만 확정 산출로 본다.
                // null(비율 미정)이나 1 미만을 그대로 넣으면 확률 부산물이 100% 산출로 굳어버린다.
                JToken chance = output["chance"];
                bool guaranteed = chance == null || (chance.Type != JTokenType.Null && (double)chance >= 1.0);
                if (!guaranteed)
                {
                    string percent = chance.Type == JTokenType.Null ? "비율 미정" : ((double)chance * 100.0) + "%";
                    string grade = (string)output["grade"];
                    note.AppendLine("[확률 산출] " + name + " x" + QuantityText(output["qty"]) + " (" + percent
                        + (string.IsNullOrEmpty(grade) ? "" : ", " + grade) + ")");
                    continue;
                }

                AddStack(recipe.outputs, name, Quantity(output["qty"]));
            }
        }

        // ── Recipe 에 담을 곳이 없는 나머지 ──────────────────
        private static void ReadExtras(JObject entry, StringBuilder note)
        {
            AppendTools(entry["tools"], note);
            if (entry["tool"] != null) note.AppendLine("[도구] " + (string)entry["tool"] + " (내구도만 소모, 재료 아님)");

            AppendIfPresent(entry["station"], "조합 장소", note);
            AppendIfPresent(entry["power"], "전력", note);
            AppendIfPresent(entry["terrain"], "필요 지형", note);
            AppendIfPresent(entry["category"], "원문 분류", note);
            AppendIfPresent(entry["fluid"], "유체", note);
            if (IsTrue(entry["manual"])) note.AppendLine("[수동] 전력이 필요 없다");
            AppendIfPresent(entry["note"], "비고", note);
        }

        private static void AppendTools(JToken tools, StringBuilder note)
        {
            JArray array = tools as JArray;
            if (array == null || array.Count == 0) return;

            List<string> names = new List<string>();
            for (int i = 0; i < array.Count; i++)
            {
                if (array[i].Type == JTokenType.String) names.Add((string)array[i]);
                else if (array[i] is JObject item && item["item"] != null) names.Add((string)item["item"]);
            }
            if (names.Count > 0)
                note.AppendLine("[도구] " + string.Join(", ", names.ToArray()) + " (내구도만 소모, 재료 아님)");
        }

        private static void AppendIfPresent(JToken token, string label, StringBuilder note)
        {
            if (token == null || token.Type == JTokenType.Null) return;
            note.AppendLine("[" + label + "] " + token.ToString(Newtonsoft.Json.Formatting.None).Trim('"'));
        }

        // ── 해석 ─────────────────────────────────────────────
        private static void AddStack(List<ItemStack> list, string name, int count)
        {
            Items item = ResolveItem(name);
            if (item == null) return;

            ItemStack stack = new ItemStack();
            stack.item = item;
            stack.count = Mathf.Max(1, count);
            list.Add(stack);
        }

        /// <summary>표시 이름 → ID 순으로 찾고, 없으면 플레이스홀더를 만든다.</summary>
        private static Items ResolveItem(string rawName)
        {
            string name = ItemDictionary.NormalizeName(rawName);
            if (string.IsNullOrEmpty(name)) return null;

            if (itemsByDisplay.TryGetValue(name, out Items byDisplay)) return CountIfPlaceholder(name, byDisplay);
            if (itemsById.TryGetValue(name, out Items byId)) return CountIfPlaceholder(name, byId);

            return CountIfPlaceholder(name, CreatePlaceholder(name));
        }

        /// <summary>
        /// 리포트 집계. 이번 실행에서 새로 만든 것뿐 아니라 이전 실행이 만들어 둔 것도 세야 하므로
        /// 생성 여부가 아니라 "에셋이 Placeholder 폴더에 있는가"로 판정한다.
        /// </summary>
        private static Items CountIfPlaceholder(string name, Items item)
        {
            if (item == null) return null;
            if (!AssetDatabase.GetAssetPath(item).StartsWith(PlaceholderFolder)) return item;

            placeholderUses.TryGetValue(name, out int uses);
            placeholderUses[name] = uses + 1;
            placeholderIcons[name] = item.Icon != null;
            return item;
        }

        private static Items CreatePlaceholder(string name)
        {
            string path = UniquePath(PlaceholderFolder + "/" + Sanitize(name) + ".asset");

            Items item = AssetDatabase.LoadAssetAtPath<Items>(path);
            bool isNew = item == null;
            if (isNew) item = ScriptableObject.CreateInstance<Items>();

            item.itemName = name;          // 임시 ID. 정식 아이템으로 승격할 때 영어 ID 로 바꾼다
            item.displayName = name;
            item.placeable = false;
            item.maxStack = PlaceholderMaxStack;
            if (item.Icon == null) item.Icon = ResolveIcon(name);

            if (isNew) AssetDatabase.CreateAsset(item, path);
            else EditorUtility.SetDirty(item);

            // 같은 이름이 다시 나오면 이 에셋을 재사용하도록 색인에 등록
            if (!itemsByDisplay.ContainsKey(name)) itemsByDisplay[name] = item;
            return item;
        }

        private static Sprite ResolveIcon(string name)
        {
            for (int i = 0; i < IconMap.GetLength(0); i++)
            {
                if (ItemDictionary.NormalizeName(IconMap[i, 0]) != name) continue;
                if (sprites.TryGetValue(IconMap[i, 1], out Sprite sprite)) return sprite;
            }
            return null;
        }

        private static MachineBlock ResolveMachine(string rawName, StringBuilder note)
        {
            if (string.IsNullOrEmpty(rawName)) return null;

            string name = ItemDictionary.NormalizeName(rawName);
            if (aliases.TryGetValue(name, out string alias)) name = alias;

            if (machinesByDisplay.TryGetValue(name, out MachineBlock block))
            {
                if (!string.Equals(name, ItemDictionary.NormalizeName(rawName)))
                    note.AppendLine("[기계] '" + rawName + "' → " + block.DisplayName + " 으로 매칭");
                return block;
            }

            note.AppendLine("[기계] '" + rawName + "' — 대응하는 MachineBlock 이 없어 비워 둠");
            unknownMachines.TryGetValue(rawName, out int count);
            unknownMachines[rawName] = count + 1;
            return null;
        }

        // ── 리포트 ───────────────────────────────────────────
        private static void AppendPlaceholderSection(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("## 플레이스홀더 아이템 (" + placeholderUses.Count + "종)");
            report.AppendLine();
            report.AppendLine("대응하는 `Items` 에셋이 없어 `" + PlaceholderFolder + "` 에 임시로 만든 것들이다.");
            report.AppendLine("**이 목록이 곧 앞으로 만들어야 할 아이템 목록이다.** 정식 아이템으로 승격할 때 `itemName` 을 영어 ID 로 바꾸면 된다.");
            report.AppendLine();
            report.AppendLine("| 이름 | 사용 횟수 | 아이콘 |");
            report.AppendLine("|---|---|---|");

            List<KeyValuePair<string, int>> sorted = new List<KeyValuePair<string, int>>(placeholderUses);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < sorted.Count; i++)
            {
                placeholderIcons.TryGetValue(sorted[i].Key, out bool hasIcon);
                report.AppendLine("| " + sorted[i].Key + " | " + sorted[i].Value + " | " + (hasIcon ? "연결됨" : "없음") + " |");
            }
        }

        private static void AppendMachineSection(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("## 대응 기계가 없는 JSON 기계 (" + unknownMachines.Count + "종)");
            report.AppendLine();
            report.AppendLine("해당 레시피의 `machine` 은 비어 있다. `MachineBlock` 을 만들고 `displayName` 을 맞추면 다음 임포트에서 자동 연결된다.");
            report.AppendLine();
            report.AppendLine("| 기계 | 레시피 수 |");
            report.AppendLine("|---|---|");

            List<KeyValuePair<string, int>> sorted = new List<KeyValuePair<string, int>>(unknownMachines);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < sorted.Count; i++)
                report.AppendLine("| " + sorted[i].Key + " | " + sorted[i].Value + " |");
        }

        // ── 유틸 ─────────────────────────────────────────────
        private static int Quantity(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return 1;
            return Mathf.Max(1, (int)token);
        }

        private static string QuantityText(JToken token)
            => token == null || token.Type == JTokenType.Null ? "?" : token.ToString();

        private static bool IsTrue(JToken token)
            => token != null && token.Type == JTokenType.Boolean && (bool)token;

        /// <summary>파일명에 쓸 수 없는 문자를 치환한다('석탄/갈탄/석유 중 1' 처럼 슬래시가 든 이름이 있다).</summary>
        private static string Sanitize(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool bad = false;
                for (int j = 0; j < invalid.Length; j++) if (invalid[j] == c) { bad = true; break; }
                sb.Append(bad ? '_' : c);
            }
            return sb.ToString();
        }

        /// <summary>치환 후 이름이 겹칠 수 있으므로 이미 쓴 경로면 번호를 붙인다.</summary>
        private static string UniquePath(string path)
        {
            if (!usedPaths.Contains(path)) { usedPaths.Add(path); return path; }

            string withoutExtension = path.Substring(0, path.Length - ".asset".Length);
            for (int i = 2; i < 1000; i++)
            {
                string candidate = withoutExtension + "_" + i + ".asset";
                if (usedPaths.Contains(candidate)) continue;
                usedPaths.Add(candidate);
                return candidate;
            }
            return path;
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
