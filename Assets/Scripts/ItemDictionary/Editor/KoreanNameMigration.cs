using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectCraft.EditorTools
{
    /// <summary>
    /// Items / BlockBase 의 한글 displayName 을 일괄로 채운다.
    /// itemName · blockName(내부 ID, 세이브 키)은 절대 건드리지 않는다.
    /// 이미 값이 있으면 덮어쓰지 않으므로 손으로 다듬은 이름은 보존된다. 재실행 가능.
    /// </summary>
    public static class KoreanNameMigration
    {
        // ID → 한글 표시 이름. JSON 기획서의 어휘에 맞춰 임포터가 자동 매칭되도록 했다.
        private static readonly string[,] Table =
        {
            // 광석: raw_*_ore → "X 조각"
            { "raw_iron_ore", "철 조각" },          { "raw_copper_ore", "구리 조각" },
            { "raw_gold_ore", "금 조각" },          { "raw_silver_ore", "은 조각" },
            { "raw_lead_ore", "납 조각" },          { "raw_nickel_ore", "니켈 조각" },
            { "raw_tin_ore", "주석 조각" },         { "raw_titanium_ore", "티타늄 조각" },
            { "raw_aluminum_ore", "알루미늄 조각" }, { "raw_lithium_ore", "리튬 조각" },
            { "raw_osmium_ore", "오스뮴 조각" },     { "raw_thorium_ore", "토륨 조각" },
            { "raw_uranium_ore", "우라늄 조각" },

            // 주괴: *_ingot → "X 주괴"
            { "iron_ingot", "철 주괴" },            { "copper_ingot", "구리 주괴" },
            { "gold_ingot", "금 주괴" },            { "silver_ingot", "은 주괴" },
            { "lead_ingot", "납 주괴" },            { "nickel_ingot", "니켈 주괴" },
            { "tin_ingot", "주석 주괴" },           { "titanium_ingot", "티타늄 주괴" },
            { "aluminum_ingot", "알루미늄 주괴" },   { "lithium_ingot", "리튬 주괴" },
            { "osmium_ingot", "오스뮴 주괴" },       { "thorium_ingot", "토륨 주괴" },
            { "uranium_ingot", "우라늄 주괴" },

            // 기타 재료
            { "coal", "석탄" },                     { "brown_coal", "갈탄" },
            { "bone_meal", "뼈 가루" },
            { "quartz_crystal", "석영 결정" },       { "quartz_powder", "석영 가루" },
            { "redstone_crystal", "레드스톤 결정" }, { "redstone_powder", "레드스톤 가루" },
            { "uranium_powder", "우라늄 가루" },     { "surface_powder", "표토 가루" },
            { "energy_crystal", "에너지 결정" },     { "magic_crystal", "마력 결정" },
            { "diamond", "다이아몬드" },            { "ruby", "루비" },
            { "sapphire", "사파이어" },

            // 기계 (Items 와 BlockBase 양쪽에 같은 ID 로 존재한다)
            { "Machine:ElectricPulverizer", "전기 분쇄기" },
            { "Machine:AlloySmelter", "합금 재련기" },
            { "Machine:BioIncubator", "유기물 배양기" },
            { "Machine:Compressor", "압축기" },
            { "Machine:Electrolyzer", "전기 분해기" },
            { "Machine:Extractor", "추출기" },
            { "Machine:LasorProcessor", "레이저 가공기" },
            { "CoreCrafter", "코어 조합기" },

            // 지형 (ID 의 wall: / floor: 접두사는 로직이므로 그대로 둔다)
            { "wall:stone", "돌" },
            { "floor:dirt", "흙" },
        };

        [MenuItem("Tools/Project Craft/Localize/Fill Korean Display Names")]
        public static void FillMenu() => Fill();

        /// <summary>표에 따라 displayName 을 채운다. 채운 개수를 반환.</summary>
        public static int Fill()
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            for (int i = 0; i < Table.GetLength(0); i++) map[Table[i, 0]] = Table[i, 1];

            int filled = 0;
            List<string> skipped = new List<string>();   // 이미 값이 있어 건너뜀
            List<string> missing = new List<string>();   // 표에 없는 ID

            filled += FillItems(map, skipped, missing);
            filled += FillBlocks(map, skipped, missing);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[KoreanNameMigration] displayName " + filled + "개 채움"
                + (skipped.Count > 0 ? "\n이미 값이 있어 건너뜀 (" + skipped.Count + "): " + string.Join(", ", skipped.ToArray()) : "")
                + (missing.Count > 0 ? "\n번역표에 없음 (" + missing.Count + "): " + string.Join(", ", missing.ToArray()) : ""));
            return filled;
        }

        private static int FillItems(Dictionary<string, string> map, List<string> skipped, List<string> missing)
        {
            int filled = 0;
            string[] guids = AssetDatabase.FindAssets("t:Items");

            for (int i = 0; i < guids.Length; i++)
            {
                Items item = AssetDatabase.LoadAssetAtPath<Items>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (item == null) continue;

                if (!map.TryGetValue(item.itemName, out string korean)) { missing.Add(item.itemName); continue; }
                if (!string.IsNullOrEmpty(item.displayName)) { skipped.Add(item.itemName); continue; }

                item.displayName = korean;
                EditorUtility.SetDirty(item);
                filled++;
            }
            return filled;
        }

        private static int FillBlocks(Dictionary<string, string> map, List<string> skipped, List<string> missing)
        {
            int filled = 0;
            string[] guids = AssetDatabase.FindAssets("t:BlockBase");

            for (int i = 0; i < guids.Length; i++)
            {
                BlockBase block = AssetDatabase.LoadAssetAtPath<BlockBase>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (block == null) continue;

                if (!map.TryGetValue(block.blockName, out string korean)) { missing.Add(block.blockName); continue; }
                if (!string.IsNullOrEmpty(block.displayName)) { skipped.Add(block.blockName); continue; }

                block.displayName = korean;
                EditorUtility.SetDirty(block);
                filled++;
            }
            return filled;
        }

        /// <summary>
        /// 기계는 Items.itemName 과 BlockBase.blockName 이 같아야 배치가 동작한다
        /// (PlaceableRecord(item.itemName) → GetGameObjectFromBlockDictionary(blockId)).
        /// 표시 이름도 어긋나면 UI 와 인벤토리가 다른 이름을 보여 주므로 함께 검사한다.
        /// </summary>
        [MenuItem("Tools/Project Craft/Localize/Verify Machine Name Pairs")]
        public static void VerifyMachinePairs()
        {
            Dictionary<string, Items> items = new Dictionary<string, Items>();
            string[] itemGuids = AssetDatabase.FindAssets("t:Items");
            for (int i = 0; i < itemGuids.Length; i++)
            {
                Items item = AssetDatabase.LoadAssetAtPath<Items>(AssetDatabase.GUIDToAssetPath(itemGuids[i]));
                if (item != null && !items.ContainsKey(item.itemName)) items[item.itemName] = item;
            }

            List<string> problems = new List<string>();
            int ok = 0;
            string[] blockGuids = AssetDatabase.FindAssets("t:MachineBlock");
            for (int i = 0; i < blockGuids.Length; i++)
            {
                MachineBlock block = AssetDatabase.LoadAssetAtPath<MachineBlock>(AssetDatabase.GUIDToAssetPath(blockGuids[i]));
                if (block == null) continue;

                if (!items.TryGetValue(block.blockName, out Items item))
                {
                    problems.Add(block.blockName + ": 같은 이름의 Items 가 없음(배치 불가)");
                    continue;
                }
                if (block.DisplayName != item.DisplayName)
                {
                    problems.Add(block.blockName + ": 표시 이름 불일치 (블록 '" + block.DisplayName + "' vs 아이템 '" + item.DisplayName + "')");
                    continue;
                }
                ok++;
            }

            Debug.Log("[KoreanNameMigration] 기계 이름 정합 " + ok + "개 정상"
                + (problems.Count > 0 ? "\n문제 " + problems.Count + "건:\n  " + string.Join("\n  ", problems.ToArray()) : ""));
        }
    }
}
