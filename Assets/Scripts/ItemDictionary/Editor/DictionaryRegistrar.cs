using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectCraft.EditorTools
{
    /// <summary>
    /// 프로젝트의 아이템 · 블록 · 레시피 에셋을 씬의 딕셔너리에 일괄 등록한다.
    /// 등록되지 않은 에셋은 런타임에 존재하지 않는 것과 같으므로(세이브 로드 실패 · 레시피 미작동),
    /// 에셋을 추가한 뒤 이 메뉴를 한 번 돌리면 된다. 재실행 가능(이미 있는 항목은 건너뛴다).
    ///
    /// 레시피는 <b>골라서</b> 등록한다. 미완성 임포트본을 그대로 넣으면 기계가 엉뚱한 레시피를 잡기 때문이다.
    /// </summary>
    public static class DictionaryRegistrar
    {
        private const string ReportPath = "Assets/Prefabs/Recipes/_RegisterReport.md";

        private static readonly StringBuilder Report = new StringBuilder();

        [MenuItem("Tools/Project Craft/Dictionary/Register All Assets")]
        public static void RegisterAll()
        {
            ItemDictionary itemDictionary = Object.FindFirstObjectByType<ItemDictionary>(FindObjectsInactive.Include);
            RecipeDictionary recipeDictionary = Object.FindFirstObjectByType<RecipeDictionary>(FindObjectsInactive.Include);

            if (itemDictionary == null || recipeDictionary == null)
            {
                Debug.LogError("[DictionaryRegistrar] 열려 있는 씬에 ItemDictionary / RecipeDictionary 가 없습니다. MapTest 씬을 여세요.");
                return;
            }

            Report.Clear();
            Report.AppendLine("# 딕셔너리 등록 보고서");
            Report.AppendLine();
            Report.AppendLine("`Tools/Project Craft/Dictionary/Register All Assets` 가 자동 생성한 파일입니다.");
            Report.AppendLine();

            RegisterItems(itemDictionary);
            RegisterBlocks(itemDictionary);
            RegisterFluids(itemDictionary);
            RegisterRecipes(recipeDictionary);

            EditorSceneManager.MarkSceneDirty(itemDictionary.gameObject.scene);
            EditorSceneManager.SaveScene(itemDictionary.gameObject.scene);
            Report.AppendLine();
            Report.AppendLine($"씬 저장: `{itemDictionary.gameObject.scene.path}`");

            File.WriteAllText(ReportPath, Report.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ReportPath);
            Debug.Log("[DictionaryRegistrar] 완료. 자세한 내역은 " + ReportPath);
        }

        // ── 아이템 ────────────────────────────────────────────────
        private static void RegisterItems(ItemDictionary dictionary)
        {
            List<Items> all = LoadAll<Items>();
            List<Items> valid = new List<Items>();

            Dictionary<string, Items> byId = new Dictionary<string, Items>();
            Dictionary<string, Items> byDisplay = new Dictionary<string, Items>();
            List<string> problems = new List<string>();

            foreach (Items item in all)
            {
                if (string.IsNullOrEmpty(item.itemName))
                {
                    problems.Add($"itemName 이 비어 있음: `{AssetDatabase.GetAssetPath(item)}`");
                    continue;
                }
                if (byId.TryGetValue(item.itemName, out Items other))
                {
                    problems.Add($"itemName '{item.itemName}' 중복: `{AssetDatabase.GetAssetPath(other)}` / `{AssetDatabase.GetAssetPath(item)}`");
                    continue;
                }
                byId[item.itemName] = item;

                // 한글 이름이 겹치면 /give 나 검색에서 한쪽만 잡히므로 미리 알려 준다(등록은 한다).
                string display = ItemDictionary.NormalizeName(item.DisplayName);
                if (byDisplay.TryGetValue(display, out Items sameName))
                    problems.Add($"표시 이름 '{display}' 중복: '{sameName.itemName}' / '{item.itemName}' (이름 검색 시 앞의 것만 잡힘)");
                else byDisplay[display] = item;

                valid.Add(item);
            }

            int added = Append(dictionary, "itemsList", valid);
            Report.AppendLine($"## 아이템");
            Report.AppendLine();
            Report.AppendLine($"- 에셋 {all.Count}개 중 {valid.Count}개 등록 대상, 새로 추가 {added}개");
            RemoveMissing(dictionary, "itemsList");
            AppendProblems(problems);
        }

        // ── 블록 ──────────────────────────────────────────────────
        private static void RegisterBlocks(ItemDictionary dictionary)
        {
            List<BlockBase> all = LoadAll<BlockBase>();
            List<BlockBase> valid = new List<BlockBase>();
            List<string> problems = new List<string>();
            HashSet<string> seen = new HashSet<string>();

            foreach (BlockBase block in all)
            {
                if (string.IsNullOrEmpty(block.blockName))
                {
                    problems.Add($"blockName 이 비어 있음: `{AssetDatabase.GetAssetPath(block)}`");
                    continue;
                }
                if (!seen.Add(block.blockName))
                {
                    problems.Add($"blockName '{block.blockName}' 중복: `{AssetDatabase.GetAssetPath(block)}`");
                    continue;
                }
                valid.Add(block);
            }

            int added = Append(dictionary, "blocksList", valid);
            Report.AppendLine();
            Report.AppendLine($"## 블록");
            Report.AppendLine();
            Report.AppendLine($"- 에셋 {all.Count}개 중 {valid.Count}개 등록 대상, 새로 추가 {added}개");
            RemoveMissing(dictionary, "blocksList");
            AppendProblems(problems);
        }

        // ── 유체 ──────────────────────────────────────────────────
        private static void RegisterFluids(ItemDictionary dictionary)
        {
            List<FluidDefine> all = LoadAll<FluidDefine>();
            List<FluidDefine> valid = new List<FluidDefine>();
            List<string> problems = new List<string>();
            HashSet<string> seen = new HashSet<string>();

            foreach (FluidDefine fluid in all)
            {
                string path = AssetDatabase.GetAssetPath(fluid);

                if (string.IsNullOrEmpty(fluid.fluidId))
                {
                    problems.Add($"fluidId 가 비어 있음: `{path}`");
                    continue;
                }
                if (!seen.Add(fluid.fluidId))
                {
                    problems.Add($"fluidId '{fluid.fluidId}' 중복: `{path}`");
                    continue;
                }
                // 한쪽만 있으면 양동이 교환이 반쪽이 된다(채우기만 되고 비우기가 안 되는 식).
                if ((fluid.bucketItem == null) != (fluid.emptyItem == null))
                    problems.Add($"'{fluid.fluidId}' 의 bucketItem/emptyItem 중 하나만 지정됨 — 양동이 교환이 동작하지 않습니다: `{path}`");

                valid.Add(fluid);
            }

            int added = Append(dictionary, "fluidsList", valid);
            Report.AppendLine();
            Report.AppendLine($"## 유체");
            Report.AppendLine();
            Report.AppendLine($"- 에셋 {all.Count}개 중 {valid.Count}개 등록 대상, 새로 추가 {added}개");
            RemoveMissing(dictionary, "fluidsList");
            AppendProblems(problems);
        }

        // ── 레시피 ────────────────────────────────────────────────
        /// <summary>
        /// 등록 조건:
        /// ① 기계가 지정돼 있을 것(없으면 어느 기계도 이 레시피를 못 찾는다)
        /// ② 산출물이 하나라도 있을 것(재료만 있고 결과가 없으면 재료를 먹고 아무것도 안 준다)
        /// </summary>
        private static void RegisterRecipes(RecipeDictionary dictionary)
        {
            List<Recipe> all = LoadAll<Recipe>();
            List<Recipe> valid = new List<Recipe>();

            int noMachine = 0, noOutput = 0;
            List<string> dangerous = new List<string>();

            foreach (Recipe recipe in all)
            {
                string path = AssetDatabase.GetAssetPath(recipe);

                if (recipe.machine == null) { noMachine++; continue; }

                if (!HasAnyOutput(recipe))
                {
                    noOutput++;
                    // 재료는 있는데 결과가 없는 레시피는 등록하면 재료만 사라진다. 반드시 짚어 준다.
                    if (HasAnyInput(recipe)) dangerous.Add($"`{path}` ({recipe.MachineBlockId})");
                    continue;
                }

                valid.Add(recipe);
            }

            // 같은 기계에서 같은 산출물을 내는 레시피가 둘이면 앞의 것만 계속 쓰인다.
            Dictionary<string, Recipe> firstByKey = new Dictionary<string, Recipe>();
            List<string> shadowed = new List<string>();
            foreach (Recipe recipe in valid)
            {
                // 유체만 내는 레시피는 PrimaryOutput 이 null 이다(전기 분해기의 물 → 수소·산소).
                // 그때는 첫 산출 유체를 대표로 쓴다 — 여기서 그냥 .itemName 을 부르면 NRE 로 등록이 통째로 멈춘다.
                string outKey = recipe.PrimaryOutput != null ? recipe.PrimaryOutput.itemName : FirstFluidId(recipe);
                string key = recipe.MachineBlockId + "|" + outKey;
                if (firstByKey.TryGetValue(key, out Recipe first))
                    shadowed.Add($"`{AssetDatabase.GetAssetPath(recipe)}` ← `{AssetDatabase.GetAssetPath(first)}` 가 먼저 잡힘");
                else firstByKey[key] = recipe;
            }

            int added = Append(dictionary, "recipesList", valid);
            RemoveMissing(dictionary, "recipesList");

            Report.AppendLine();
            Report.AppendLine("## 레시피");
            Report.AppendLine();
            Report.AppendLine($"- 에셋 {all.Count}개 중 **{valid.Count}개 등록 대상**, 새로 추가 {added}개");
            Report.AppendLine($"- 제외: 기계 미지정 {noMachine}개 · 산출물 없음 {noOutput}개");

            if (dangerous.Count > 0)
            {
                Report.AppendLine();
                Report.AppendLine($"### ⚠ 재료만 있고 산출물이 없어 제외한 레시피 {dangerous.Count}개");
                Report.AppendLine();
                Report.AppendLine("등록했다면 재료만 먹고 아무것도 만들지 않았을 것들입니다. 산출물을 채운 뒤 다시 실행하세요.");
                Report.AppendLine();
                foreach (string line in dangerous) Report.AppendLine($"- {line}");
            }

            if (shadowed.Count > 0)
            {
                Report.AppendLine();
                Report.AppendLine($"### ⚠ 같은 기계 · 같은 산출물이라 가려지는 레시피 {shadowed.Count}개");
                Report.AppendLine();
                foreach (string line in shadowed) Report.AppendLine($"- {line}");
            }

            // 기계별 요약
            Dictionary<string, int> byMachine = new Dictionary<string, int>();
            foreach (Recipe recipe in valid)
            {
                string key = recipe.MachineBlockId;
                byMachine.TryGetValue(key, out int count);
                byMachine[key] = count + 1;
            }
            List<string> keys = new List<string>(byMachine.Keys);
            keys.Sort(System.StringComparer.Ordinal);

            Report.AppendLine();
            Report.AppendLine("### 기계별 등록 수");
            Report.AppendLine();
            foreach (string key in keys) Report.AppendLine($"- `{key}` : {byMachine[key]}개");
        }

        /// <summary>
        /// 낼 것이 하나라도 있는가. <b>확률 부산물만 있는 레시피도 산출이 있는 것</b>이다
        /// (추출기가 그렇다) — 여기서 빠뜨리면 딕셔너리에 등록되지 않아 기계가 영원히 놀게 된다.
        /// </summary>
        private static bool HasAnyOutput(Recipe recipe)
        {
            if (recipe.outputs != null)
                foreach (ItemStack stack in recipe.outputs)
                    if (stack != null && stack.item != null && stack.count > 0) return true;

            if (recipe.chanceOutputs != null)
                foreach (ChanceOutput roll in recipe.chanceOutputs)
                    if (roll != null && roll.item != null && roll.count > 0 && roll.chance > 0f) return true;

            // 유체만 내는 레시피도 산출이 있는 것이다. 빠뜨리면 전기 분해기·마나 용해기가
            // "재료만 먹는 위험한 레시피" 로 걸러져 딕셔너리에 등록되지 않고 영원히 논다.
            if (recipe.fluidOutputs != null)
                foreach (FluidStack fluid in recipe.fluidOutputs)
                    if (fluid != null && fluid.fluid != null && fluid.amount > 0) return true;

            return false;
        }

        private static bool HasAnyInput(Recipe recipe)
        {
            if (recipe.inputs != null)
                foreach (ItemStack stack in recipe.inputs)
                    if (stack != null && stack.item != null && stack.count > 0) return true;

            if (recipe.fluidInputs != null)
                foreach (FluidStack fluid in recipe.fluidInputs)
                    if (fluid != null && fluid.fluid != null && fluid.amount > 0) return true;

            return false;
        }

        /// <summary>대표 산출 유체의 id(아이템 산출이 없는 레시피의 중복 검사 키).</summary>
        private static string FirstFluidId(Recipe recipe)
        {
            if (recipe.fluidOutputs != null)
                foreach (FluidStack fluid in recipe.fluidOutputs)
                    if (fluid != null && fluid.fluid != null && fluid.amount > 0) return "fluid:" + fluid.fluid.fluidId;
            return "";
        }

        // ── 공용 ──────────────────────────────────────────────────
        private static List<T> LoadAll<T>() where T : Object
        {
            List<T> result = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) result.Add(asset);
            }
            return result;
        }

        private static int Append<T>(Object target, string fieldName, List<T> values) where T : Object
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty list = so.FindProperty(fieldName);
            if (list == null)
            {
                Debug.LogError($"[DictionaryRegistrar] '{target.name}' 에 '{fieldName}' 필드가 없습니다.");
                return 0;
            }

            // 삭제된 에셋이 남긴 빈 칸을 먼저 걷어낸다. ItemDictionary.Awake 의 경고가
            // "Register All Assets 로 정리하세요" 라고 안내하므로 여기서 실제로 정리해야 한다.
            for (int i = list.arraySize - 1; i >= 0; i--)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == null) list.DeleteArrayElementAtIndex(i);

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

        /// <summary>삭제된 에셋 때문에 비어 버린 칸을 걷어낸다.</summary>
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
                Report.AppendLine($"- 빈 칸 {removed}개 제거");
            }
        }

        private static void AppendProblems(List<string> problems)
        {
            if (problems.Count == 0) return;

            Report.AppendLine();
            Report.AppendLine($"⚠ 확인 필요 {problems.Count}건:");
            Report.AppendLine();
            foreach (string line in problems) Report.AppendLine($"- {line}");
        }
    }
}
