using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 파이프 아이템을 배치 가능하게 켜고, 대응하는 <see cref="PipeBlock"/> 을 만들어 값을 채운다.
///
/// <b>재실행 안전</b>하다. 이미 있는 블록은 값만 갱신하므로 몇 번을 돌려도 된다.
/// Tools/Tiles/Slice Pipe Sheet → Build Pipe Atlas 를 먼저 돌려야 아틀라스가 연결된다.
/// </summary>
public static class PipeSetup
{
    private const string BlockFolder = "Assets/Prefabs/Blocks/Pipes";
    private const string AtlasFolder = "Assets/Asset/Tiles/Atlas";

    private struct PipeSpec
    {
        public string itemName;      // Items.itemName (세이브 키). 여기서 blockName 을 복사해 온다
        public string assetName;     // 만들 에셋 파일 이름
        public PipeKind kind;
        public int tier;
        public float secondsPerCell;
        public int throughput;
        public Color tint;
    }

    private static readonly PipeSpec[] Specs =
    {
        new PipeSpec { itemName = "아이템 파이프",      assetName = "ItemPipe",   kind = PipeKind.Item,   tier = 0, secondsPerCell = 0.40f, throughput = 8,  tint = Color.white },
        new PipeSpec { itemName = "고체 운송 파이프",   assetName = "SolidPipe",  kind = PipeKind.Item,   tier = 1, secondsPerCell = 0.12f, throughput = 16, tint = new Color(1f, 0.85f, 0.55f) },
        new PipeSpec { itemName = "액체 파이프",        assetName = "LiquidPipe", kind = PipeKind.Liquid, tier = 0, secondsPerCell = 0.30f, throughput = 1,  tint = Color.white },
        new PipeSpec { itemName = "기체 파이프",        assetName = "GasPipe",    kind = PipeKind.Gas,    tier = 0, secondsPerCell = 0.30f, throughput = 1,  tint = Color.white },
    };

    /// <summary>PipeKind → 아틀라스 파일 이름 조각(시트의 밴드 이름과 같다).</summary>
    private static string AtlasNameFor(PipeKind kind)
    {
        switch (kind)
        {
            case PipeKind.Gas: return "gas";
            case PipeKind.Liquid: return "liquid";
            default: return "item";
        }
    }

    [MenuItem("Tools/Project Craft/Pipes/파이프 에셋 설정")]
    public static void SetupAll()
    {
        Dictionary<string, Items> itemsByName = new Dictionary<string, Items>();
        foreach (string guid in AssetDatabase.FindAssets("t:Items"))
        {
            Items item = AssetDatabase.LoadAssetAtPath<Items>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && !string.IsNullOrEmpty(item.itemName)) itemsByName[item.itemName] = item;
        }

        if (!AssetDatabase.IsValidFolder(BlockFolder))
            System.IO.Directory.CreateDirectory(BlockFolder);

        StringBuilder report = new StringBuilder();
        report.AppendLine("# 파이프 에셋 설정");
        report.AppendLine();

        int changed = 0;
        foreach (PipeSpec spec in Specs) changed += Apply(spec, itemsByName, report);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.AppendLine();
        report.AppendLine($"바뀐 에셋 {changed}개");
        report.AppendLine("딕셔너리 등록을 이어서 돌립니다 (Register All Assets).");
        Debug.Log(report.ToString());

        EditorApplication.ExecuteMenuItem("Tools/Project Craft/Dictionary/Register All Assets");
    }

    private static int Apply(PipeSpec spec, Dictionary<string, Items> itemsByName, StringBuilder report)
    {
        if (!itemsByName.TryGetValue(spec.itemName, out Items item))
        {
            report.AppendLine($"- ⚠ 아이템 `{spec.itemName}` 을 찾지 못했습니다.");
            return 0;
        }

        int changed = 0;
        if (!item.placeable)
        {
            item.placeable = true;
            EditorUtility.SetDirty(item);
            changed++;
        }

        string path = $"{BlockFolder}/{spec.assetName}.asset";
        PipeBlock block = AssetDatabase.LoadAssetAtPath<PipeBlock>(path);
        bool isNew = block == null;
        if (isNew) block = ScriptableObject.CreateInstance<PipeBlock>();

        // blockName 은 아이템에서 그대로 복사한다. 손으로 타이핑하면 한글 정규화(NFC/NFD)가
        // 어긋나 겉보기엔 같아도 딕셔너리 조회가 조용히 실패한다.
        block.blockName = item.itemName;
        block.displayName = item.displayName;
        block.dropItem = item;
        block.dropCount = 1;

        block.kind = spec.kind;
        block.tier = spec.tier;
        block.secondsPerCell = spec.secondsPerCell;
        block.throughput = spec.throughput;
        block.tint = spec.tint;

        string atlasPath = $"{AtlasFolder}/pipe_{AtlasNameFor(spec.kind)}_atlas.asset";
        block.atlas = AssetDatabase.LoadAssetAtPath<PipeAtlas>(atlasPath);
        if (block.atlas == null)
            report.AppendLine($"- ⚠ 아틀라스 `{atlasPath}` 가 없습니다. Slice Pipe Sheet → Build Pipe Atlas 를 먼저 돌리세요.");

        if (isNew) AssetDatabase.CreateAsset(block, path);
        else EditorUtility.SetDirty(block);
        changed++;

        report.AppendLine($"- **{item.DisplayName}** — {spec.kind} 티어{spec.tier} · 칸당 {spec.secondsPerCell:0.00}초"
            + $" · 한 번에 {spec.throughput}개 · `{path}` {(isNew ? "(생성)" : "(갱신)")}");
        return changed;
    }
}
