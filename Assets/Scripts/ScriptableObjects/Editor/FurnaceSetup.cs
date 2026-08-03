using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ProjectCraft.UIFactory.EditorTools;

namespace ProjectCraft.EditorTools
{
    /// <summary>화로 한 티어의 정의.</summary>
    internal class FurnaceTierSpec
    {
        public string id;         // Machine:Furnace 뒤에 붙는 부분
        public string korean;
        public int tier;
        public bool usesEnergy;   // 0티어만 false(석탄 연소)
        public float craftSpeed;  // 참고용(지금은 레시피의 craftTime 을 그대로 쓴다)

        public FurnaceTierSpec(string id, string korean, int tier, bool usesEnergy, float craftSpeed)
        {
            this.id = id;
            this.korean = korean;
            this.tier = tier;
            this.usesEnergy = usesEnergy;
            this.craftSpeed = craftSpeed;
        }
    }

    /// <summary>
    /// 화로 3티어를 만들고, 제련 레시피를 합금 재련기에서 화로로 옮긴다.
    ///
    /// - 0티어 화로: 석탄·갈탄을 태워 무전력 가동(연료 칸 1개)
    /// - 1티어 전기로 / 2티어 고전압 전기로: 전력 사용(연료 칸 없음)
    /// 셋은 <see cref="MachineBlock.recipeGroupId"/> 를 "Furnace" 로 공유하므로 레시피 목록이 같고,
    /// <see cref="MachineBlock.tier"/> 로 어느 레시피까지 구울 수 있는지가 갈린다.
    ///
    /// 재실행해도 안전하다(이미 있는 에셋은 손대지 않는다). 대화상자를 띄우지 않는다.
    /// </summary>
    public static class FurnaceSetup
    {
        private const string RecipeGroup = "Furnace";

        private const string BlockFolder = "Assets/Prefabs/Blocks/Machines";
        private const string ItemFolder = "Assets/Prefabs/Items/Machines";
        private const string WorldPrefabFolder = "Assets/Prefabs/Blocks/Machines";
        private const string UIPrefabFolder = "Assets/Prefabs/UI/Machines";
        private const string MachineSheet = "Assets/Asset/MachineImages/machine2.png";
        private const string ReportPath = "Assets/Prefabs/Blocks/Machines/_FurnaceReport.md";

        private const string AlloySmelterPath = BlockFolder + "/AlloySmelter.asset";
        private const string SourceUIPrefab = UIPrefabFolder + "/AlloySmelter_UI.prefab";
        private const string SourceWorldPrefab = WorldPrefabFolder + "/AlloySmelter.prefab";

        private static readonly FurnaceTierSpec[] Tiers =
        {
            new FurnaceTierSpec("Furnace",           "화로",           0, false, 1f),
            new FurnaceTierSpec("ElectricFurnace",   "전기로",         1, true,  0.75f),
            new FurnaceTierSpec("HVElectricFurnace", "고전압 전기로",  2, true,  0.5f),
        };

        // 연료 에너지(Notion 화력 발전기 연료표와 같은 수치를 쓴다)
        private static readonly string[,] Fuels =
        {
            { "brown_coal", "200" },
            { "coal", "400" },
        };

        private static readonly StringBuilder Report = new StringBuilder();

