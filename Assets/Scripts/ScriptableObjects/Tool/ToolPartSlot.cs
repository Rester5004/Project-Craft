using System.Collections.Generic;
using UnityEngine;

/// <summary>부품 칸이 받아 줄 재질의 범위.</summary>
public enum MaterialFilter
{
    /// <summary>아래 목록에 있는 재질만(기본 16종처럼 손으로 고른 집합).</summary>
    Curated = 0,
    /// <summary><see cref="ToolMaterial.isMetal"/> 인 재질 전부. 나중에 합금이 늘어도 자동으로 허용된다.</summary>
    AnyMetal = 1,
    /// <summary>모든 재질.</summary>
    Any = 2,
}

/// <summary>
/// 도구 하나를 이루는 부품 칸 한 개. 이 칸은 두 가지를 동시에 뜻한다.
/// ① 조합할 때 필요한 <b>입력 슬롯</b> ② 완성된 도구 그림의 <b>레이어 한 장</b>.
/// <see cref="ToolDefinition.slots"/> 의 순서가 곧 레이어 순서(뒤가 위)다.
/// </summary>
[System.Serializable]
public class ToolPartSlot
{
    [Tooltip("이 칸에 넣을 부품의 종류.")]
    public ToolPartKind kind;

    [Tooltip("허용할 재질의 범위.")]
    public MaterialFilter filter = MaterialFilter.Curated;

    [Tooltip("filter 가 Curated 일 때만 쓰는 허용 재질 목록.")]
    public List<ToolMaterial> curated = new();

    [Tooltip("완성 그림에 쓸 스프라이트 이름. {material} 이 재질 ID 로 치환된다. 예: \"{material}_hammer\", \"driver\"")]
    public string layerSpritePattern;

    [Tooltip("재질별 그림이 없어 한 장을 공유할 때 켠다. 재질 색으로 물들여 구분한다.")]
    public bool tintByMaterial;

    /// <summary>이 칸이 해당 재질을 받아 주는가.</summary>
    public bool Allows(ToolMaterial material)
    {
        if (material == null) return false;
        switch (filter)
        {
            case MaterialFilter.Any: return true;
            case MaterialFilter.AnyMetal: return material.isMetal;
            default: return curated != null && curated.Contains(material);
        }
    }

    /// <summary>해당 재질일 때 쓸 스프라이트 이름.</summary>
    public string SpriteNameFor(string materialId)
    {
        if (string.IsNullOrEmpty(layerSpritePattern)) return "";
        return layerSpritePattern.Replace("{material}", materialId ?? "");
    }
}
