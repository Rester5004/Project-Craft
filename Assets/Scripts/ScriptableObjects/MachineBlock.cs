using UnityEngine;

[CreateAssetMenu(fileName = "MachineBlock", menuName = "Blocks/MachineBlock")]
public class MachineBlock : BlockBase
{
    public GameObject machinePrefab;

    [Tooltip("이 기계 전용 UI 프리팹(Machine UI Factory 로 제작). 비우면 기본 패널을 사용한다.")]
    public GameObject uiPrefab;

    [Header("가동 중 그림")]
    [Tooltip("가동 중일 때 보여줄 스프라이트. 비워 두면 그림을 바꾸지 않는다(대부분의 기계가 그렇다).\n" +
             "멈춰 있을 때의 그림은 여기가 아니라 machinePrefab 의 SpriteRenderer 가 정본이다.")]
    public Sprite runningSprite;

    [Header("수동 작동")]
    [Tooltip("0 이면 자동으로 가공한다. 0보다 크면 버튼 1회에 craftTime 의 이 비율만큼 진행한다.\n" +
             "0.05 = 20번 눌러야 하나가 완성된다.")]
    [Range(0f, 1f)] public float manualStepRatio = 0f;

    [Header("등급 · 레시피 연결")]
    [Tooltip("이 기계의 티어. recipe.tier 가 이 값 이하인 레시피만 처리한다.")]
    [Min(0)] public int tier;

    [Tooltip("레시피를 공유하는 그룹 이름. 비우면 blockName 을 쓴다.\n" +
             "0/1/2티어 화로처럼 업그레이드 관계인 기계들이 같은 레시피 목록을 보게 할 때 지정한다.")]
    public string recipeGroupId;

    [Header("Machine UI 설정")]
    public int inputSlotCount = 3;
    public int outputSlotCount = 6;

    [Tooltip("연료 칸 수. 1 이상이면 연료를 태워 가동한다(화로 등). 0 이면 연료가 필요 없다.")]
    [Min(0)] public int fuelSlotCount = 0;

    [Tooltip("가동 중 1초에 태우는 연료 에너지. 석탄 1개(400)면 이 값으로 나눈 만큼 버틴다.")]
    [Min(0.01f)] public float fuelBurnRate = 20f;

    [Tooltip("업그레이드 모듈 칸 수. 일반 기계는 2(속도·효율), 조합대·재단은 0, 코어 조합기만 1(티어 상승용).\n" +
             "모듈은 소모되지 않고 칸에 들어 있는 개수만큼 효과가 붙는다.")]
    [Min(0)] public int upgradeSlotCount = 2;
    [Header("유체 (액체·기체를 한 계층으로 다룬다)")]
    [Tooltip("입력 탱크 수. 레시피의 fluidInputs 를 여기서 가져간다.\n" +
             "탱크 한 칸에는 한 종류만 담기므로, 두 종류를 먹는 레시피는 2칸 이상이 필요하다.")]
    [UnityEngine.Serialization.FormerlySerializedAs("inputGasSlotCount")]
    [UnityEngine.Serialization.FormerlySerializedAs("gasSlotCount")]
    [Min(0)] public int inputFluidSlotCount = 0;

    [Tooltip("출력 탱크 수. 레시피의 fluidOutputs 가 여기 쌓인다.\n" +
             "산출 종류 수보다 적으면 자리를 못 잡아 기계가 영원히 멈춘다.")]
    [UnityEngine.Serialization.FormerlySerializedAs("outputGasSlotCount")]
    [Min(0)] public int outputFluidSlotCount = 0;

    // 옛 maxGasAmount 는 float 였다. 타입이 달라 FormerlySerializedAs 로는 못 물려받으므로
    // 값이 있던 3개 에셋(BioIncubator·Electrolyzer·LasorProcessor)은 손으로 다시 넣었다.
    [Tooltip("탱크 한 칸의 최대 저장량. 1 양동이 = 1000 이 규약이므로 8000 이면 8양동이다.")]
    [Min(0)] public int maxFluidAmount = 0;
    public float maxEnergyAmount = 0f;
    public bool isUseEnergy = false;