        [MenuItem("Tools/Project Craft/Machines/Setup Furnace And Move Smelting")]
        public static void Run()
        {
            // <b>파괴적인 작업보다 전제 조건 검사가 먼저다.</b>
            // 예전에는 아래에서 Smelt_* 레시피를 지운 다음 등록 단계에 가서야 씬에 딕셔너리가 없다는 걸
            // 알아챘다 — 삭제는 이미 끝났는데 리스트의 빈 칸 정리(RemoveMissing)는 못 한 중간 상태로 끝났다.
            if (Object.FindFirstObjectByType<ItemDictionary>(FindObjectsInactive.Include) == null)
            {
                Debug.LogWarning("[FurnaceSetup] 열려 있는 씬에 ItemDictionary 가 없습니다. "
                    + "MapTest 씬을 연 뒤 다시 실행하세요. (아무것도 바꾸지 않았습니다)");
                return;
            }

            Report.Clear();
            Report.AppendLine("# 화로 구성 보고서");
            Report.AppendLine();
            Report.AppendLine("`Tools/Project Craft/Machines/Setup Furnace And Move Smelting` 가 자동 생성한 파일입니다.");
            Report.AppendLine();

            Dictionary<string, Sprite> sprites = LoadSprites();
            SetFuelValues();

            GameObject uiPrefab = EnsureFurnaceUI();
            List<MachineBlock> blocks = new List<MachineBlock>();
            List<Items> items = new List<Items>();

            foreach (FurnaceTierSpec spec in Tiers)
            {
                GameObject worldPrefab = EnsureWorldPrefab(spec, sprites);
                MachineBlock block = EnsureBlock(spec, worldPrefab, uiPrefab);
                blocks.Add(block);
                items.Add(EnsureItem(spec, sprites));
            }

            MoveSmeltingRecipes(blocks[0]);
            RestrictAlloySmelter();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RegisterInScene(items, blocks);

            File.WriteAllText(ReportPath, Report.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ReportPath);
            Debug.Log("[FurnaceSetup] 완료. 자세한 내역은 " + ReportPath);
        }

        // ── 연료 수치 ─────────────────────────────────────────────
        private static void SetFuelValues()
        {
            for (int i = 0; i < Fuels.GetLength(0); i++)
            {
                Items item = FindItem(Fuels[i, 0]);
                if (item == null)
                {
                    Report.AppendLine($"- ⚠ 연료 아이템 '{Fuels[i, 0]}' 을 찾지 못했습니다.");
                    continue;
                }
                float energy = float.Parse(Fuels[i, 1]);
                if (Mathf.Approximately(item.burnEnergy, energy)) continue;

                item.burnEnergy = energy;
                EditorUtility.SetDirty(item);
                Report.AppendLine($"- 연료 설정: {item.DisplayName} = {energy} Energy");
            }
        }

        // ── 스프라이트 ────────────────────────────────────────────
        private static Dictionary<string, Sprite> LoadSprites()
        {
            Dictionary<string, Sprite> result = new Dictionary<string, Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(MachineSheet))
                if (asset is Sprite sprite) result[sprite.name] = sprite;
            return result;
        }

        private static Sprite Get(Dictionary<string, Sprite> sprites, string name)
        {
            if (sprites.TryGetValue(name, out Sprite sprite)) return sprite;
            Report.AppendLine($"  - ⚠ 스프라이트 '{name}' 없음");
            return null;
        }

