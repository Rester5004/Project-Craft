using UnityEngine;

[CreateAssetMenu(fileName = "MachineBlock", menuName = "Blocks/MachineBlock")]
public class MachineBlock : BlockBase
{
    public GameObject machinePrefab;

    [Tooltip("이 기계 전용 UI 프리팹(Machine UI Factory 로 제작). 비우면 기본 패널을 사용한다.")]
    public GameObject uiPrefab;

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
    [Tooltip("가스 입력 슬롯 수")]
    [UnityEngine.Serialization.FormerlySerializedAs("gasSlotCount")]
    public int inputGasSlotCount = 0;

    [Tooltip("가스 출력 슬롯 수")]
    public int outputGasSlotCount = 0;

    [Tooltip("모든 가스 슬롯(입력/출력)이 공유하는 최대 저장량")]
    [UnityEngine.Serialization.FormerlySerializedAs("maxGasAmountForSlot1")]
    public float maxGasAmount = 0f;
    public float maxEnergyAmount = 0f;
    public bool isUseEnergy = false;

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

    /// <summary>레시피 조회 키. 업그레이드 관계인 기계들은 같은 값을 공유한다.</summary>
    public string RecipeGroupId => string.IsNullOrEmpty(recipeGroupId) ? blockName : recipeGroupId;
}
