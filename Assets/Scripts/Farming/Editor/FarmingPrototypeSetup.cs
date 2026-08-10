#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>농사 프로토타입용 SO를 만들고 MapTest의 ItemDictionary에 등록한다.</summary>
public static class FarmingPrototypeSetup
{
    private const string Root = "Assets/Prefabs/Farming";
    private const string ItemRoot = Root + "/Items";
    private const string BlockRoot = Root + "/Blocks";
    private const string PlaceholderPath = "Assets/Asset/assetPlaceHolder.png";
    private const string SoilTilePath = "Assets/Asset/Tiles/dirt.asset";
    private const string SoilSpritePath = "Assets/Asset/Tiles/stage_2_floor_tilemap_17.asset";

    [MenuItem("Tools/Project Craft/Setup Farming Prototype")]
    public static void SetupFromMenu() => Setup(false);

    // -executeMethod에서 호출하는 진입점.
    public static void SetupBatch() => Setup(true);

    private static void Setup(bool openMapTest)
    {
        EnsureFolder("Assets/Prefabs", "Farming");
        EnsureFolder(Root, "Items");
        EnsureFolder(Root, "Blocks");

        Sprite placeholder = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderPath);
        Tile soilTile = AssetDatabase.LoadAssetAtPath<Tile>(SoilTilePath);
        Sprite soilSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SoilSpritePath);
        if (placeholder == null || soilTile == null || soilSprite == null)
            throw new System.InvalidOperationException("농사 프로토타입의 placeholder 또는 흙 타일을 찾지 못했습니다.");

        Items soilItem = GetOrCreateItem(ItemRoot + "/farm_soil.asset", "farm_soil", "농사용 흙", placeholder, true);
        Items treeSeed = GetOrCreateItem(ItemRoot + "/prototype_tree_seed.asset", "prototype_tree_seed", "나무 씨앗 (농사)", placeholder, true);
        Items wood = GetOrCreateItem(ItemRoot + "/prototype_wood.asset", "prototype_wood", "수확한 나무", placeholder, false);

        MainBlock soil = GetOrCreate<MainBlock>(BlockRoot + "/farm_soil.asset");
        soil.blockName = "floor:farm_soil";
        soil.displayName = "농사용 흙";
        soil.dropItem = soilItem;
        soil.dropCount = 1;
        soil.assetPath = soilTile;
        soil.floorSprite = soilSprite;
        EditorUtility.SetDirty(soil);

        CropBlock tree = GetOrCreate<CropBlock>(BlockRoot + "/prototype_tree.asset");
        tree.blockName = "crop:prototype_tree";
        tree.displayName = "프로토타입 나무";
        tree.dropItem = treeSeed;
        tree.dropCount = 1;
        if (tree.growthSeconds <= 0.1f) tree.growthSeconds = 10f;
        tree.requiredSoilId = soil.blockName;
        tree.cropSprite = placeholder;
        tree.harvestItem = wood;
        tree.harvestCount = 3;
        tree.seedReturnCount = 1;
        EditorUtility.SetDirty(tree);

        if (openMapTest)
            EditorSceneManager.OpenScene("Assets/Scenes/MapTest.unity", OpenSceneMode.Single);

        ItemDictionary dictionary = Object.FindFirstObjectByType<ItemDictionary>(FindObjectsInactive.Include);
        if (dictionary == null) throw new System.InvalidOperationException("열린 씬에서 ItemDictionary를 찾지 못했습니다.");

        SerializedObject serialized = new SerializedObject(dictionary);
        AddUnique(serialized.FindProperty("itemsList"), soilItem, treeSeed, wood);
        AddUnique(serialized.FindProperty("blocksList"), soil, tree);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(dictionary);
        EditorSceneManager.MarkSceneDirty(dictionary.gameObject.scene);
        EditorSceneManager.SaveScene(dictionary.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[FarmingPrototypeSetup] 농사용 흙/나무 씨앗/나무 작물/수확물 생성 및 ItemDictionary 등록 완료.");
    }

    private static Items GetOrCreateItem(string path, string id, string display, Sprite icon, bool placeable)
    {
        Items item = GetOrCreate<Items>(path);
        item.itemName = id;
        item.displayName = display;
        item.Icon = icon;
        item.placeable = placeable;
        item.maxStack = 64;
        EditorUtility.SetDirty(item);
        return item;
    }

    private static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void AddUnique(SerializedProperty list, params Object[] assets)
    {
        var existing = new HashSet<Object>();
        for (int i = 0; i < list.arraySize; i++) existing.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
        foreach (Object asset in assets)
        {
            if (asset == null || existing.Contains(asset)) continue;
            int index = list.arraySize++;
            list.GetArrayElementAtIndex(index).objectReferenceValue = asset;
            existing.Add(asset);
        }
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