        // ── 월드 프리팹 ───────────────────────────────────────────
        /// <summary>기존 기계 프리팹을 복제해 스프라이트만 화로로 바꾼다(콜라이더·컴포넌트 구성을 그대로 물려받는다).</summary>
        private static GameObject EnsureWorldPrefab(FurnaceTierSpec spec, Dictionary<string, Sprite> sprites)
        {
            string path = $"{WorldPrefabFolder}/{spec.id}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            if (!AssetDatabase.CopyAsset(SourceWorldPrefab, path))
            {
                Report.AppendLine($"- ⚠ 월드 프리팹 복제 실패: {path}");
                return null;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                contents.name = spec.id;
                SpriteRenderer renderer = contents.GetComponentInChildren<SpriteRenderer>(true);
                // 전기로 전용 아트가 아직 없어 셋 다 화로 그림을 쓴다.
                if (renderer != null) renderer.sprite = Get(sprites, "furnace_off");
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            Report.AppendLine($"- 월드 프리팹 생성: `{path}`");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // ── UI 프리팹 ─────────────────────────────────────────────
        /// <summary>
        /// AlloySmelter_UI 를 복제해 연료 칸과 연료 바를 덧붙인 화로 UI 를 만든다.
        /// 세 티어가 같은 프리팹을 쓴다 — 연료 칸은 연료를 안 쓰는 티어에서 런타임에 자동으로 꺼진다.
        /// </summary>
        private static GameObject EnsureFurnaceUI()
        {
            string path = $"{UIPrefabFolder}/Furnace_UI.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && HasRole(existing, MachineUIRole.FuelSlot)) return existing;

            if (existing == null && !AssetDatabase.CopyAsset(SourceUIPrefab, path))
            {
                Report.AppendLine($"- ⚠ UI 프리팹 복제 실패: {path}");
                return null;
            }

            GameObject slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MachineUIFactoryPaths.SlotPrefab);
            GameObject barPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MachineUIFactoryPaths.EnergyBarPrefab);

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                contents.name = "Furnace_UI";
                Transform parent = FindElementParent(contents);

                if (!HasRole(contents, MachineUIRole.FuelSlot) && slotPrefab != null)
                    AddElement(parent, slotPrefab, "FuelSlot", MachineUIRole.FuelSlot, new Vector2(-300f, 10f));

                if (!HasRole(contents, MachineUIRole.FuelBar) && barPrefab != null)
                    AddElement(parent, barPrefab, "FuelBar", MachineUIRole.FuelBar, new Vector2(-420f, 0f));

                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            Report.AppendLine($"- UI 프리팹 생성: `{path}` (연료 칸 + 연료 바 추가)");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>기존 요소들이 붙어 있는 부모를 찾는다(레이아웃 루트).</summary>
        private static Transform FindElementParent(GameObject root)
        {
            MachineUIElement any = root.GetComponentInChildren<MachineUIElement>(true);
            return any != null ? any.transform.parent : root.transform;
        }

        private static bool HasRole(GameObject root, MachineUIRole role)
        {
            foreach (MachineUIElement element in root.GetComponentsInChildren<MachineUIElement>(true))
                if (element.role == role) return true;
            return false;
        }

        private static void AddElement(Transform parent, GameObject prefab, string name,
            MachineUIRole role, Vector2 position)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;

            RectTransform rect = go.transform as RectTransform;
            if (rect != null) rect.anchoredPosition = position;

            MachineUIElement element = go.GetComponent<MachineUIElement>();
            if (element == null) element = go.AddComponent<MachineUIElement>();
            element.role = role;
            element.index = 0;
        }

        // ── 블록 · 아이템 ─────────────────────────────────────────
        private static MachineBlock EnsureBlock(FurnaceTierSpec spec, GameObject worldPrefab, GameObject uiPrefab)
        {
            string path = $"{BlockFolder}/{spec.id}.asset";
            MachineBlock block = AssetDatabase.LoadAssetAtPath<MachineBlock>(path);
            if (block != null) return block;

            block = ScriptableObject.CreateInstance<MachineBlock>();
            block.blockName = "Machine:" + spec.id;
            block.displayName = spec.korean;
            block.machinePrefab = worldPrefab;
            block.uiPrefab = uiPrefab;
            block.tier = spec.tier;
            block.recipeGroupId = RecipeGroup;      // 세 티어가 같은 제련 레시피 목록을 본다
            block.inputSlotCount = 1;
            block.outputSlotCount = 1;
            block.fuelSlotCount = spec.usesEnergy ? 0 : 1;   // 0티어만 연료를 태운다
            block.fuelBurnRate = 20f;                        // 석탄 1개(400) = 20초
            block.isUseEnergy = spec.usesEnergy;
            block.maxEnergyAmount = spec.usesEnergy ? 1000f : 0f;
            AssetDatabase.CreateAsset(block, path);

            Report.AppendLine($"- 블록 생성: `{block.blockName}` ({spec.korean}, 티어 {spec.tier}, "
                + (spec.usesEnergy ? "전력" : "연료 1칸") + ")");
            return block;
        }

