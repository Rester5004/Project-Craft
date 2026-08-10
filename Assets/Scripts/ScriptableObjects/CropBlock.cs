using UnityEngine;

/// <summary>씨앗 하나가 자라서 수확되는 작물의 설정.</summary>
[CreateAssetMenu(fileName = "Crop", menuName = "Blocks/Farming Crop")]
public class CropBlock : BlockBase
{
    [Header("성장")]
    [Tooltip("씨앗을 심은 뒤 완전히 자랄 때까지의 실제 시간(초).")]
    [Min(0.1f)] public float growthSeconds = 10f;

    [Tooltip("이 바닥 ID 위에만 심을 수 있다.")]
    public string requiredSoilId = "floor:farm_soil";

    [Header("표시 (프로토타입)")]
    [Tooltip("에셋이 준비되기 전 성장 단계 표시에 사용할 스프라이트.")]
    public Sprite cropSprite;
    [Range(0.1f, 1f)] public float seedlingScale = 0.35f;
    [Range(0.1f, 1f)] public float growingScale = 0.65f;

    [Header("수확")]
    public Items harvestItem;
    [Min(1)] public int harvestCount = 1;
    [Tooltip("수확할 때 씨앗도 돌려받을 개수. 0이면 돌려받지 않는다.")]
    [Min(0)] public int seedReturnCount = 1;
}
