using UnityEngine;

/// <summary>
/// 도구 부품을 만들 수 있는 재질 하나(나무 · 돌 · 철 …).
/// 부품 아이템과 완성 도구가 이 에셋을 참조해 이름 · 색 · 성능을 가져간다.
///
/// 새 재질(합금 등)을 추가하려면 이 에셋을 만들고 같은 이름 규칙의 스프라이트를 넣으면 된다.
/// </summary>
[CreateAssetMenu(fileName = "ToolMaterial", menuName = "Tools/Tool Material")]
public class ToolMaterial : ScriptableObject
{
    [Tooltip("내부 ID(영문 소문자). 스프라이트 이름 규칙의 {material} 자리에 들어간다. 예: iron")]
    public string materialId;

    [Tooltip("화면에 표시할 이름(한글). 비우면 materialId 를 쓴다.")]
    public string displayName;

    [Tooltip("금속인가. 금속만 허용하는 부품 칸(드라이버 등)의 판정에 쓴다. 나무 · 돌 · 석영은 끈다.")]
    public bool isMetal = true;

    [Tooltip("재질별 그림이 없는 부품을 이 색으로 물들여 구분한다.")]
    public Color tint = Color.white;

    [Tooltip("이 재질이 도구의 '머리'로 쓰였을 때의 내구도 배율.")]
    [Min(0.01f)] public float durabilityFactor = 1f;

    [Tooltip("이 재질이 도구의 '자루'로 쓰였을 때의 내구도 배율.")]
    [Min(0.01f)] public float handleFactor = 1f;

    [Tooltip("채굴 등급(아직 채굴 로직이 쓰지 않는다. 확장용).")]
    [Min(0)] public int miningTier;

    /// <summary>표시에 쓸 이름(displayName 이 비면 materialId, 그것도 비면 에셋 이름).</summary>
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(displayName)) return displayName;
            return string.IsNullOrEmpty(materialId) ? name : materialId;
        }
    }
}
