using UnityEngine;

/// <summary>
/// 기계 UI 레이아웃에서 각 오브젝트가 담당하는 역할.
/// 값은 직렬화 호환을 위해 명시적으로 고정한다(구 GasBar=4 → InputGasBar 로 승계).
/// </summary>
public enum MachineUIRole
{
    InputSlot = 0,
    OutputSlot = 1,
    InputGasBar = 4,
    OutputGasBar = 6,
    ProgressBar = 2,
    EnergyBar = 3,
    MachineName = 5
}

/// <summary>
/// 기계 UI 프리팹 안의 요소(슬롯/바/이름)에 붙는 역할 태그.
/// <see cref="DefaultMachineUI"/> 가 자식에서 이 컴포넌트를 수집해 역할·index 순으로 바인딩하므로,
/// 레이아웃(위치·개수)은 프리팹에서 자유롭게 구성할 수 있다.
/// </summary>
[DisallowMultipleComponent]
public class MachineUIElement : MonoBehaviour
{
    [Tooltip("이 오브젝트가 담당하는 역할")]
    public MachineUIRole role = MachineUIRole.InputSlot;

    [Tooltip("같은 역할 내 순번(0부터). 슬롯 순서·가스바 1/2 구분에 사용된다.")]
    public int index;
}
