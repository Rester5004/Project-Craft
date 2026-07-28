using UnityEngine;

[CreateAssetMenu(fileName = "MachineBlock", menuName = "Blocks/MachineBlock")]
public class MachineBlock : BlockBase
{
    public GameObject machinePrefab;

    [Tooltip("이 기계 전용 UI 프리팹(Machine UI Factory 로 제작). 비우면 기본 패널을 사용한다.")]
    public GameObject uiPrefab;

    [Header("Machine UI 설정")]
    public int inputSlotCount = 3;
    public int outputSlotCount = 6;
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
}
