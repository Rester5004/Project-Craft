using UnityEngine;

/// <summary>
/// 도구를 조립할 때 쓰는 부품 아이템(철 막대 · 금 곡괭이 머리 …).
/// 자기가 어떤 종류이고 어떤 재질인지 스스로 선언하므로, 조합대 부품 칸은
/// 하드코딩된 목록 없이 <c>item is ToolPartItem</c> 만으로 판정할 수 있다.
/// </summary>
[CreateAssetMenu(fileName = "ToolPartItem", menuName = "Items/Tool Part")]
public class ToolPartItem : Items
{
    [Header("도구 부품")]
    [Tooltip("이 부품의 종류(막대 · 망치 머리 …).")]
    public ToolPartKind kind;

    [Tooltip("이 부품의 재질.")]
    public ToolMaterial material;

    /// <summary>스프라이트 이름 규칙의 {material} 자리에 넣을 값.</summary>
    public string MaterialId => material != null ? material.materialId : "";
}
