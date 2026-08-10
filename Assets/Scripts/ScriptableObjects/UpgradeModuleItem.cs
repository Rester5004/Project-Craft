using UnityEngine;

/// <summary>업그레이드 모듈이 무엇을 바꾸는가. 값은 직렬화 호환을 위해 고정한다.</summary>
public enum UpgradeKind
{
    /// <summary>가공 시간을 줄인다(<see cref="MachineInstance.EffectiveCraftTime"/>).</summary>
    Speed = 0,
    /// <summary>전력과 연료 소모를 함께 줄인다. 발전기에서는 같은 연료로 더 많은 전력을 낸다.</summary>
    Efficiency = 1,
}

/// <summary>
/// 기계의 업그레이드 칸에 꽂는 모듈. <see cref="WrenchItem"/> 과 같은 <b>타입으로 판정</b>하는 아이템이라
/// 문자열 비교가 필요 없다.
///
/// <b>소모되지 않는다.</b> 칸에 들어 있는 <b>개수</b>만큼 효과가 붙으므로,
/// 몇 개까지 꽂을 수 있는지는 <see cref="Items.maxStack"/> 이 정한다.
///
/// 수치를 코드 상수가 아니라 여기에 두는 이유: 밸런스는 반드시 여러 번 바뀌는데,
/// 그때마다 스크립트를 고치면 에셋과 코드 중 어느 쪽이 정본인지 흐려진다.
/// </summary>
[CreateAssetMenu(fileName = "UpgradeModule", menuName = "Items/Upgrade Module")]
public class UpgradeModuleItem : Items
{
    [Tooltip("이 모듈이 바꾸는 것.")]
    public UpgradeKind kind = UpgradeKind.Speed;

    [Tooltip("1개당 효과. 속도는 +비율(0.25 = 개당 25% 빨라짐), 효율은 -비율(0.10 = 개당 10% 덜 씀).")]
    [Min(0f)] public float valuePerUnit = 0.25f;
}
