using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도구 체계의 런타임 색인. <see cref="ItemDictionary"/> 와 같은 규약으로
/// 인스펙터 리스트를 Awake 에서 Dictionary 로 만든다(에디터 툴이 리스트를 채운다).
///
/// 세이브에는 재질이 ID 문자열로만 남으므로, 로드할 때 ID → 에셋을 되찾는 곳이 반드시 필요하다.
/// </summary>
public class ToolDictionary : Singleton<ToolDictionary>
{
    [Header("Materials")]
    [SerializeField] private List<ToolMaterial> materials = new();

    [Header("Part Kinds")]
    [SerializeField] private List<ToolPartKind> partKinds = new();

    [Header("Parts")]
    [SerializeField] private List<ToolPartItem> parts = new();

    [Header("Tools")]
    [SerializeField] private List<ToolItem> tools = new();

    [Header("Sprites")]
    [SerializeField] private ToolSpriteLibrary spriteLibrary;

    private readonly Dictionary<string, ToolMaterial> materialById = new();
    private readonly Dictionary<string, ToolPartKind> kindById = new();
    private readonly Dictionary<ToolDefinition, ToolItem> itemByDefinition = new();
    // (종류, 재질) → 부품 아이템
    private readonly Dictionary<(ToolPartKind, ToolMaterial), ToolPartItem> partByKindMaterial = new();

    public IReadOnlyList<ToolMaterial> Materials => materials;
    public IReadOnlyList<ToolPartKind> PartKinds => partKinds;
    public ToolSpriteLibrary SpriteLibrary => spriteLibrary;

    protected override void Awake()
    {
        base.Awake();
        Rebuild();
    }

    /// <summary>인스펙터 리스트로 색인을 다시 만든다.</summary>
    public void Rebuild()
    {
        materialById.Clear();
        kindById.Clear();
        itemByDefinition.Clear();
        partByKindMaterial.Clear();

        foreach (ToolMaterial material in materials)
        {
            if (material == null || string.IsNullOrEmpty(material.materialId)) continue;
            if (!materialById.TryAdd(material.materialId, material))
                Debug.LogWarning($"[ToolDictionary] 재질 ID '{material.materialId}' 가 중복입니다.", material);
        }

        foreach (ToolPartKind kind in partKinds)
        {
            if (kind == null || string.IsNullOrEmpty(kind.kindId)) continue;
            if (!kindById.TryAdd(kind.kindId, kind))
                Debug.LogWarning($"[ToolDictionary] 부품 종류 ID '{kind.kindId}' 가 중복입니다.", kind);
        }

        foreach (ToolPartItem part in parts)
        {
            if (part == null || part.kind == null || part.material == null) continue;
            partByKindMaterial[(part.kind, part.material)] = part;
        }

        foreach (ToolItem tool in tools)
        {
            if (tool == null || tool.definition == null) continue;
            if (!itemByDefinition.TryAdd(tool.definition, tool))
                Debug.LogWarning($"[ToolDictionary] 도구 '{tool.definition.name}' 에 아이템이 둘 이상 연결됐습니다.", tool);
        }
    }

    /// <summary>도메인 리로드로 색인만 비었을 때 복구한다(RecipeDictionary 와 같은 방식).</summary>
    private void EnsureIndex()
    {
        if (materialById.Count == 0 && materials.Count > 0) Rebuild();
    }

    public ToolMaterial GetMaterial(string materialId)
    {
        if (string.IsNullOrEmpty(materialId)) return null;
        EnsureIndex();
        return materialById.TryGetValue(materialId, out ToolMaterial material) ? material : null;
    }

    public ToolPartKind GetPartKind(string kindId)
    {
        if (string.IsNullOrEmpty(kindId)) return null;
        EnsureIndex();
        return kindById.TryGetValue(kindId, out ToolPartKind kind) ? kind : null;
    }

    /// <summary>종류 + 재질로 부품 아이템을 찾는다(없으면 null).</summary>
    public ToolPartItem GetPart(ToolPartKind kind, ToolMaterial material)
    {
        if (kind == null || material == null) return null;
        EnsureIndex();
        return partByKindMaterial.TryGetValue((kind, material), out ToolPartItem part) ? part : null;
    }

    /// <summary>설계도에 대응하는 완성 도구 아이템(없으면 null).</summary>
    public ToolItem GetToolItem(ToolDefinition definition)
    {
        if (definition == null) return null;
        EnsureIndex();
        return itemByDefinition.TryGetValue(definition, out ToolItem tool) ? tool : null;
    }

    /// <summary>이름으로 도구 스프라이트를 찾는다(없으면 null).</summary>
    public Sprite GetSprite(string spriteName)
        => spriteLibrary != null ? spriteLibrary.Get(spriteName) : null;
}
