using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// <c>pipes.png</c> 를 32x32 균일 격자(5열 x 9행 = 45칸)로 다시 자른다.
///
/// 원래 시트는 자동 바운딩박스로 잘려 있어 조각 크기가 제각각(33x10, 74x74 …)이었다.
/// 그러면 격자 좌표를 계산할 수 없고, 무엇보다 <b>조각이 셀 가장자리에 닿지 않아 이음매에 빈 틈이 생긴다.</b>
/// 원본 아트는 처음부터 32x32 격자 위에 그려져 있으므로 균일하게 다시 자르기만 하면 된다.
///
/// 이름이 같은 조각은 기존 spriteID 를 물려주므로 <b>몇 번을 다시 돌려도 참조가 끊기지 않는다.</b>
/// </summary>
public static class PipeSpriteSlicer
{
    public const string SheetPath = "Assets/Asset/BlockImages/pipes.png";

    private const int Cell = 32;
    private const int Cols = 5;
    private const int BandRows = 3;
    private const int Bands = 3;

    /// <summary>밴드 순서(위에서 아래로). 픽셀 평균색으로 확인한 값이다.</summary>
    public static readonly string[] BandNames = { "item", "gas", "liquid" };

    /// <summary>
    /// 밴드 안의 [행, 열] 이 담고 있는 연결 마스크(N=1, E=2, S=4, W=8).
    /// -1 은 아직 그림이 없는 예비 칸이다.
    /// </summary>
    public static readonly int[,] MaskAt =
    {
        { 10,  5,  6, 14, 12 },
        {  8,  2,  7, 15, 13 },
        { -1, -1,  3, 11,  9 },
    };

    /// <summary>예비 칸의 이름(마스크가 없는 칸).</summary>
    public static string SpareName(string band, int col) => $"pipe_{band}_spare{col}";

    /// <summary>마스크 조각의 이름.</summary>
    public static string MaskName(string band, int mask) => $"pipe_{band}_{mask}";

    [MenuItem("Tools/Tiles/Slice Pipe Sheet")]
    public static void Slice()
    {
        TextureImporter importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[PipeSpriteSlicer] '{SheetPath}' 를 찾을 수 없습니다.");
            return;
        }

        ApplyImportSettings(importer);

        // 데이터 프로바이더는 spriteMode 가 Multiple 일 때만 rect 목록을 다룬다.
        importer.spriteImportMode = SpriteImportMode.Multiple;

        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        // 이름이 같은 조각은 기존 ID 를 물려준다 — 아틀라스가 이미 참조 중이어도 끊기지 않는다.
        Dictionary<string, GUID> previousIds = new Dictionary<string, GUID>();
        foreach (SpriteRect old in provider.GetSpriteRects())
            previousIds[old.name] = old.spriteID;

        List<SpriteRect> rects = BuildRects(previousIds);
        provider.SetSpriteRects(rects.ToArray());   // 전량 교체 — 예전 pipes_0..14 는 함께 사라진다
        provider.Apply();
        importer.SaveAndReimport();

        Debug.Log($"[PipeSpriteSlicer] '{SheetPath}' 를 {Cell}x{Cell} 격자 {rects.Count}칸으로 다시 잘랐습니다"
            + $" (물려받은 ID {CountReused(rects, previousIds)}개).");
    }

    private static void ApplyImportSettings(TextureImporter importer)
    {
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        settings.textureType = TextureImporterType.Sprite;
        settings.spriteMode = (int)SpriteImportMode.Multiple;
        settings.spritePixelsPerUnit = Cell;                 // 32px = 1 월드 유닛 = 타일맵 한 칸
        settings.spriteMeshType = SpriteMeshType.FullRect;   // 타일은 항상 사각형. Tight 는 회전에서 손해만 본다
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.filterMode = FilterMode.Point;
        settings.alphaIsTransparency = true;
        settings.mipmapEnabled = false;
        settings.readable = false;                           // 런타임에 픽셀을 읽지 않는다
        settings.wrapMode = TextureWrapMode.Clamp;

        importer.SetTextureSettings(settings);
    }

    private static List<SpriteRect> BuildRects(Dictionary<string, GUID> previousIds)
    {
        List<SpriteRect> rects = new List<SpriteRect>();

        for (int band = 0; band < Bands; band++)
        {
            string bandName = BandNames[band];
            for (int row = 0; row < BandRows; row++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    int mask = MaskAt[row, col];
                    string name = mask >= 0 ? MaskName(bandName, mask) : SpareName(bandName, col);

                    // 시트는 위에서 아래로 읽지만 Unity 의 rect 는 아래가 0 이다.
                    int visualRow = band * BandRows + row;
                    int yFromBottom = (Bands * BandRows - 1) - visualRow;

                    rects.Add(new SpriteRect
                    {
                        name = name,
                        spriteID = previousIds.TryGetValue(name, out GUID id) ? id : GUID.Generate(),
                        rect = new Rect(col * Cell, yFromBottom * Cell, Cell, Cell),
                        pivot = new Vector2(0.5f, 0.5f),
                        alignment = SpriteAlignment.Center,
                        border = Vector4.zero,
                    });
                }
            }
        }

        return rects;
    }

    private static int CountReused(List<SpriteRect> rects, Dictionary<string, GUID> previousIds)
    {
        int reused = 0;
        foreach (SpriteRect rect in rects)
            if (previousIds.ContainsKey(rect.name)) reused++;
        return reused;
    }
}
