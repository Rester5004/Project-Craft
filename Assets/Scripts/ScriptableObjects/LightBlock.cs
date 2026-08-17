using UnityEngine;

/// <summary>
/// 빛을 내는 배치물(횃불·전등). <see cref="MachineBlock"/> 을 물려받는 이유는 순전히 <b>공짜로 얻는 것</b>
/// 때문이다 — 프리팹 스폰·발자국·콜라이더·채굴/회수·세이브·청크 수명주기·전력 링크가 전부 그대로 돈다.
/// 새 배치물 <i>종류</i>를 만들면 <c>MapGenerator</c>·<c>PlayerInteraction</c> 등 여섯 곳에
/// 분기를 더해야 한다(<see cref="PipeBlock"/> 이 그 값을 치른 예다).
///
/// <b>빛 설정의 정본은 여기다.</b> 프리팹의 <c>Light2D</c> 는 빈 그릇이고 값은 <see cref="LightEmitter"/> 가
/// 배치 시점에 이 에셋에서 베껴 넣는다 — 두 곳에 두면 언젠가 어긋난다
/// (<see cref="MachineBlock.runningSprite"/> 와 같은 규약).
///
/// 파일명 = 클래스명을 유지해야 에셋의 m_Script 참조가 잡힌다.
/// </summary>
[CreateAssetMenu(fileName = "LightBlock", menuName = "Blocks/LightBlock")]
public class LightBlock : MachineBlock
{
    [Header("빛")]
    [Tooltip("빛의 색. 횃불은 주황, 전등은 흰빛에 가깝게.")]
    public Color lightColor = Color.white;

    [Tooltip("이 반경 안은 감쇠 없이 최대 밝기다(칸 단위).")]
    [Min(0f)] public float innerRadius = 1.5f;

    [Tooltip("빛이 닿는 최대 반경(칸 단위). 여기서 0 이 된다.")]
    [Min(0.1f)] public float outerRadius = 6f;

    [Tooltip("빛의 세기. 환경광에 더해지므로 1 이면 그 자리는 거의 원래 색으로 보인다.")]
    [Min(0f)] public float lightIntensity = 1f;

    [Tooltip("벽이 이 빛을 막는가. 끄면 벽 너머로 새어 나간다(작은 장식등에 쓸 수 있다).")]
    public bool castsShadows = true;

    [Tooltip("불꽃처럼 세기를 흔들지. 횃불은 켜고 전등은 끈다.")]
    public bool flicker = false;

    /// <summary>조명은 레시피 없이 그냥 켜져 있다. 전력을 쓰는 것이면 전력이 있을 때만 켜진다.</summary>
    public override bool IsAlwaysOn => true;

    /// <summary>슬롯이 하나도 없으므로 우클릭해도 빈 패널을 띄우지 않는다.</summary>
    public override bool OpensUI => false;

    /// <summary>
    /// ⚠ <b>반드시 true 여야 한다.</b> 아니면 <c>MachineInstance.ApplyConfig</c> 가 0/0 슬롯을
    /// "미설정"으로 보고 <b>조용히 3입력·6출력을 준다</b>(StorageBlock 이 같은 이유로 켠다).
    /// </summary>
    public override bool AllowsZeroSlots => true;

    /// <summary>가공하지 않으므로 자동 처리 루프에 들어갈 이유가 없다.</summary>
    public override bool AutoProcess => false;
}
