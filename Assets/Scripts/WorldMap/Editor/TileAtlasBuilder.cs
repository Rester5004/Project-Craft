using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 슬라이스된 시트 PNG 에서 <see cref="TileAtlas.sprites"/> 를 채워 주는 에디터 도구.
/// 시트 한 장이 55~63장이라 손으로 끌어다 넣을 물건이 아니다.
///
/// 시트를 다시 슬라이스했다면 이 메뉴를 한 번 더 돌리면 된다(에셋을 새로 만들지 않고 덮어쓴다).
/// </summary>
public static class TileAtlasBuilder
{
    private const string AtlasFolder = "Assets/Asset/Tiles/Atlas";

    [MenuItem("Tools/Tiles/Build Tile Atlas")]
    public static void BuildAll()
    {
        int built = 0;
        built += Build("Assets/Asset/Tiles/Tilesets/stage1_wall.png", "stage1_wall_atlas") ? 1 : 0;
        built += Build("Assets/Asset/Tiles/Tilesets/stage2_wall_tilemap.png", "stage2_wall_atlas") ? 1 : 0;
        built += Build("Assets/Asset/Tiles/Tilesets/wall_tilemap_outline.png", "wall_outline_atlas") ? 1 : 0;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TileAtlasBuilder] 아틀라스 {built}개를 갱신했습니다.");
    }

    /// <summary>시트 경로의 서브 스프라이트를 모두 모아 아틀라스 에셋에 넣는다. 없으면 새로 만든다.</summary>
    public static bool Build(string sheetPath, string atlasName)
    {
        List<Sprite> sprites = new List<Sprite>();
        foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(sheetPath))
            if (asset is Sprite sprite) sprites.Add(sprite);

        // Single 모드 시트는 서브 에셋이 없고 메인 에셋 자체가 스프라이트다.
        if (sprites.Count == 0)
        {
            Sprite main = AssetDatabase.LoadAssetAtPath<Sprite>(sheetPath);
            if (main != null) sprites.Add(main);
        }

        if (sprites.Count == 0)
        {
            Debug.LogError($"[TileAtlasBuilder] '{sheetPath}' 에서 스프라이트를 찾지 못했습니다(경로 오타이거나 슬라이스 전).");
            return false;
        }

        if (!AssetDatabase.IsValidFolder(AtlasFolder))
            System.IO.Directory.CreateDirectory(AtlasFolder);

        string atlasPath = $"{AtlasFolder}/{atlasName}.asset";
        TileAtlas atlas = AssetDatabase.LoadAssetAtPath<TileAtlas>(atlasPath);
        bool isNew = atlas == null;
        if (isNew) atlas = ScriptableObject.CreateInstance<TileAtlas>();

        atlas.sprites = sprites.ToArray();

        if (isNew) AssetDatabase.CreateAsset(atlas, atlasPath);
        else EditorUtility.SetDirty(atlas);

        Debug.Log($"[TileAtlasBuilder] {atlasName} ← {sheetPath} (스프라이트 {sprites.Count}장)");
        return true;
    }
}
