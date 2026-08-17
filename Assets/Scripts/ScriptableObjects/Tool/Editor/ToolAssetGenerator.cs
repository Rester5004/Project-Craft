using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectCraft.EditorTools
{
    /// <summary>재질 하나의 초기 수치. 인스펙터에서 다듬을 수 있으므로 여기 값은 출발점일 뿐이다.</summary>
    internal class MaterialSpec
    {
        public string id;
        public string korean;
        public bool isMetal;
        public float durabilityFactor;
        public float handleFactor;
        public int miningTier;
        /// <summary>부품을 만들 때 넣는 아이템의 itemName. 비면 그 재질은 만들 수 없다(나무).</summary>
        public string sourceItemName;

        public MaterialSpec(string id, string korean, bool isMetal, float durabilityFactor, float handleFactor,
                            int miningTier, string sourceItemName)
        {
            this.id = id;
            this.korean = korean;
            this.isMetal = isMetal;
            this.durabilityFactor = durabilityFactor;
            this.handleFactor = handleFactor;
            this.miningTier = miningTier;
            this.sourceItemName = sourceItemName;
        }
    }

    /// <summary>
    /// 도구 체계의 에셋을 한 번에 만들어 준다(재질 · 부품 종류 · 부품 아이템 · 설계도 · 도구 아이템 ·
    /// 스프라이트 라이브러리 · 도구 카테고리 · 도구 레시피), 딕셔너리에 등록하고,
    /// 임포터가 만들어 둔 중복 플레이스홀더를 정리한다.
    ///
    /// 재실행해도 안전하다 — 이미 있는 에셋은 손대지 않으므로 손으로 다듬은 수치가 보존된다.
    /// 자동화 중 멈추지 않도록 대화상자를 띄우지 않는다(다른 팩토리와 같은 규약).
    /// </summary>
    public static class ToolAssetGenerator
    {
        private const string MaterialFolder = "Assets/Prefabs/Tools/Materials";
        private const string KindFolder = "Assets/Prefabs/Tools/PartKinds";
        private const string DefinitionFolder = "Assets/Prefabs/Tools/Definitions";
        private const string PartItemFolder = "Assets/Prefabs/Items/ToolParts";
        private const string ToolItemFolder = "Assets/Prefabs/Items/Tools";
        private const string RecipeFolder = "Assets/Prefabs/Recipes/Tools";
        private const string CategoryFolder = "Assets/Prefabs/Recipes/Category";
        private const string LibraryPath = "Assets/Prefabs/Tools/ToolSpriteLibrary.asset";
        private const string ReportPath = "Assets/Prefabs/Tools/_GenerateReport.md";
        private const string CoreCrafterPath = "Assets/Prefabs/Blocks/Machines/CoreCrafter.asset";

        private static readonly string[] SpriteSheets =
        {
            "Assets/Asset/ItemImages/ToolImages/hammer_and_@.png",
            "Assets/Asset/ItemImages/ToolImages/pickaxe_head.png",
            "Assets/Asset/ItemImages/ToolImages/knife.png",
            // 판은 도구 그림이 아니지만 부품 종류가 되면서 여기서 아이콘을 가져간다.
            // ⚠ stone_plate·wood_plate 만 없다 — 그 둘은 아이콘 없이 만들어진다.
            "Assets/Asset/ItemImages/project_craft_metal_plates.png",
        };

        // 도구에 쓸 수 있는 재질 16종(사용자 지정 순서: 나무/돌/철/구리/금/주석/석영/니켈/오스뮴/은/납/티타늄/알루미늄/우라늄/리튬/토륨)
        private static readonly MaterialSpec[] Materials =
        {
            // 마지막 열 = 부품을 만들 때 넣는 아이템. <b>나무는 게임에 아이템이 없어 비운다</b>
            // (그래서 나무 부품은 만들 수 없다 — 시작 도구는 돌이다).
            new MaterialSpec("wood",     "나무",     false, 0.25f, 0.90f, 0, ""),
            new MaterialSpec("stone",    "돌",       false, 0.50f, 0.95f, 1, "stone"),
            new MaterialSpec("iron",     "철",       true,  1.00f, 1.10f, 2, "iron_ingot"),
            new MaterialSpec("copper",   "구리",     true,  0.70f, 1.00f, 2, "copper_ingot"),
            new MaterialSpec("gold",     "금",       true,  0.20f, 0.90f, 2, "gold_ingot"),
            new MaterialSpec("tin",      "주석",     true,  0.60f, 0.95f, 1, "tin_ingot"),
            new MaterialSpec("quartz",   "석영",     false, 0.80f, 0.85f, 2, "quartz_crystal"),
            new MaterialSpec("nickel",   "니켈",     true,  1.20f, 1.15f, 3, "nickel_ingot"),
            new MaterialSpec("osmium",   "오스뮴",   true,  2.20f, 1.30f, 4, "osmium_ingot"),
            new MaterialSpec("silver",   "은",       true,  0.80f, 1.00f, 2, "silver_ingot"),
            new MaterialSpec("lead",     "납",       true,  0.50f, 1.20f, 1, "lead_ingot"),
            new MaterialSpec("titanium", "티타늄",   true,  2.00f, 1.25f, 4, "titanium_ingot"),
            new MaterialSpec("aluminum", "알루미늄", true,  0.90f, 0.90f, 3, "aluminum_ingot"),
            new MaterialSpec("uranium",  "우라늄",   true,  1.50f, 1.10f, 4, "uranium_ingot"),
            new MaterialSpec("lithium",  "리튬",     true,  0.60f, 0.80f, 3, "lithium_ingot"),
            new MaterialSpec("thorium",  "토륨",     true,  1.80f, 1.15f, 5, "thorium_ingot"),
        };

        // 부품 종류: id, 한글, 아이템 이름 접미사("{material}_rod"), 표시 이름 접미사("철 막대")
        private static readonly string[,] Kinds =
        {
            { "rod",          "막대",       "_rod" },
            { "hammer_head",  "망치 머리",   "_hammer_head" },
            { "pickaxe_head", "곡괭이 머리", "_pickaxe_head" },
            { "blade",        "칼날",       "_blade" },
            // 판은 도구 부품은 아니지만 <b>재질마다 하나</b>라는 구조가 같아 같은 표를 쓴다.
            // 조합대에서 재질 칸에 주괴를 올리면 그 재질의 판이 나온다(레시피 하나).
            { "plate",        "판",         "_plate" },
        };

        // 삭제할 중복 플레이스홀더 → 대체할 새 아이템의 itemName("" 이면 참조를 도구 요구로 옮긴다)
        private static readonly string[,] Superseded =
        {
            { "막대",          "wood_rod" },
            { "망치 머리",      "wood_hammer_head" },
            { "철 망치 머리",   "iron_hammer_head" },
            { "돌 망치 머리",   "stone_hammer_head" },
            { "망치",          "tool_hammer" },
            { "철 망치",        "tool_hammer" },
            { "돌 망치",        "tool_hammer" },
            { "드라이버",       "tool_driver" },
        };

        private static readonly StringBuilder Report = new StringBuilder();

        [MenuItem("Tools/Project Craft/Tool/Generate Tool Assets")]
        public static void Generate()
        {
            Report.Clear();
            Report.AppendLine("# 도구 에셋 생성 보고서");
            Report.AppendLine();
            Report.AppendLine("`Tools/Project Craft/Tool/Generate Tool Assets` 가 자동 생성한 파일입니다.");
            Report.AppendLine();

            EnsureFolder(MaterialFolder);
            EnsureFolder(KindFolder);
            EnsureFolder(DefinitionFolder);
            EnsureFolder(PartItemFolder);
            EnsureFolder(ToolItemFolder);
            EnsureFolder(RecipeFolder);

            Dictionary<string, Sprite> sprites = CollectSprites();
            ToolSpriteLibrary library = BuildLibrary(sprites);

            Dictionary<string, ToolMaterial> materials = BuildMaterials(sprites);
            Dictionary<string, ToolPartKind> kinds = BuildKinds();
            List<ToolPartItem> parts = BuildPartItems(materials, kinds, sprites);
            Dictionary<string, ToolDefinition> definitions = BuildDefinitions(materials, kinds, sprites);
            List<ToolItem> toolItems = BuildToolItems(definitions, sprites);

            RecipeCategory category = BuildCategory(sprites);
            List<Recipe> recipes = BuildRecipes(definitions, toolItems, category);

            CleanupSuperseded(definitions);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RegisterInScene(materials, kinds, parts, toolItems, library, recipes);

            File.WriteAllText(ReportPath, Report.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ReportPath);

            Debug.Log("[ToolAssetGenerator] 완료. 자세한 내역은 " + ReportPath);
        }

        // ── 스프라이트 ────────────────────────────────────────────
        /// <summary>도구 시트의 이름 붙은 서브 스프라이트를 모은다(자동 이름 <c>시트_숫자</c> 는 제외).</summary>
        private static Dictionary<string, Sprite> CollectSprites()
        {
            Dictionary<string, Sprite> result = new Dictionary<string, Sprite>();
            int skipped = 0;

            foreach (string path in SpriteSheets)
            {
                string fileBase = Path.GetFileNameWithoutExtension(path);
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                if (assets == null || assets.Length == 0)
                {
                    Debug.LogError("[ToolAssetGenerator] 스프라이트 시트를 찾지 못했습니다: " + path);
                    continue;
                }

                foreach (Object asset in assets)
                {
                    if (asset is not Sprite sprite) continue;
                    if (IsAutoName(sprite.name, fileBase)) { skipped++; continue; }
                    if (!result.ContainsKey(sprite.name)) result[sprite.name] = sprite;
                }
            }

            Report.AppendLine($"- 스프라이트 {result.Count}개 수집(이름 없는 자동 스프라이트 {skipped}개 제외)");
            return result;
        }

        /// <summary>"hammer_and_@_65" 처럼 시트 이름 + 숫자인 자동 이름인가.</summary>
        private static bool IsAutoName(string spriteName, string fileBase)
        {
            string prefix = fileBase + "_";
            if (!spriteName.StartsWith(prefix)) return false;

            string tail = spriteName.Substring(prefix.Length);
            if (tail.Length == 0) return false;
            foreach (char c in tail) if (!char.IsDigit(c)) return false;
            return true;
        }

        private static ToolSpriteLibrary BuildLibrary(Dictionary<string, Sprite> sprites)
        {
            ToolSpriteLibrary library = AssetDatabase.LoadAssetAtPath<ToolSpriteLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ToolSpriteLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            // 파생 데이터라 매번 새로 채운다(이름이 바뀌어도 따라간다).
            library.sprites = new List<Sprite>(sprites.Values);
            library.Invalidate();
            EditorUtility.SetDirty(library);

            Report.AppendLine($"- 스프라이트 라이브러리 {library.sprites.Count}장 등록");
            return library;
        }

        /// <summary>
        /// 재질 색을 뽑는다. 시트가 isReadable=0 이라 GetPixels 를 못 쓰므로
        /// PNG 를 직접 읽어 임시 텍스처로 올린 뒤 해당 서브 스프라이트 영역만 평균낸다.
        /// </summary>
        private static Color SampleTint(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return Color.white;

            string path = AssetDatabase.GetAssetPath(sprite.texture);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return Color.white;

            Texture2D readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(readable, File.ReadAllBytes(path))) return Color.white;

                Rect rect = sprite.rect;
                Color[] pixels = readable.GetPixels(
                    Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y),
                    Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height));

                float r = 0f, g = 0f, b = 0f;
                int count = 0;
                foreach (Color pixel in pixels)
                {
                    if (pixel.a < 0.5f) continue;
                    r += pixel.r; g += pixel.g; b += pixel.b;
                    count++;
                }
                if (count == 0) return Color.white;
                return new Color(r / count, g / count, b / count, 1f);
            }
            finally
            {
                Object.DestroyImmediate(readable);
            }
        }

        // ── 재질 · 부품 종류 ──────────────────────────────────────
        private static Dictionary<string, ToolMaterial> BuildMaterials(Dictionary<string, Sprite> sprites)
        {
            Dictionary<string, ToolMaterial> result = new Dictionary<string, ToolMaterial>();
            int created = 0;
            int filledSource = 0;

            foreach (MaterialSpec spec in Materials)
            {
                string path = $"{MaterialFolder}/{spec.id}.asset";
                ToolMaterial material = AssetDatabase.LoadAssetAtPath<ToolMaterial>(path);
                if (material == null)
                {
                    material = ScriptableObject.CreateInstance<ToolMaterial>();
                    material.materialId = spec.id;
                    material.displayName = spec.korean;
                    material.isMetal = spec.isMetal;
                    material.durabilityFactor = spec.durabilityFactor;
                    material.handleFactor = spec.handleFactor;
                    material.miningTier = spec.miningTier;
                    // 막대 스프라이트가 그 재질의 실제 도구 색이라 틴트 기준으로 가장 알맞다.
                    sprites.TryGetValue(spec.id + "_rod", out Sprite rod);
                    material.tint = SampleTint(rod);
                    AssetDatabase.CreateAsset(material, path);
                    created++;
                }

                // sourceItem 은 나중에 생긴 필드라 기존 16개 에셋이 전부 비어 있다.
                // <b>비어 있을 때만</b> 채운다 — 손으로 바꾼 값을 되돌리지 않기 위해서다(다른 팩토리와 같은 규약).
                if (material.sourceItem == null && !string.IsNullOrEmpty(spec.sourceItemName))
                {
                    material.sourceItem = FindItemByName(spec.sourceItemName);
                    if (material.sourceItem == null)
                        Report.AppendLine($"- ⚠ 재질 `{spec.id}` 의 재료 아이템 `{spec.sourceItemName}` 을 찾지 못했습니다.");
                    else { EditorUtility.SetDirty(material); filledSource++; }
                }
                result[spec.id] = material;
            }

            Report.AppendLine($"- 재질 {result.Count}종 (새로 만든 것 {created}개 · 재료 아이템을 채운 것 {filledSource}개)");
            return result;
        }

        private static Dictionary<string, ToolPartKind> BuildKinds()
        {
            Dictionary<string, ToolPartKind> result = new Dictionary<string, ToolPartKind>();
            int created = 0;

            for (int i = 0; i < Kinds.GetLength(0); i++)
            {
                string id = Kinds[i, 0];
                string path = $"{KindFolder}/{id}.asset";
                ToolPartKind kind = AssetDatabase.LoadAssetAtPath<ToolPartKind>(path);
                if (kind == null)
                {
                    kind = ScriptableObject.CreateInstance<ToolPartKind>();
                    kind.kindId = id;
                    kind.displayName = Kinds[i, 1];
                    AssetDatabase.CreateAsset(kind, path);
                    created++;
                }
                result[id] = kind;
            }

            Report.AppendLine($"- 부품 종류 {result.Count}종 (새로 만든 것 {created}개)");
            return result;
        }

        // ── 부품 아이템 ───────────────────────────────────────────
        private static List<ToolPartItem> BuildPartItems(
            Dictionary<string, ToolMaterial> materials,
            Dictionary<string, ToolPartKind> kinds,
            Dictionary<string, Sprite> sprites)
        {
            List<ToolPartItem> result = new List<ToolPartItem>();
            List<string> missingSprites = new List<string>();
            List<string> adopted = new List<string>();
            int created = 0;

            // ⚠ <b>이름이 같은 아이템이 다른 폴더에 있으면 새로 만들면 안 된다.</b>
            // 예전에는 자기 폴더(PartItemFolder)만 봐서, 판처럼 이미 Placeholder 에 있던 것을
            // <b>하나 더 만들어</b> itemName 이 겹쳤다(세이브 키가 겹치면 조회가 어느 쪽인지 알 수 없다).
            Dictionary<string, ToolPartItem> existing = new Dictionary<string, ToolPartItem>();
            foreach (string guid in AssetDatabase.FindAssets("t:ToolPartItem"))
            {
                ToolPartItem it = AssetDatabase.LoadAssetAtPath<ToolPartItem>(AssetDatabase.GUIDToAssetPath(guid));
                if (it != null && !string.IsNullOrEmpty(it.itemName)) existing[it.itemName] = it;
            }

            foreach (MaterialSpec spec in Materials)
            {
                for (int k = 0; k < Kinds.GetLength(0); k++)
                {
                    string kindId = Kinds[k, 0];
                    string itemName = spec.id + Kinds[k, 2];
                    string path = $"{PartItemFolder}/{itemName}.asset";

                    ToolPartItem part = AssetDatabase.LoadAssetAtPath<ToolPartItem>(path);
                    if (part == null && existing.TryGetValue(itemName, out ToolPartItem elsewhere))
                    {
                        // 다른 폴더에 이미 있다(판 4종을 손으로 승격시킨 경우). 그대로 쓴다.
                        part = elsewhere;
                        adopted.Add(itemName);
                    }
                    if (part == null)
                    {
                        part = ScriptableObject.CreateInstance<ToolPartItem>();
                        part.itemName = itemName;
                        part.displayName = spec.korean + " " + Kinds[k, 1];
                        part.maxStack = 64;
                        part.placeable = false;
                        part.kind = kinds[kindId];
                        part.material = materials[spec.id];

                        if (sprites.TryGetValue(itemName, out Sprite icon)) part.Icon = icon;
                        else missingSprites.Add(itemName);

                        AssetDatabase.CreateAsset(part, path);
                        created++;
                    }
                    result.Add(part);
                }
            }

            Report.AppendLine($"- 부품 아이템 {result.Count}개 (새로 만든 것 {created}개)");
            if (adopted.Count > 0)
                Report.AppendLine($"  - 다른 폴더에 이미 있어 그대로 쓴 것 {adopted.Count}개: {string.Join(", ", adopted)}");
            if (missingSprites.Count > 0)
                Report.AppendLine($"  - ⚠ 스프라이트를 못 찾은 부품 {missingSprites.Count}개: {string.Join(", ", missingSprites)}");
            return result;
        }

        // ── 설계도 · 도구 아이템 ──────────────────────────────────
        private static Dictionary<string, ToolDefinition> BuildDefinitions(
            Dictionary<string, ToolMaterial> materials,
            Dictionary<string, ToolPartKind> kinds,
            Dictionary<string, Sprite> sprites)
        {
            List<ToolMaterial> all = new List<ToolMaterial>();
            foreach (MaterialSpec spec in Materials) all.Add(materials[spec.id]);

            Dictionary<string, ToolDefinition> result = new Dictionary<string, ToolDefinition>();

            // 곡괭이: 구멍 뚫린 막대({m}_hammer) 위에 곡괭이 머리를 얹는다.
            result["pickaxe"] = EnsureDefinition("pickaxe", "곡괭이", 100, Sprite(sprites, "iron_pickaxe_head"), new[]
            {
                Slot(kinds["rod"], MaterialFilter.Curated, all, "{material}_hammer", false),
                Slot(kinds["pickaxe_head"], MaterialFilter.Curated, all, "{material}_pickaxe_head", false),
            });

            result["hammer"] = EnsureDefinition("hammer", "망치", 100, Sprite(sprites, "iron_hammer"), new[]
            {
                Slot(kinds["rod"], MaterialFilter.Curated, all, "{material}_hammer", false),
                Slot(kinds["hammer_head"], MaterialFilter.Curated, all, "{material}_hammer_head", false),
            });

            // 칼: 망치와 같은 꼴이다(자루 + 머리). 자루 그림은 망치·곡괭이와 공유하고
            // 날만 {material}_blade 로 갈아 끼운다. 목록 아이콘은 미리 합쳐진 {m}_knife 를 쓴다.
            result["knife"] = EnsureDefinition("knife", "칼", 80, Sprite(sprites, "iron_knife"), new[]
            {
                Slot(kinds["rod"], MaterialFilter.Curated, all, "{material}_hammer", false),
                Slot(kinds["blade"], MaterialFilter.Curated, all, "{material}_blade", false),
            });

            // 드라이버: 부품이 막대 하나뿐이고 재질별 그림이 없어 공용 스프라이트를 재질 색으로 물들인다.
            // 금속이면 무엇이든 되므로 나중에 합금이 늘어도 자동으로 허용된다.
            result["driver"] = EnsureDefinition("driver", "드라이버", 60, Sprite(sprites, "driver"), new[]
            {
                Slot(kinds["rod"], MaterialFilter.AnyMetal, null, "driver", true),
            });

            Report.AppendLine($"- 도구 설계도 {result.Count}종");
            return result;
        }

        private static Sprite Sprite(Dictionary<string, Sprite> sprites, string name)
        {
            if (sprites.TryGetValue(name, out Sprite sprite)) return sprite;
            Report.AppendLine($"  - ⚠ 스프라이트 '{name}' 을 찾지 못했습니다.");
            return null;
        }

        private static ToolPartSlot Slot(ToolPartKind kind, MaterialFilter filter,
            List<ToolMaterial> curated, string pattern, bool tint)
        {
            return new ToolPartSlot
            {
                kind = kind,
                filter = filter,
                curated = curated != null ? new List<ToolMaterial>(curated) : new List<ToolMaterial>(),
                layerSpritePattern = pattern,
                tintByMaterial = tint,
            };
        }

        private static ToolDefinition EnsureDefinition(string id, string korean, int durability,
            Sprite icon, ToolPartSlot[] slots)
        {
            string path = $"{DefinitionFolder}/{id}.asset";
            ToolDefinition definition = AssetDatabase.LoadAssetAtPath<ToolDefinition>(path);
            if (definition != null) return definition;

            definition = ScriptableObject.CreateInstance<ToolDefinition>();
            definition.toolId = id;
            definition.displayName = korean;
            definition.baseDurability = durability;
            definition.listIcon = icon;
            definition.slots = new List<ToolPartSlot>(slots);
            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }

        private static List<ToolItem> BuildToolItems(Dictionary<string, ToolDefinition> definitions,
            Dictionary<string, Sprite> sprites)
        {
            List<ToolItem> result = new List<ToolItem>();
            foreach (KeyValuePair<string, ToolDefinition> pair in definitions)
            {
                string itemName = "tool_" + pair.Key;
                string path = $"{ToolItemFolder}/{itemName}.asset";

                ToolItem item = AssetDatabase.LoadAssetAtPath<ToolItem>(path);
                if (item == null)
                {
                    item = ScriptableObject.CreateInstance<ToolItem>();
                    item.itemName = itemName;
                    item.displayName = pair.Value.DisplayName;
                    item.maxStack = 1;          // 개체마다 재질·내구도가 달라 합쳐질 수 없다
                    item.placeable = false;
                    item.Icon = pair.Value.listIcon;
                    item.definition = pair.Value;
                    AssetDatabase.CreateAsset(item, path);
                }
                result.Add(item);
            }

            Report.AppendLine($"- 도구 아이템 {result.Count}개");
            return result;
        }

        // ── 카테고리 · 레시피 ─────────────────────────────────────
        private static RecipeCategory BuildCategory(Dictionary<string, Sprite> sprites)
        {
            string path = $"{CategoryFolder}/tool.asset";
            RecipeCategory category = AssetDatabase.LoadAssetAtPath<RecipeCategory>(path);
            if (category != null) return category;

            category = ScriptableObject.CreateInstance<RecipeCategory>();
            category.displayName = "도구";
            category.icon = Sprite(sprites, "iron_pickaxe_head");
            category.sortOrder = 3;      // resource(0) · machine(1) · block(2) 다음
            AssetDatabase.CreateAsset(category, path);

            Report.AppendLine("- 도구 카테고리 생성 (탭 아이콘: iron_pickaxe_head)");
            return category;
        }

        /// <summary>곡괭이 → 망치 → 드라이버 순서로 만든다(등록 순서가 곧 탭 안의 표시 순서).</summary>
        private static List<Recipe> BuildRecipes(Dictionary<string, ToolDefinition> definitions,
            List<ToolItem> toolItems, RecipeCategory category)
        {
            MachineBlock coreCrafter = AssetDatabase.LoadAssetAtPath<MachineBlock>(CoreCrafterPath);
            if (coreCrafter == null)
                Report.AppendLine("  - ⚠ CoreCrafter 블록을 찾지 못해 레시피에 기계를 연결하지 못했습니다.");

            string[] order = { "pickaxe", "hammer", "driver", "knife" };
            List<Recipe> result = new List<Recipe>();

            foreach (string id in order)
            {
                ToolDefinition definition = definitions[id];
                ToolItem item = toolItems.Find(t => t.definition == definition);

                // <b>레시피 에셋 이름은 만들어지는 것의 이름 하나뿐이다</b>(`pickaxe` `hammer` `driver`).
                // 예전에는 여기서 `Craft_Pickaxe` 로 만들었는데, 정본이 개명된 뒤에도 이 줄이 그대로라
                // <b>툴을 다시 돌릴 때마다 같은 도구의 레시피가 하나 더 생겨</b> 서로를 가렸다.
                string path = $"{RecipeFolder}/{id}.asset";
                string legacy = $"{RecipeFolder}/Craft_{char.ToUpper(id[0])}{id.Substring(1)}.asset";
                if (AssetDatabase.LoadAssetAtPath<ToolRecipe>(legacy) != null)
                {
                    AssetDatabase.DeleteAsset(legacy);
                    Report.AppendLine($"  - 옛 이름 `{legacy}` 를 지웠습니다(정본은 `{path}`).");
                }

                ToolRecipe recipe = AssetDatabase.LoadAssetAtPath<ToolRecipe>(path);
                if (recipe == null)
                {
                    recipe = ScriptableObject.CreateInstance<ToolRecipe>();
                    recipe.machine = coreCrafter;
                    recipe.category = category;
                    recipe.tier = 0;
                    recipe.craftTime = 0f;
                    recipe.tool = definition;
                    recipe.inputs = new List<ItemStack>();      // 재료는 부품 칸이 대신한다
                    recipe.outputs = new List<ItemStack> { new ItemStack { item = item, count = 1 } };
                    AssetDatabase.CreateAsset(recipe, path);
                }
                result.Add(recipe);
            }

            Report.AppendLine($"- 도구 레시피 {result.Count}개 (곡괭이 → 망치 → 드라이버 순)");
            return result;
        }

        // ── 중복 플레이스홀더 정리 ────────────────────────────────
        /// <summary>
        /// 임포터가 만든 중복 아이템을 지우기 전에, 이를 참조하던 레시피를 새 아이템으로 옮긴다.
        /// 완성 도구가 재료로 쓰였으면 <see cref="Recipe.requiredTools"/> 로 옮긴다(도구는 소모가 아니라 내구도 차감).
        /// </summary>
        private static void CleanupSuperseded(Dictionary<string, ToolDefinition> definitions)
        {
            Report.AppendLine();
            Report.AppendLine("## 중복 플레이스홀더 정리");
            Report.AppendLine();

            // 이름 → 삭제 대상 아이템, 그리고 대체할 새 아이템
            Dictionary<Items, Items> replacement = new Dictionary<Items, Items>();
            List<Items> doomed = new List<Items>();
            Dictionary<Items, ToolDefinition> toolOf = new Dictionary<Items, ToolDefinition>();

            for (int i = 0; i < Superseded.GetLength(0); i++)
            {
                string oldName = Superseded[i, 0];
                string newName = Superseded[i, 1];

                Items oldItem = FindItemByName(oldName);
                if (oldItem == null) continue;

                Items newItem = FindItemByName(newName);
                if (newItem == null)
                {
                    Report.AppendLine($"- ⚠ '{oldName}' 의 대체 아이템 '{newName}' 을 찾지 못해 삭제하지 않았습니다.");
                    continue;
                }

                doomed.Add(oldItem);
                replacement[oldItem] = newItem;
                if (newItem is ToolItem tool && tool.definition != null) toolOf[oldItem] = tool.definition;
            }

            if (doomed.Count == 0)
            {
                Report.AppendLine("- 정리할 대상이 없습니다(이미 삭제됨).");
                return;
            }

            int rewritten = 0;
            List<string> obsoleteRecipes = new List<string>();

            string[] guids = AssetDatabase.FindAssets("t:Recipe");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Recipe recipe = AssetDatabase.LoadAssetAtPath<Recipe>(path);
                if (recipe == null) continue;

                bool changed = false;

                // 입력: 부품이면 새 부품으로 치환, 완성 도구면 requiredTools 로 이동
                for (int i = recipe.inputs.Count - 1; i >= 0; i--)
                {
                    ItemStack need = recipe.inputs[i];
                    if (need == null || need.item == null || !replacement.TryGetValue(need.item, out Items to)) continue;

                    if (toolOf.TryGetValue(need.item, out ToolDefinition definition))
                    {
                        recipe.inputs.RemoveAt(i);
                        if (recipe.requiredTools == null) recipe.requiredTools = new List<ToolRequirement>();
                        if (!recipe.requiredTools.Exists(r => r.tool == definition))
                            recipe.requiredTools.Add(new ToolRequirement { tool = definition, durabilityCost = 1 });
                    }
                    else
                    {
                        need.item = to;
                    }
                    changed = true;
                }

                // 출력: 완성 도구는 새 ToolRecipe 가 대신하므로 참조만 끊고 보고한다
                for (int i = 0; i < recipe.outputs.Count; i++)
                {
                    ItemStack produce = recipe.outputs[i];
                    if (produce == null || produce.item == null || !replacement.ContainsKey(produce.item)) continue;

                    if (toolOf.ContainsKey(produce.item))
                    {
                        obsoleteRecipes.Add(path);
                        produce.item = null;
                    }
                    else
                    {
                        produce.item = replacement[produce.item];
                    }
                    changed = true;
                }

                if (!changed) continue;
                EditorUtility.SetDirty(recipe);
                rewritten++;
            }

            AssetDatabase.SaveAssets();

            int deleted = 0;
            foreach (Items item in doomed)
            {
                string path = AssetDatabase.GetAssetPath(item);
                Report.AppendLine($"- 삭제: `{path}` → `{replacement[item].itemName}` 로 대체");
                if (AssetDatabase.DeleteAsset(path)) deleted++;
            }

            Report.AppendLine();
            Report.AppendLine($"- 참조를 고친 레시피 {rewritten}개, 삭제한 아이템 {deleted}개");
            if (obsoleteRecipes.Count > 0)
            {
                Report.AppendLine();
                Report.AppendLine($"### 새 도구 레시피로 대체되어 산출물이 비워진 레시피 {obsoleteRecipes.Count}개");
                Report.AppendLine();
                foreach (string path in obsoleteRecipes) Report.AppendLine($"- `{path}`");
            }
        }

        private static Items FindItemByName(string itemName)
        {
            string[] guids = AssetDatabase.FindAssets("t:Items");
            foreach (string guid in guids)
            {
                Items item = AssetDatabase.LoadAssetAtPath<Items>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null && item.itemName == itemName) return item;
            }
            return null;
        }

        // ── 씬 딕셔너리 등록 ──────────────────────────────────────
        private static void RegisterInScene(
            Dictionary<string, ToolMaterial> materials,
            Dictionary<string, ToolPartKind> kinds,
            List<ToolPartItem> parts,
            List<ToolItem> toolItems,
            ToolSpriteLibrary library,
            List<Recipe> recipes)
        {
            Report.AppendLine();
            Report.AppendLine("## 딕셔너리 등록");
            Report.AppendLine();

            ItemDictionary itemDictionary = Object.FindFirstObjectByType<ItemDictionary>(FindObjectsInactive.Include);
            if (itemDictionary == null)
            {
                Report.AppendLine("- ⚠ 열려 있는 씬에 ItemDictionary 가 없어 등록을 건너뛰었습니다. MapTest 씬을 연 뒤 다시 실행하세요.");
                Debug.LogWarning("[ToolAssetGenerator] 씬에 ItemDictionary 가 없어 딕셔너리 등록을 건너뛰었습니다.");
                return;
            }

            List<Items> newItems = new List<Items>();
            foreach (ToolPartItem part in parts) newItems.Add(part);
            foreach (ToolItem tool in toolItems) newItems.Add(tool);
            int addedItems = AppendToList(itemDictionary, "itemsList", newItems);
            Report.AppendLine($"- ItemDictionary 에 아이템 {addedItems}개 추가(부품 {parts.Count} + 도구 {toolItems.Count} 중 새것)");

            RecipeDictionary recipeDictionary = Object.FindFirstObjectByType<RecipeDictionary>(FindObjectsInactive.Include);
            if (recipeDictionary != null)
            {
                int addedRecipes = AppendToList(recipeDictionary, "recipesList", new List<Recipe>(recipes));
                Report.AppendLine($"- RecipeDictionary 에 도구 레시피 {addedRecipes}개 추가");
            }
            else
            {
                Report.AppendLine("- ⚠ 씬에 RecipeDictionary 가 없어 도구 레시피를 등록하지 못했습니다.");
            }

            ToolDictionary toolDictionary = Object.FindFirstObjectByType<ToolDictionary>(FindObjectsInactive.Include);
            if (toolDictionary == null)
            {
                GameObject host = new GameObject("ToolDictionary");
                host.transform.SetParent(itemDictionary.transform.parent, false);
                toolDictionary = host.AddComponent<ToolDictionary>();
                Report.AppendLine("- ToolDictionary 게임오브젝트를 새로 만들었습니다.");
            }

            List<ToolMaterial> materialList = new List<ToolMaterial>();
            foreach (MaterialSpec spec in Materials) materialList.Add(materials[spec.id]);
            List<ToolPartKind> kindList = new List<ToolPartKind>();
            for (int i = 0; i < Kinds.GetLength(0); i++) kindList.Add(kinds[Kinds[i, 0]]);

            SerializedObject so = new SerializedObject(toolDictionary);
            SetList(so, "materials", materialList.ConvertAll(m => (Object)m));
            SetList(so, "partKinds", kindList.ConvertAll(k => (Object)k));
            SetList(so, "parts", parts.ConvertAll(p => (Object)p));
            SetList(so, "tools", toolItems.ConvertAll(t => (Object)t));
            so.FindProperty("spriteLibrary").objectReferenceValue = library;
            so.ApplyModifiedPropertiesWithoutUndo();
            Report.AppendLine($"- ToolDictionary 채움 (재질 {materialList.Count} · 종류 {kindList.Count} · 부품 {parts.Count} · 도구 {toolItems.Count})");

            EditorSceneManager.MarkSceneDirty(itemDictionary.gameObject.scene);
            EditorSceneManager.SaveScene(itemDictionary.gameObject.scene);
            Report.AppendLine($"- 씬 저장: `{itemDictionary.gameObject.scene.path}`");
        }

        /// <summary>비공개 리스트 필드에 아직 없는 항목만 덧붙인다. 추가한 개수를 반환.</summary>
        private static int AppendToList<T>(Object target, string fieldName, List<T> values) where T : Object
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty list = so.FindProperty(fieldName);
            if (list == null)
            {
                Debug.LogError($"[ToolAssetGenerator] '{target.name}' 에 '{fieldName}' 필드가 없습니다.");
                return 0;
            }

            HashSet<Object> existing = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++)
                existing.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);

            int added = 0;
            foreach (T value in values)
            {
                if (value == null || existing.Contains(value)) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = value;
                existing.Add(value);
                added++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return added;
        }

        private static void SetList(SerializedObject so, string fieldName, List<Object> values)
        {
            SerializedProperty list = so.FindProperty(fieldName);
            if (list == null)
            {
                Debug.LogError($"[ToolAssetGenerator] '{fieldName}' 필드를 찾지 못했습니다.");
                return;
            }

            list.ClearArray();
            for (int i = 0; i < values.Count; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
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
