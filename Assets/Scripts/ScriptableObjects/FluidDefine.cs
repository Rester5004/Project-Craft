using UnityEngine;

/// <summary>액체인가 기체인가. 어느 파이프가 나르는지를 이 값이 정한다(<see cref="PipeKind"/> 와 짝).</summary>
public enum FluidPhase
{
    Liquid = 0,
    Gas = 1,
}

/// <summary>
/// 유체 한 종류(물 · 용암 · 수소 …).
///
/// <b>액체와 기체를 한 계층으로 둔다.</b> 실제로 다른 것은 "어느 파이프가 나르는가" 하나뿐이고
/// 그것은 <see cref="phase"/> → <see cref="PipeKind"/> 가 표현한다. 둘로 나누면 기계 탱크 ·
/// <see cref="Recipe"/> · <see cref="RecipeSolver"/> · 세이브가 전부 두 벌이 되고, 언젠가 한쪽만 고쳐진다.
///
/// <b>양의 단위를 코드는 모른다 — 정수일 뿐이다.</b> 규약으로 1 양동이 = <see cref="bucketAmount"/>(1000)
/// 을 쓴다. 나중에 mL 로 세분화하려면 레시피의 숫자만 바꾸면 되고, float 누적 오차도 없다.
/// </summary>
[CreateAssetMenu(fileName = "Fluid", menuName = "Items/Fluid")]
public class FluidDefine : ScriptableObject
{
    [Tooltip("세이브 키. 반드시 영어 snake_case — 바꾸면 기존 세이브의 탱크 내용이 사라진다.")]
    public string fluidId;

    [Tooltip("화면에 표시할 이름(한글). 타이핑하지 말고 기존 에셋에서 복사할 것(NFC/NFD 가 겉보기엔 같다).")]
    public string displayName;

    // 색은 여기 두지 않는다 — <see cref="FluidColors"/> 가 fluidId 로 찾는 static 정본 표다.
    // 탱크 바·슬롯은 유체 <b>이름만</b> 받아 스스로 색을 고르므로 그리는 쪽이 이 에셋을 몰라도 된다.

    [Tooltip("액체 파이프가 나를지 기체 파이프가 나를지를 정한다.")]
    public FluidPhase phase = FluidPhase.Liquid;

    [Header("양동이 변환 (없으면 파이프로만 옮길 수 있다)")]
    [Tooltip("이 유체가 담긴 '채워진 양동이' 아이템. 예: 물 · 용암 · 원유.")]
    public Items bucketItem;

    [Tooltip("퍼내고 남는 빈 그릇(양동이 · 유리 용기). bucketItem 이 있으면 함께 지정해야 교환이 성립한다.")]
    public Items emptyItem;

    [Tooltip("채워진 그릇 하나에 담기는 양. 1 양동이 = 1000 이 규약이다.")]
    [Min(1)] public int bucketAmount = 1000;

    /// <summary>표시 이름(비었으면 fluidId, 그것도 비면 에셋 이름).</summary>
    public string DisplayName => !string.IsNullOrEmpty(displayName)
        ? displayName
        : (string.IsNullOrEmpty(fluidId) ? name : fluidId);

    /// <summary>양동이로 퍼 담고 비울 수 있는가. 둘 중 하나만 있으면 교환이 반쪽이 되므로 둘 다 요구한다.</summary>
    public bool HasBucket => bucketItem != null && emptyItem != null && bucketAmount > 0;

    /// <summary>이 유체를 나를 수 있는 파이프 종류.</summary>
    public PipeKind CarriedBy => phase == FluidPhase.Gas ? PipeKind.Gas : PipeKind.Liquid;
}
