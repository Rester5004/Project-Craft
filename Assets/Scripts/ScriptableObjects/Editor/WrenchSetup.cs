using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 렌치 아이템과 그 제작 레시피를 만든다.
///
/// <b>재실행 안전</b>하다. 이미 있는 에셋은 값만 맞춰 주므로 몇 번을 돌려도 되고,
/// 손으로 다듬은 아이콘은 덮어쓰지 않는다.
/// 다른 팩토리와 같은 규약을 따른다 — 대화상자를 띄우지 않고, 끝에 딕셔너리 등록을 이어서 돌린다.
/// </summary>
public static class WrenchSetup
{
    private const string ItemPath = "Assets/Prefabs/Items/Tools/wrench.asset";
    private const string RecipePath = "Assets/Prefabs/Recipes/Craft_Wrench.asset";
    private const string CategoryPath = "Assets/Prefabs/Recipes/Category/tool.asset";
    private const string CoreCrafterPath = "Assets/Prefabs/Blocks/Machines/CoreCrafter.asset";
    private const string PlaceholderIconPath = "Assets/Asset/assetPlaceHolder.png";

    /// <summary>제작 재료. 렌치 그림도 수치도 아직 정해진 게 없어 <b>임의로 고른 출발점</b>이다.</summary>
    private const string MaterialItemName = "iron_rod";
    private const int MaterialCount = 2;

    [MenuItem("Tools/Project Craft/Tool/렌치 에셋 설정")]
    public static void SetupAll()
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("# 렌치 에셋 설정");
        report.AppendLine();

        WrenchItem wrench = EnsureItem(report);
        EnsureRecipe(wrench, report);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.AppendLine();
        report.AppendLine($"⚠ 재료({MaterialItemName} ×{MaterialCount})와 아이콘은 임의로 정한 출발점입니다. 그림이 생기면 바꾸세요.");
        report.AppendLine("딕셔너리 등록을 이어서 돌립니다 (Register All Assets).");
        Debug.Log(report.ToString());

        EditorApplication.ExecuteMenuItem("Tools/Project Craft/Dictionary/Register All Assets");
    }

    private static WrenchItem EnsureItem(StringBuilder report)
    {
        WrenchItem item = AssetDatabase.LoadAssetAtPath<WrenchItem>(ItemPath);
        bool isNew = item == null;
        if (isNew) item = ScriptableObject.CreateInstance<WrenchItem>();

        item.itemName = "wrench";       // 세이브 키. 영어 규약(한글은 displayName 쪽)
        item.displayName = "렌치";
        item.maxStack = 1;
        item.placeable = false;         // 배치물이 아니므로 blockId 규약과 무관하다
        item.burnEnergy = 0f;

        // 아이콘은 없을 때만 채운다. 나중에 진짜 그림을 꽂아 두면 재실행해도 지워지지 않는다.
        if (item.Icon == null)
            item.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderIconPath);
        if (item.Icon == null)
            report.AppendLine($"- ⚠ 플레이스홀더 아이콘 `{PlaceholderIconPath}` 을 찾지 못했습니다.");

        if (isNew) AssetDatabase.CreateAsset(item, ItemPath);
        else EditorUtility.SetDirty(item);

        report.AppendLine($"- 아이템 `{ItemPath}` {(isNew ? "생성" : "갱신")} (itemName=wrench, 표시명=렌치)");
        return item;
    }

    private static void EnsureRecipe(WrenchItem wrench, StringBuilder report)
    {
        Recipe recipe = AssetDatabase.LoadAssetAtPath<Recipe>(RecipePath);
        bool isNew = recipe == null;
        if (isNew) recipe = ScriptableObject.CreateInstance<Recipe>();

        MachineBlock crafter = AssetDatabase.LoadAssetAtPath<MachineBlock>(CoreCrafterPath);
        if (crafter == null) report.AppendLine($"- ⚠ 조합대 `{CoreCrafterPath}` 를 찾지 못했습니다.");

        Items material = FindItem(MaterialItemName);
        if (material == null) report.AppendLine($"- ⚠ 재료 `{MaterialItemName}` 를 찾지 못해 빈 레시피가 됩니다.");

        recipe.machine = crafter;
        recipe.category = AssetDatabase.LoadAssetAtPath<RecipeCategory>(CategoryPath);
        recipe.tier = 1;
        recipe.craftTime = 0f;
        recipe.inputs = new List<ItemStack>();
        if (material != null)
            recipe.inputs.Add(new ItemStack { item = material, count = MaterialCount });
        recipe.outputs = new List<ItemStack> { new ItemStack { item = wrench, count = 1 } };

        if (isNew) AssetDatabase.CreateAsset(recipe, RecipePath);
        else EditorUtility.SetDirty(recipe);

        report.AppendLine($"- 레시피 `{RecipePath}` {(isNew ? "생성" : "갱신")} (1티어 · 조합대 · 도구 탭)");
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
