using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectCraft.EditorTools
{
    /// <summary>
    /// 추출기 계열과 그 이웃 기계(자원 생성기 · 펌프 · 지열 발전기)를 정식 기계로 만든다.
    ///
    /// 정본은 `자원과 그 가공방식.canvas` 다 — 메인자원을 분쇄기로 1/2/3회 분쇄한 것을 추출기에 넣어
    /// 부산물을 확률로 얻고, <b>{메인티어}-{등급}티어 추출기</b> 12종이 계열을 이룬다.
    ///
    /// <b>등급차를 레시피 복제로 표현하지 않는다.</b> 같은 계열은 <see cref="MachineBlock.recipeGroupId"/> 로
    /// 레시피 목록을 공유하고, <see cref="MachineBlock.tier"/> 가 "등급 N 은 0~N 의 산출을 모두 가진다" 를
    /// 그대로 구현하며, 속도·확률차는 <see cref="MachineBlock.speedMultiplier"/> / <c>chanceMultiplier</c> 가 낸다.
    ///
    /// 재실행 안전하다(이미 있는 에셋은 값만 갱신). 대화상자를 띄우지 않는다.
    /// </summary>
    public static class ExtractorSetup
    {
        private const string BlockFolder = "Assets/Prefabs/Blocks/Machines";
        private const string ItemFolder = "Assets/Prefabs/Items/Machines";
        private const string PlaceholderSprite = "Assets/Asset/assetPlaceHolder.png";
        private const string ReportPath = BlockFolder + "/_ExtractorReport.md";

        /// <summary>없앨 옛 기계. 캔버스에 없는 뭉뚱그린 "추출기" 다.</summary>
        private const string ObsoleteBlock = BlockFolder + "/Extractor.asset";
        private const string ObsoleteItem = ItemFolder + "/Extractor.asset";

        private class Spec
        {
            public string korean;
            public string id;
            public string group;       // 레시피를 공유하는 계열
            public int tier;           // 등급. tier 이하의 레시피를 전부 처리한다
            public float speed;
            public float chance;
            public int inputs;
            public int outputs;
            public bool power;

            public Spec(string korean, string id, string group, int tier, float speed, float chance,
                        int inputs, int outputs, bool power)
            {
                this.korean = korean; this.id = id; this.group = group; this.tier = tier;
                this.speed = speed; this.chance = chance;
                this.inputs = inputs; this.outputs = outputs; this.power = power;
            }
        }

        // 배율은 캔버스 그대로. 0-1(0티어 1.5배?)·0-2(1티어 1.5배?)는 물음표가 붙은 미정 메모라 1.0 으로 둔다.
        private static readonly Spec[] Specs =
        {
            // 0계열 — 메인자원 돌
            new Spec("수동 0-0티어 추출기", "Extractor00", "Extractor0", 0, 1f,  1f,   1, 1, false),
            new Spec("0-1티어 추출기",      "Extractor01", "Extractor0", 1, 1f,  1f,   1, 1, true),
            new Spec("0-2티어 추출기",      "Extractor02", "Extractor0", 2, 1f,  1f,   1, 1, true),
            new Spec("0-3티어 추출기",      "Extractor03", "Extractor0", 3, 1f,  1f,   1, 1, true),

            // 1계열 — 메인자원 마력석
            new Spec("1-0티어 추출기", "Extractor10", "Extractor1", 0, 1f, 1f, 1, 1, true),
            new Spec("1-1티어 추출기", "Extractor11", "Extractor1", 1, 1f, 1f, 1, 1, true),
            new Spec("1-2티어 추출기", "Extractor12", "Extractor1", 2, 2f, 1f, 1, 1, true),
            new Spec("1-3티어 추출기", "Extractor13", "Extractor1", 3, 4f, 2f, 1, 1, true),

            // 2계열 — 메인자원 운석
            new Spec("2-0티어 추출기", "Extractor20", "Extractor2", 0, 1f, 1f,   1, 1, true),
            new Spec("2-1티어 추출기", "Extractor21", "Extractor2", 1, 1f, 1.5f, 1, 1, true),
            new Spec("2-2티어 추출기", "Extractor22", "Extractor2", 2, 2f, 1f,   1, 1, true),
            new Spec("2-3티어 추출기", "Extractor23", "Extractor2", 3, 4f, 2f,   1, 1, true),

            // 지형에 설치해 메인자원·유체를 뽑는 쪽. 추출기와 개념이 달라 계열을 나눈다.
            // 입력 0 / 출력 1 인 이유: 지형에서 뽑으므로 넣을 것이 없다(출력이 1이라 3/6 폴백에 걸리지 않는다).
            new Spec("0티어 자원 생성기",       "ResourceGenerator0",     "ResourceGenerator", 0, 1f, 1f, 0, 1, true),
            new Spec("0티어 자원 생성기(강화)", "ResourceGenerator0Plus", "ResourceGenerator", 1, 2f, 1f, 0, 1, true),
            new Spec("펌프",                    "Pump",                   "Pump",              0, 1f, 1f, 0, 1, true),
            new Spec("지열 발전기",             "GeothermalGenerator",    "Geothermal",        0, 1f, 1f, 0, 1, false),
        };

        /// <summary>추출 레시피를 어느 기계로 옮길지. 값이 빈 문자열이면 "붙일 기계가 아직 없다".</summary>
        private static readonly string[,] RecipeMoves =
        {
            { "extract_conductor_powder", "Machine:Extractor00" },
            { "extract_normal",           "Machine:ResourceGenerator0" },
            { "extract_water",            "Machine:Pump" },
            { "extract_oil",              "Machine:Pump" },
            { "extract_crude_oil",        "Machine:Pump" },
            { "geothermal",               "Machine:GeothermalGenerator" },
            { "extract_ore",              "" },   // 1티어 자원 생성기가 아직 없다
            { "extract_meteorite",        "" },   // 2티어 자원 생성기가 아직 없다
        };

        private static readonly StringBuilder Report = new StringBuilder();

        [MenuItem("Tools/Project Craft/Machines/추출기 계열 설정")]
        public static void Run()
        {
            Report.Clear();
            Report.AppendLine("# 추출기 계열 설정");
            Report.AppendLine();
            Report.AppendLine("`Tools/Project Craft/Machines/추출기 계열 설정` 이 자동 생성한 파일입니다.");
            Report.AppendLine("정본은 `자원과 그 가공방식.canvas` 입니다.");
            Report.AppendLine();

            Sprite placeholder = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderSprite);
            if (placeholder == null) Report.AppendLine("- ⚠ 플레이스홀더 아이콘을 찾지 못했습니다.");

            int created = CreateMachines(placeholder);
            int moved = MoveRecipes();
            int removed = RemoveObsolete();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Report.AppendLine();
            Report.AppendLine($"- 기계 {Specs.Length}종 처리(새로 만든 것 {created}개) · 레시피 {moved}개 이동 · 옛 기계 {removed}개 삭제");
            Report.AppendLine();
            Report.AppendLine("이어서 `중복 아이템 통합` 을 돌리면 같은 이름의 플레이스홀더가 흡수됩니다.");

            File.WriteAllText(ReportPath, Report.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ReportPath);
            Debug.Log("[ExtractorSetup] 완료. 자세한 내역은 " + ReportPath);

            EditorApplication.ExecuteMenuItem("Tools/Project Craft/Dictionary/Register All Assets");
        }

        // ── 기계 생성 ─────────────────────────────────────────────

        private static int CreateMachines(Sprite placeholder)
        {
            Report.AppendLine("## 기계");
            Report.AppendLine();
            Report.AppendLine("| 기계 | ID | 계열 | 등급 | 속도 | 확률 | 상태 |");
            Report.AppendLine("|---|---|---|---|---|---|---|");

            int created = 0;
            foreach (Spec spec in Specs)
            {
                string blockPath = $"{BlockFolder}/{spec.id}.asset";
                MachineBlock block = AssetDatabase.LoadAssetAtPath<MachineBlock>(blockPath);
                bool isNew = block == null;
                if (isNew) block = ScriptableObject.CreateInstance<MachineBlock>();

                block.blockName = "Machine:" + spec.id;
                block.displayName = spec.korean;
                block.recipeGroupId = spec.group;
                block.tier = spec.tier;
                block.speedMultiplier = spec.speed;
                block.chanceMultiplier = spec.chance;
                block.inputSlotCount = spec.inputs;
                block.outputSlotCount = spec.outputs;
                block.fuelSlotCount = 0;
                block.isUseEnergy = spec.power;
                block.maxEnergyAmount = spec.power ? 1000f : 0f;
                if (block.machinePrefab == null)
                    block.machinePrefab = MachineBlockFiller.EnsureWorldPrefab(spec.id, placeholder);

                if (isNew) { AssetDatabase.CreateAsset(block, blockPath); created++; }
                else EditorUtility.SetDirty(block);

                EnsurePlacementItem(spec, placeholder);

                Report.AppendLine($"| {spec.korean} | `{block.blockName}` | {spec.group} | {spec.tier} "
                    + $"| ×{spec.speed} | ×{spec.chance} | {(isNew ? "생성" : "갱신")} |");
            }
            Report.AppendLine();
            return created;
        }

        /// <summary>배치하려면 <c>blockName == itemName</c> 인 아이템이 있어야 한다(규약).</summary>
        private static void EnsurePlacementItem(Spec spec, Sprite placeholder)
        {
            string path = $"{ItemFolder}/{spec.id}.asset";
            Items item = AssetDatabase.LoadAssetAtPath<Items>(path);
            bool isNew = item == null;
            if (isNew) item = ScriptableObject.CreateInstance<Items>();

            item.itemName = "Machine:" + spec.id;
            item.displayName = spec.korean;
            item.placeable = true;
            item.maxStack = 64;
            if (item.Icon == null) item.Icon = placeholder;   // 손으로 꽂은 그림은 덮지 않는다

            if (isNew) AssetDatabase.CreateAsset(item, path);
            else EditorUtility.SetDirty(item);
        }

        // ── 레시피 이동 ───────────────────────────────────────────

        private static int MoveRecipes()
        {
            Dictionary<string, MachineBlock> byName = new Dictionary<string, MachineBlock>();
            foreach (string guid in AssetDatabase.FindAssets("t:MachineBlock"))
            {
                MachineBlock block = AssetDatabase.LoadAssetAtPath<MachineBlock>(AssetDatabase.GUIDToAssetPath(guid));
                if (block != null && !byName.ContainsKey(block.blockName)) byName[block.blockName] = block;
            }

            Report.AppendLine("## 추출 레시피 재배정");
            Report.AppendLine();
            Report.AppendLine("`extraction.json` 은 `terrain` 필드가 붙은 **지형 설치형**이라, 분쇄물을 넣는 캔버스의 추출기와 개념이 다르다.");
            Report.AppendLine();
            Report.AppendLine("| 레시피 | 옮긴 곳 |");
            Report.AppendLine("|---|---|");

            int moved = 0;
            for (int i = 0; i < RecipeMoves.GetLength(0); i++)
            {
                string id = RecipeMoves[i, 0];
                string target = RecipeMoves[i, 1];

                List<Recipe> found = FindRecipes(id);
                if (found.Count == 0)
                {
                    Report.AppendLine($"| `{id}` | — 레시피가 없음 |");
                    continue;
                }

                if (string.IsNullOrEmpty(target))
                {
                    foreach (Recipe recipe in found)
                    {
                        recipe.machine = null;   // 붙일 기계가 없다. 비워 두는 편이 틀린 기계에 붙는 것보다 낫다
                        EditorUtility.SetDirty(recipe);
                    }
                    Report.AppendLine($"| `{id}` | ⚠ **비움** — 1·2티어 자원 생성기가 아직 없음 ({found.Count}개) |");
                    continue;
                }

                if (!byName.TryGetValue(target, out MachineBlock block))
                {
                    Report.AppendLine($"| `{id}` | ⚠ `{target}` 을 찾지 못함 |");
                    continue;
                }

                foreach (Recipe recipe in found)
                {
                    recipe.machine = block;
                    EditorUtility.SetDirty(recipe);
                    moved++;
                }
                Report.AppendLine($"| `{id}` | `{target}` ({block.DisplayName}) · {found.Count}개 |");
            }
            Report.AppendLine();
            return moved;
        }

        private static List<Recipe> FindRecipes(string id)
        {
            List<Recipe> result = new List<Recipe>();
            foreach (string guid in AssetDatabase.FindAssets("t:Recipe " + id))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("/" + id + ".asset")) continue;

                Recipe recipe = AssetDatabase.LoadAssetAtPath<Recipe>(path);
                if (recipe != null) result.Add(recipe);
            }
            return result;
        }

        // ── 옛 기계 삭제 ──────────────────────────────────────────

        /// <summary>참조가 남아 있으면 지우지 않는다. 조용히 지우면 레시피의 기계가 빈다.</summary>
        private static int RemoveObsolete()
        {
            Report.AppendLine("## 옛 '추출기' 삭제");
            Report.AppendLine();

            MachineBlock block = AssetDatabase.LoadAssetAtPath<MachineBlock>(ObsoleteBlock);
            if (block == null)
            {
                Report.AppendLine("- 이미 없습니다.");
                Report.AppendLine();
                return 0;
            }

            List<string> users = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Recipe"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Recipe recipe = AssetDatabase.LoadAssetAtPath<Recipe>(path);
                if (recipe != null && recipe.machine == block) users.Add(path);
            }

            if (users.Count > 0)
            {
                Report.AppendLine($"- ⚠ 아직 {users.Count}개 레시피가 기계로 쓰고 있어 지우지 않았습니다:");
                foreach (string path in users) Report.AppendLine($"  - `{path}`");
                Report.AppendLine();
                return 0;
            }

            int removed = 0;
            if (AssetDatabase.DeleteAsset(ObsoleteBlock)) removed++;
            if (AssetDatabase.DeleteAsset(ObsoleteItem)) removed++;
            Report.AppendLine($"- `{ObsoleteBlock}` · `{ObsoleteItem}` 삭제 ({removed}개)");
            Report.AppendLine();
            return removed;
        }
    }
}