        private static Items EnsureItem(FurnaceTierSpec spec, Dictionary<string, Sprite> sprites)
        {
            string path = $"{ItemFolder}/{spec.id}.asset";
            Items item = AssetDatabase.LoadAssetAtPath<Items>(path);
            if (item != null) return item;

            item = ScriptableObject.CreateInstance<Items>();
            // 배치가 되려면 itemName 이 blockName 과 같아야 한다(GetGameObjectFromBlockDictionary).
            item.itemName = "Machine:" + spec.id;
            item.displayName = spec.korean;
            item.placeable = true;
            item.maxStack = 64;
            item.Icon = Get(sprites, "furnace_icon");
            AssetDatabase.CreateAsset(item, path);

            Report.AppendLine($"- 아이템 생성: `{item.itemName}` ({spec.korean})");
            return item;
        }

        // ── 레시피 재배치 ─────────────────────────────────────────
        /// <summary>
        /// Notion 제련 레시피 11개를 화로에 연결한다.
        /// 합금 재련기에 붙어 있던 광석 굽기 레시피(Smelt_*)는 이것들과 중복이라 지운다.
        /// </summary>
        private static void MoveSmeltingRecipes(MachineBlock furnace)
        {
            Report.AppendLine();
            Report.AppendLine("## 제련 레시피 이동");
            Report.AppendLine();

            int linked = 0;
            List<Recipe> smelting = new List<Recipe>();

            foreach (string guid in AssetDatabase.FindAssets("t:Recipe", new[] { "Assets/Prefabs/Recipes/Incomplete/smelting" }))
            {
                Recipe recipe = AssetDatabase.LoadAssetAtPath<Recipe>(AssetDatabase.GUIDToAssetPath(guid));
                if (recipe == null) continue;

                if (recipe.machine != furnace)
                {
                    recipe.machine = furnace;
                    EditorUtility.SetDirty(recipe);
                    linked++;
                }
                smelting.Add(recipe);
            }

            smelting.Sort((a, b) => a.tier != b.tier ? a.tier.CompareTo(b.tier) : string.CompareOrdinal(a.name, b.name));
            Report.AppendLine($"- 화로에 연결한 제련 레시피 {smelting.Count}개 (새로 연결 {linked}개)");
            foreach (Recipe recipe in smelting)
            {
                string output = recipe.PrimaryOutput != null ? recipe.PrimaryOutput.DisplayName : "(산출물 없음)";
                Report.AppendLine($"  - 티어 {recipe.tier} · {recipe.name} → {output}");
            }

            pendingSmelting = smelting;
        }

        private static List<Recipe> pendingSmelting = new List<Recipe>();

        /// <summary>합금 재련기에는 합금 레시피만 남긴다.</summary>
        private static void RestrictAlloySmelter()
        {
            Report.AppendLine();
            Report.AppendLine("## 합금 재련기 정리");
            Report.AppendLine();

            MachineBlock alloySmelter = AssetDatabase.LoadAssetAtPath<MachineBlock>(AlloySmelterPath);
            if (alloySmelter == null)
            {
                Report.AppendLine("- ⚠ AlloySmelter 블록을 찾지 못했습니다.");
                return;
            }

            List<Recipe> alloys = new List<Recipe>();
            List<string> removed = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Recipe"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Recipe recipe = AssetDatabase.LoadAssetAtPath<Recipe>(path);
                if (recipe == null || recipe.machine != alloySmelter) continue;

                // Notion 제련 레시피와 중복되는 광석 굽기 — 화로로 옮겼으므로 지운다.
                if (path.StartsWith("Assets/Prefabs/Recipes/Smelt_"))
                {
                    removed.Add(path);
                    continue;
                }
                alloys.Add(recipe);
            }

            foreach (string path in removed)
            {
                Report.AppendLine($"- 삭제(화로의 제련 레시피와 중복): `{path}`");
                AssetDatabase.DeleteAsset(path);
            }

