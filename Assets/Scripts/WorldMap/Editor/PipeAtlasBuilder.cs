using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 잘라 둔 <c>pipes.png</c> 조각을 종류별 <see cref="PipeAtlas"/> 로 묶는다.
///
/// 반드시 Slice Pipe Sheet 를 먼저 돌려야 한다 — 조각이 32x32 균일 격자가 아니면 이름 규칙이 맞지 않는다.
/// </summary>
public static class PipeAtlasBuilder
{
    private const string AtlasFolder = "Assets/Asset/Tiles/Atlas";

    /// <summary>시트에 없는 칸을 채우는 규칙: (마스크, 대신 쓸 마스크, 회전 각도).</summary>
    private static readonly (int mask, int source, int rotation)[] Derived =
    {
        // E 캡은 셀 중심 기준으로 픽셀 단위 대칭이라 90도 회전이 정확히 N만/S만이 된다.
        (1, 2,  90),   // N만  ← E 캡을 위로 돌린다
        (4, 2, -90),   // S만  ← E 캡을 아래로 돌린다
    };

    [MenuItem("Tools/Tiles/Build Pipe Atlas")]
    public static void BuildAll()
    {
        Dictionary<string, Sprite> byName = new Dictionary<string, Sprite>();
        foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(PipeSpriteSlicer.SheetPath))
            if (asset is Sprite sprite) byName[sprite.name] = sprite;

        StringBuilder report = new StringBuilder();
        report.AppendLine("# 파이프 아틀라스 빌드");
        report.AppendLine();
        report.AppendLine($"시트 조각 {byName.Count}장 (기대값 45)");

        if (byName.Count == 0)
        {
            Debug.LogError("[PipeAtlasBuilder] 시트에서 조각을 찾지 못했습니다. Tools/Tiles/Slice Pipe Sheet 를 먼저 돌리세요.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(AtlasFolder))
            System.IO.Directory.CreateDirectory(AtlasFolder);

        foreach (string band in PipeSpriteSlicer.BandNames)
            BuildOne(band, byName, report);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(report.ToString());
    }

    private static void BuildOne(string band, Dictionary<string, Sprite> byName, StringBuilder report)
    {
        string path = $"{AtlasFolder}/pipe_{band}_atlas.asset";
        PipeAtlas atlas = AssetDatabase.LoadAssetAtPath<PipeAtlas>(path);
        bool isNew = atlas == null;
        if (isNew) atlas = ScriptableObject.CreateInstance<PipeAtlas>();

        atlas.sprites = new Sprite[16];
        atlas.rotations = new int[16];

        // 1) 시트에 그대로 있는 칸
        int found = 0;
        for (int mask = 0; mask < 16; mask++)
        {
            if (!byName.TryGetValue(PipeSpriteSlicer.MaskName(band, mask), out Sprite sprite)) continue;
            atlas.sprites[mask] = sprite;
            found++;
        }

        // 2) 있는 그림을 돌려 채우는 칸
        List<string> derivedNotes = new List<string>();
        foreach ((int mask, int source, int rotation) in Derived)
        {
            if (atlas.sprites[mask] != null) continue;   // 나중에 전용 그림이 생기면 그쪽이 이긴다
            if (atlas.sprites[source] == null) continue;

            atlas.sprites[mask] = atlas.sprites[source];
            atlas.rotations[mask] = rotation;
            derivedNotes.Add($"{mask}←{source}({rotation}도)");
        }

        // 3) 연결 없음(0). 예비 칸에 그림이 있으면 그것을, 없으면 가로 직선으로 대체한다.
        string spareNote = "";
        if (atlas.sprites[0] == null)
        {
            Sprite spare = FindNonEmptySpare(band, byName);
            if (spare != null)
            {
                atlas.sprites[0] = spare;
                spareNote = " (예비 칸 그림 사용)";
            }
            else if (atlas.sprites[10] != null)
            {
                atlas.sprites[0] = atlas.sprites[10];
                spareNote = " (전용 그림이 없어 가로 직선으로 대체 — 시트 3행 1·2열에 그린 뒤 다시 돌리세요)";
            }
        }

        if (isNew) AssetDatabase.CreateAsset(atlas, path);
        else EditorUtility.SetDirty(atlas);

        int empty = 0;
        for (int i = 0; i < 16; i++) if (atlas.sprites[i] == null) empty++;

        report.AppendLine();
        report.AppendLine($"## {band}");
        report.AppendLine($"- `{path}` — 시트에서 {found}칸, 회전으로 {derivedNotes.Count}칸"
            + (derivedNotes.Count > 0 ? $" [{string.Join(", ", derivedNotes)}]" : ""));
        report.AppendLine($"- 마스크 0(고립): {(atlas.sprites[0] != null ? "채움" + spareNote : "**비어 있음**")}");
        if (empty > 0) report.AppendLine($"- ⚠ 아직 비어 있는 칸 {empty}개");
    }

    /// <summary>
    /// 예비 칸(3행 1·2열)에 실제로 그림이 들어왔는지 본다.
    /// 스프라이트의 texture 는 읽을 수 없게 임포트되므로 시트 PNG 를 따로 디코딩해 확인한다.
    /// </summary>
    private static Sprite FindNonEmptySpare(string band, Dictionary<string, Sprite> byName)
    {
        Texture2D probe = null;
        try
        {
            byte[] bytes = System.IO.File.ReadAllBytes(PipeSpriteSlicer.SheetPath);
            probe = new Texture2D(2, 2);
            if (!probe.LoadImage(bytes)) return null;

            for (int col = 0; col <= 1; col++)
            {
                if (!byName.TryGetValue(PipeSpriteSlicer.SpareName(band, col), out Sprite spare)) continue;

                Rect r = spare.rect;
                Color[] pixels = probe.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
                foreach (Color pixel in pixels)
                    if (pixel.a > 0.03f) return spare;
            }
            return null;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PipeAtlasBuilder] 예비 칸을 확인하지 못했습니다: {e.Message}");
            return null;
        }
        finally
        {
            if (probe != null) Object.DestroyImmediate(probe);
        }
    }
}