    [Header("전력")]
    [Tooltip("연료를 태워 전력을 만드는 발전기인가. 켜면 레시피 가공 대신 발전을 한다.")]
    public bool isGenerator = false;

    [Tooltip("전력을 보낼 수 있는 최대 거리(칸, 체비셰프). 0 이면 전송 불가. 중계기도 이 값을 쓴다.")]
    [Min(0)] public int powerRange = 0;

    [Tooltip("가동 중 1초에 쓰는 전력. 0 이면 maxEnergyAmount 의 10% 를 쓴다.")]
    [Min(0f)] public float energyUseRate = 0f;

    [Header("등급 배율 (추출기 계열)")]
    [Tooltip("가공 시간을 나누는 값. 2 면 두 배 빠르다.\n" +
             "같은 계열의 상위 등급이 같은 레시피를 더 빨리 돌게 하는 데 쓴다.")]
    [Min(0.1f)] public float speedMultiplier = 1f;

    [Tooltip("확률 산출에 곱하는 값. 1.5 면 부산물이 1.5배 자주 나온다.\n" +
             "확률은 레시피에 한 벌만 두고 등급차는 여기서 낸다 — 등급마다 레시피를 복제하지 않기 위해서다.")]
    [Min(0.1f)] public float chanceMultiplier = 1f;

    /// <summary>
    /// 입력/출력 슬롯이 둘 다 0 이어도 "미설정"으로 보지 않고 그대로 적용할지.
    /// 조합대처럼 슬롯이 없는 기계가 기본값(3/6)으로 폴백되지 않게 한다.
    /// </summary>
    public virtual bool AllowsZeroSlots => false;

    /// <summary>
    /// 입력 슬롯이 차면 스스로 레시피를 골라 가공할지.
    /// 조합대는 플레이어가 버튼을 눌러야 만들어지므로 false 다
    /// (입력 슬롯을 도구 부품 놓는 자리로 쓰는데 멋대로 가공되면 안 된다).
    /// </summary>
    public virtual bool AutoProcess => true;

    /// <summary>연료를 태워 돌아가는 기계인가.</summary>
    public bool UsesFuel => fuelSlotCount > 0;

    /// <summary>
    /// 버튼을 눌러야 진행하는 기계인가(손으로 돌리는 것).
    /// <see cref="AutoProcess"/> 가 false 인 조합대와는 다르다 — 조합대는 자기 슬롯을 아예 안 쓰고
    /// 플레이어 인벤토리로 만들지만, 수동 기계는 <b>레시피·슬롯 흐름이 자동 기계와 완전히 같고</b>
    /// 진행이 시간 대신 클릭으로 일어날 뿐이다.
    /// </summary>
    public bool IsManual => manualStepRatio > 0f;

    /// <summary>
    /// 연료를 태워 전력을 만드는 발전기인가. 연료 칸이 없으면 태울 것이 없으므로 발전기가 아니다.
    /// 발전만 안 하고 전송만 하는 중계기는 <see cref="isGenerator"/> 를 끄고 <see cref="powerRange"/> 만 준다.
    /// </summary>
    public bool IsGenerator => isGenerator && fuelSlotCount > 0;

    /// <summary>가동 중 초당 소비 전력. 미설정(0)이면 최대 저장량의 10% 로 폴백한다.</summary>
    public float EnergyUseRate => energyUseRate > 0f ? energyUseRate : maxEnergyAmount * 0.1f;

    /// <summary>레시피 조회 키. 업그레이드 관계인 기계들은 같은 값을 공유한다.</summary>
    public string RecipeGroupId => string.IsNullOrEmpty(recipeGroupId) ? blockName : recipeGroupId;
}