            // 트리를 통합한 뒤로는 두 벌이 없으므로 남은 합금 레시피가 곧 등록 대상이다.
            List<Recipe> registerable = alloys;

            Report.AppendLine($"- 합금 재련기에 남은 레시피 {registerable.Count}개");
            foreach (Recipe recipe in registerable)
            {
                string output = recipe.PrimaryOutput != null ? recipe.PrimaryOutput.DisplayName : "(산출물 없음)";
                Report.AppendLine($"  - 티어 {recipe.tier} · {recipe.name} → {output}");
            }

            pendingAlloys = registerable;
        }

        private static List<Recipe> pendingAlloys = new List<Recipe>();

        // ── 씬 등록 ───────────────────────────────────────────────
        private static void RegisterInScene(List<Items> items, List<MachineBlock> blocks)
        {
            Report.AppendLine();
            Report.AppendLine("## 딕셔너리 등록");
            Report.AppendLine();

            ItemDictionary itemDictionary = Object.FindFirstObjectByType<ItemDictionary>(FindObjectsInactive.Include);
            if (itemDictionary == null)
            {
                Report.AppendLine("- ⚠ 열려 있는 씬에 ItemDictionary 가 없어 등록을 건너뛰었습니다. MapTest 씬을 연 뒤 다시 실행하세요.");
                Debug.LogWarning("[FurnaceSetup] 씬에 ItemDictionary 가 없어 딕셔너리 등록을 건너뛰었습니다.");
                return;
            }

            int addedItems = AppendToList(itemDictionary, "itemsList", items);
            int addedBlocks = AppendToList(itemDictionary, "blocksList", blocks);
            Report.AppendLine($"- ItemDictionary: 아이템 {addedItems}개 · 블록 {addedBlocks}개 추가");

            RecipeDictionary recipeDictionary = Object.FindFirstObjectByType<RecipeDictionary>(FindObjectsInactive.Include);
            if (recipeDictionary != null)
            {
                List<Recipe> toRegister = new List<Recipe>(pendingSmelting);
                toRegister.AddRange(pendingAlloys);
                int addedRecipes = AppendToList(recipeDictionary, "recipesList", toRegister);
                Report.AppendLine($"- RecipeDictionary: 레시피 {addedRecipes}개 추가 "
                    + $"(제련 {pendingSmelting.Count} + 합금 {pendingAlloys.Count} 중 새것)");

                RemoveMissing(recipeDictionary, "recipesList");
            }
            else
            {
                Report.AppendLine("- ⚠ 씬에 RecipeDictionary 가 없습니다.");
            }

            EditorSceneManager.MarkSceneDirty(itemDictionary.gameObject.scene);
            EditorSceneManager.SaveScene(itemDictionary.gameObject.scene);
            Report.AppendLine($"- 씬 저장: `{itemDictionary.gameObject.scene.path}`");
        }

        /// <summary>비공개 리스트 필드에 아직 없는 항목만 덧붙인다.</summary>
        private static int AppendToList<T>(Object target, string fieldName, List<T> values) where T : Object
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty list = so.FindProperty(fieldName);
            if (list == null)
            {
                Debug.LogError($"[FurnaceSetup] '{target.name}' 에 '{fieldName}' 필드가 없습니다.");
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

        /// <summary>삭제된 에셋 때문에 비어 버린 칸을 걷어낸다(Smelt_* 를 지운 자리).</summary>
        private static void RemoveMissing(Object target, string fieldName)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty list = so.FindProperty(fieldName);
            if (list == null) return;

            int removed = 0;
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue != null) continue;
                list.DeleteArrayElementAtIndex(i);
                removed++;
            }
            if (removed > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                Report.AppendLine($"- RecipeDictionary 의 빈 칸 {removed}개 제거");
            }
        }

        private static Items FindItem(string itemName)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Items"))
            {
                Items item = AssetDatabase.LoadAssetAtPath<Items>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null && item.itemName == itemName) return item;
            }
            return null;
        }
    }
}
