using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectCraft.EditorTools
{
    /// <summary>
    /// <see cref="Recipe.machine"/> 이 비어 있는 레시피에 기계를 붙인다.
    /// 임포터가 JSON 의 기계 이름에 대응하는 MachineBlock 을 찾지 못해 비워 둔 것들로,
    /// 그대로 두면 어느 기계도 이 레시피를 찾지 못해 게임에 존재하지 않는 것과 같다.
    ///
    /// - 이미 있는 기계는 <b>표시 이름</b>으로 이어 붙인다(구 이름은 <see cref="MachineAliases"/> 로 보정).
    /// - `조합대` 와 기계 이름이 아예 없는 조합 레시피는 <b>코어 조합기</b> 로 본다.
    /// - 나머지는 <b>플레이스홀더 아트로 MachineBlock 과 배치용 아이템을 새로 만든다.</b>
    ///   슬롯 수·티어·전력 여부는 그 기계가 가진 레시피에서 뽑는다.
    ///
    /// 재실행 가능(이미 배정된 레시피와 이미 있는 에셋은 건드리지 않는다). 대화상자 없음.
    /// </summary>
    public static class MachineBlockFiller
    {
        private const string BlockFolder = "Assets/Prefabs/Blocks/Machines";
        private const string ItemFolder = "Assets/Prefabs/Items/Machines";
        private const string SourceWorldPrefab = BlockFolder + "/AlloySmelter.prefab";
        private const string PlaceholderSprite = "Assets/Asset/assetPlaceHolder.png";
        private const string CoreCrafterPath = BlockFolder + "/CoreCrafter.asset";
        private const string ReportPath = BlockFolder + "/_MachineFillReport.md";

        /// <summary>새로 만들 기계의 한글 이름 → 내부 ID. ID 는 세이브 키가 되므로 손으로 정한다.</summary>
        private static readonly string[,] NewMachineIds =
        {
            // '감별기' 는 유일한 재료였던 '반짝이는 돌' 과 함께 삭제됐다(보석은 1계열 추출이 낸다).
            // 여기 줄을 되살리면 이 툴이 기계를 다시 만들어 낸다.
            { "마나 용해기", "ManaDissolver" },
            { "마법부여기", "Enchanter" },
            { "변압기", "Transformer" },
            // '압연기' 는 '압축기' 와 같은 기계라 흡수됐고, '시멘트 공장'·'유리 가공기' 는 삭제됐다.
            // 여기 줄을 되살리면 이 툴이 지운 기계를 다시 만들어 낸다('감별기' 와 같은 이유).
            { "원유 채굴기", "OilDrill" },
            { "정밀 세공기", "PrecisionLathe" },
            // '정유기' 는 삭제됐다 — 원유 처리는 '증류기'(Distiller) 하나로 모았다.
            // 같은 일을 하는 기계가 티어별로 갈리면 안 된다는 규칙 때문이다(CLAUDE.md §4).
            { "중급 재단", "IntermediateAltar" },
            { "초급 재단", "BasicAltar" },
            { "핵발전소", "NuclearPlant" },
            { "화력 발전기", "ThermalGenerator" },
            { "화학 처리기", "ChemicalProcessor" },
        };

        /// <summary>한 기계가 가진 레시피에서 뽑아낸 설정값.</summary>
        private class Demand
        {
            public readonly List<Recipe> recipes = new List<Recipe>();
            public int maxTier;
            public int maxInputs = 1;
            public int maxOutputs = 1;
            public bool usesEnergy;
        }

        private static readonly StringBuilder Report = new StringBuilder();

        [MenuItem("Tools/Project Craft/Machines/Fill Missing Machine Blocks")]
        public static void Fill()
        {
            Report.Clear();
            Report.AppendLine("# 누락 기계 생성 · 레시피 배정 보고서");
            Report.AppendLine();
            Report.AppendLine("`Tools/Project Craft/Machines/Fill Missing Machine Blocks` 가 자동 생성한 파일입니다.");
            Report.AppendLine();

            MachineBlock coreCrafter = AssetDatabase.LoadAssetAtPath<MachineBlock>(CoreCrafterPath);
            if (coreCrafter == null)
            {
                Debug.LogError("[MachineBlockFiller] CoreCrafter 블록을 찾지 못했습니다: " + CoreCrafterPath);
                return;
            }

            Dictionary<string, MachineBlock> existing = LoadExistingBlocks();
            Dictionary<string, Demand> demands = CollectDemands();

            List<MachineBlock> created = new List<MachineBlock>();
            List<Items> createdItems = new List<Items>();
            int assigned = 0;
            List<string> skipped = new List<string>();

            EnsureFolder(BlockFolder);
            EnsureFolder(ItemFolder);
            Sprite placeholder = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderSprite);

            Report.AppendLine("## 기계별 처리");
            Report.AppendLine();

            List<string> names = new List<string>(demands.Keys);
            names.Sort(System.StringComparer.Ordinal);

            foreach (string name in names)
            {
                Demand demand = demands[name];

                MachineBlock target;
                string how;

                if (name == "조합대" || name == "")
                {
                    target = coreCrafter;
                    how = name == "" ? "기계 이름이 없는 조합 레시피 → 코어 조합기" : "조합대 → 코어 조합기";
                }
                else if (existing.TryGetValue(name, out MachineBlock found))
                {
                    target = found;
                    how = "기존 블록 `" + found.blockName + "` 에 연결";
                }
                else
                {
                    target = CreateBlock(name, demand, placeholder, created, createdItems);
                    if (target == null)
                    {
                        skipped.Add($"- **{name}** — 내부 ID 표에 없어 만들지 않았습니다({demand.recipes.Count}개 레시피가 미배정으로 남음)");
                        continue;
                    }
                    how = "새로 만듦 `" + target.blockName + "` (티어 " + target.tier
                        + " · 입력 " + target.inputSlotCount + " · 출력 " + target.outputSlotCount
                        + (target.isUseEnergy ? " · 전력" : "") + ")";
                }

                int linked = 0;
                foreach (Recipe recipe in demand.recipes)
                {
                    if (recipe.machine != null) continue;
                    recipe.machine = target;
                    EditorUtility.SetDirty(recipe);
                    linked++;
                }
                assigned += linked;

                Report.AppendLine($"- **{name}** — {how} · 레시피 {linked}개 연결");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ReplaceMachinePlaceholders();

            Report.AppendLine();
            Report.AppendLine($"- 새로 만든 기계 {created.Count}종 · 연결한 레시피 {assigned}개");
            if (skipped.Count > 0)
            {
                Report.AppendLine();
                Report.AppendLine("## ⚠ 만들지 않은 기계");
                Report.AppendLine();
                foreach (string line in skipped) Report.AppendLine(line);
            }

            AppendTierBreakdown(coreCrafter);

            File.WriteAllText(ReportPath, Report.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ReportPath);
            Debug.Log("[MachineBlockFiller] 완료. 자세한 내역은 " + ReportPath
                + "\n이어서 Register All Assets 를 실행하세요.");
        }

        // ── 수집 ──────────────────────────────────────────────────
        private static Dictionary<string, MachineBlock> LoadExistingBlocks()
        {
            Dictionary<string, MachineBlock> result = new Dictionary<string, MachineBlock>();
            foreach (string guid in AssetDatabase.FindAssets("t:MachineBlock"))
            {
                MachineBlock block = AssetDatabase.LoadAssetAtPath<MachineBlock>(AssetDatabase.GUIDToAssetPath(guid));
                if (block != null && !result.ContainsKey(block.DisplayName)) result[block.DisplayName] = block;
            }
            return result;
        }

        /// <summary>기계가 안 붙은 레시피를 기계 이름별로 모으고, 필요한 설정값을 뽑는다.</summary>
        private static Dictionary<string, Demand> CollectDemands()
        {
            Dictionary<string, Demand> result = new Dictionary<string, Demand>();

            foreach (string guid in AssetDatabase.FindAssets("t:Recipe"))
            {
                Recipe recipe = AssetDatabase.LoadAssetAtPath<Recipe>(AssetDatabase.GUIDToAssetPath(guid));
                if (recipe == null || recipe.machine != null) continue;

                string name = Normalize(MachineNameOf(recipe));
                if (!result.TryGetValue(name, out Demand demand))
                {
                    demand = new Demand();
                    result[name] = demand;
                }

                demand.recipes.Add(recipe);
                if (recipe.tier > demand.maxTier) demand.maxTier = recipe.tier;
                demand.maxInputs = Mathf.Max(demand.maxInputs, CountFilled(recipe.inputs));
                demand.maxOutputs = Mathf.Max(demand.maxOutputs, CountFilled(recipe.outputs));
                if ((recipe.importNote ?? "").Contains("[전력]")) demand.usesEnergy = true;
            }
            return result;
        }

        private static int CountFilled(List<ItemStack> slots)
        {
            if (slots == null) return 0;

            int count = 0;
            foreach (ItemStack stack in slots)
                if (stack != null && stack.item != null && stack.count > 0) count++;
            return count;
        }

        private static string MachineNameOf(Recipe recipe)
        {
            string note = recipe.importNote;
            if (string.IsNullOrEmpty(note)) return "";

            const string marker = "[기계] '";
            int start = note.IndexOf(marker);
            if (start < 0) return "";

            start += marker.Length;
            int end = note.IndexOf('\'', start);
            return end > start ? note.Substring(start, end - start) : "";
        }

        // 기계 별칭은 MachineAliases 한 곳에만 둔다(사본을 두면 반드시 한쪽이 낡는다).
        private static string Normalize(string machine) => MachineAliases.Resolve(machine);

        // ── 생성 ──────────────────────────────────────────────────
        private static MachineBlock CreateBlock(string koreanName, Demand demand, Sprite placeholder,
            List<MachineBlock> created, List<Items> createdItems)
        {
            string id = IdFor(koreanName);
            if (id == null) return null;

            string blockName = "Machine:" + id;
            string blockPath = $"{BlockFolder}/{id}.asset";

            MachineBlock block = AssetDatabase.LoadAssetAtPath<MachineBlock>(blockPath);
            if (block == null)
            {
                block = ScriptableObject.CreateInstance<MachineBlock>();
                block.blockName = blockName;
                block.displayName = koreanName;
                block.machinePrefab = EnsureWorldPrefab(id, placeholder);
                block.uiPrefab = null;                       // 비우면 MachineUIHost 가 기본 패널로 폴백한다
                block.tier = demand.maxTier;                 // 자기 레시피를 전부 처리할 수 있어야 한다
                block.inputSlotCount = Mathf.Max(1, demand.maxInputs);
                block.outputSlotCount = Mathf.Max(1, demand.maxOutputs);
                block.fuelSlotCount = 0;
                block.isUseEnergy = demand.usesEnergy;
                block.maxEnergyAmount = demand.usesEnergy ? 1000f : 0f;
                AssetDatabase.CreateAsset(block, blockPath);
                created.Add(block);
            }

            // 배치하려면 blockName 과 같은 itemName 의 아이템이 있어야 한다.
            string itemPath = $"{ItemFolder}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<Items>(itemPath) == null)
            {
                Items item = ScriptableObject.CreateInstance<Items>();
                item.itemName = blockName;
                item.displayName = koreanName;
                item.placeable = true;
                item.maxStack = 64;
                item.Icon = placeholder;
                AssetDatabase.CreateAsset(item, itemPath);
                createdItems.Add(item);
            }

            return block;
        }

        private static string IdFor(string koreanName)
        {
            for (int i = 0; i < NewMachineIds.GetLength(0); i++)
                if (NewMachineIds[i, 0] == koreanName) return NewMachineIds[i, 1];
            return null;
        }

        /// <summary>
        /// 월드에 세울 프리팹을 확보한다(없으면 AlloySmelter 를 복제해 그림만 갈아 끼운다 —
        /// 콜라이더 구성을 물려받으려고 새로 만들지 않고 복제한다).
        ///
        /// 다른 에디터 툴도 같은 방식으로 기계를 세워야 해서 <c>internal</c> 이다.
        /// 지금은 이 파일 안에서만 쓰지만, 새 기계를 늘리는 표준 경로라 좁히지 않는다.
        /// </summary>
        internal static GameObject EnsureWorldPrefab(string id, Sprite placeholder)
        {
            string path = $"{BlockFolder}/{id}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            if (!AssetDatabase.CopyAsset(SourceWorldPrefab, path))
            {
                Debug.LogError("[MachineBlockFiller] 월드 프리팹 복제 실패: " + path);
                return null;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                contents.name = id;
                SpriteRenderer renderer = contents.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null) renderer.sprite = placeholder;
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // ── 기계 플레이스홀더 정리 ────────────────────────────────
        /// <summary>
        /// 임포트 시점에 기계 블록이 없어 만들어진 이름뿐인 아이템(예: '압연기')을
        /// 실제 배치 가능한 기계 아이템(`Machine:RollingMill`)으로 갈아 끼우고 지운다.
        /// 이걸 안 하면 조합대에서 기계를 만들어도 <b>배치할 수 없는 껍데기</b>가 나온다.
        /// </summary>
        private static void ReplaceMachinePlaceholders()
        {
            // 표시 이름 → 실제 기계 아이템
            Dictionary<string, Items> machineItems = new Dictionary<string, Items>();
            List<Items> candidates = new List<Items>();

            foreach (string guid in AssetDatabase.FindAssets("t:Items"))
            {
                Items item = AssetDatabase.LoadAssetAtPath<Items>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null) continue;

                if (item.itemName.StartsWith("Machine:") || item.itemName == "CoreCrafter")
                    machineItems[item.DisplayName] = item;
                else candidates.Add(item);
            }

            Dictionary<Items, Items> replace = new Dictionary<Items, Items>();
            foreach (Items item in candidates)
                if (machineItems.TryGetValue(item.DisplayName, out Items real) && real != item)
                    replace[item] = real;

            if (replace.Count == 0) return;

            int hits = 0, changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Recipe"))
            {
                Recipe recipe = AssetDatabase.LoadAssetAtPath<Recipe>(AssetDatabase.GUIDToAssetPath(guid));
                if (recipe == null) continue;

                bool dirty = false;
                dirty |= Swap(recipe.inputs, replace, ref hits);
                dirty |= Swap(recipe.outputs, replace, ref hits);
                if (!dirty) continue;

                EditorUtility.SetDirty(recipe);
                changed++;
            }

            // 레시피만 갈아 끼우면 부족하다. 지형·파이프의 배치 역인덱스가 dropItem 에 걸려 있어
            // 그걸 두고 아이템을 지우면 블록이 떨구는 것이 사라진다. ItemMerger 의 것을 그대로 쓴다.
            int blocks = ItemMerger.RewriteBlocks(replace);
            AssetDatabase.SaveAssets();

            Report.AppendLine();
            Report.AppendLine("## 기계 플레이스홀더 → 실제 기계 아이템");
            Report.AppendLine();
            Report.AppendLine($"- 레시피 {changed}개에서 참조 {hits}건 치환 · 블록 {blocks}개 dropItem 수정");
            Report.AppendLine();

            // 참조 재작성 → 저장 → <b>그 다음에만</b> 삭제. 순서를 뒤집거나 검사를 빼면
            // 남은 참조 자리에 {fileID: 0} 이 생겨 재료 한 줄이 조용히 사라진다.
            HashSet<Items> stillUsed = ItemMerger.CollectReferenced();

            int deleted = 0, kept = 0;
            foreach (KeyValuePair<Items, Items> pair in replace)
            {
                string path = AssetDatabase.GetAssetPath(pair.Key);
                if (stillUsed.Contains(pair.Key))
                {
                    Report.AppendLine($"- ⚠ `{path}` 는 아직 참조가 남아 지우지 않았습니다.");
                    kept++;
                    continue;
                }

                Report.AppendLine($"- `{pair.Key.itemName}` → `{pair.Value.itemName}` (`{path}` 삭제)");
                if (AssetDatabase.DeleteAsset(path)) deleted++;
            }
            Report.AppendLine();
            Report.AppendLine($"- 삭제한 플레이스홀더 {deleted}개 · 참조가 남아 보존한 것 {kept}개");
            AssetDatabase.SaveAssets();
        }

        private static bool Swap(List<ItemStack> slots, Dictionary<Items, Items> replace, ref int hits)
        {
            if (slots == null) return false;

            bool dirty = false;
            foreach (ItemStack stack in slots)
            {
                if (stack == null || stack.item == null) continue;
                if (!replace.TryGetValue(stack.item, out Items to)) continue;

                stack.item = to;
                dirty = true;
                hits++;
            }
            return dirty;
        }

        // ── 보고 ──────────────────────────────────────────────────
        /// <summary>조합대에 몰린 레시피가 티어별로 몇 개인지. 티어 게이팅 때문에 당장 보이는 수와 다르다.</summary>
        private static void AppendTierBreakdown(MachineBlock coreCrafter)
        {
            SortedDictionary<int, int> byTier = new SortedDictionary<int, int>();
            int noOutput = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Recipe"))
            {
                Recipe recipe = AssetDatabase.LoadAssetAtPath<Recipe>(AssetDatabase.GUIDToAssetPath(guid));
                if (recipe == null || recipe.machine != coreCrafter) continue;

                if (CountFilled(recipe.outputs) == 0) { noOutput++; continue; }
                byTier.TryGetValue(recipe.tier, out int count);
                byTier[recipe.tier] = count + 1;
            }

            Report.AppendLine();
            Report.AppendLine("## 코어 조합기 레시피의 티어 분포");
            Report.AppendLine();
            Report.AppendLine($"조합대 티어는 현재 **{coreCrafter.tier}** 이라, 그보다 높은 티어의 레시피는 등록은 되지만 목록에 뜨지 않는다.");
            Report.AppendLine();
            foreach (KeyValuePair<int, int> pair in byTier)
                Report.AppendLine($"- 티어 {pair.Key} : {pair.Value}개" + (pair.Key <= coreCrafter.tier ? "  ← 지금 보임" : ""));
            if (noOutput > 0) Report.AppendLine($"- (산출물이 없어 등록 제외: {noOutput}개)");
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
