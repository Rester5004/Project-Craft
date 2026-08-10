using UnityEngine;

/// <summary>
/// 기계 UI 레이아웃에서 각 오브젝트가 담당하는 역할.
/// <b>값은 직렬화 호환을 위해 고정한다</b> — 프리팹에는 정수로 저장돼 있어 이름만 바꾸는 것은 안전하지만
/// 숫자를 바꾸면 이미 만든 UI 프리팹의 역할이 통째로 뒤섞인다.
/// (구 GasBar=4 → InputGasBar → InputFluidBar 로 이름만 이어받았다.)
/// </summary>
public enum MachineUIRole
{
    InputSlot = 0,
    OutputSlot = 1,
    /// <summary>입력 탱크 잔량 바(액체·기체 공용).</summary>
    InputFluidBar = 4,
    /// <summary>출력 탱크 잔량 바.</summary>
    OutputFluidBar = 6,
    ProgressBar = 2,
    EnergyBar = 3,
    MachineName = 5,
    /// <summary>연료를 넣는 칸(화로 등). 컨테이너 평면 인덱스는 [입력][출력][연료] 순서다.</summary>
    FuelSlot = 7,
    /// <summary>지금 타고 있는 연료의 잔량(0~1).</summary>
    FuelBar = 8,
    /// <summary>
    /// 손으로 돌리는 기계의 작동 버튼(<see cref="MachineBlock.IsManual"/>). 누를 때마다 진행도가 오른다.
    /// 자동 기계의 패널에 들어 있어도 런타임에 꺼지므로, 공유 프리팹에 넣어도 안전하다.
    /// </summary>
    ManualButton = 9,
    /// <summary>
    /// 업그레이드 모듈 칸(속도·효율). 컨테이너 평면 인덱스는 [입력][출력][연료][업그레이드] 순서다.
    /// 코어 조합기에서는 이 칸이 티어 상승용으로 쓰인다(넣으면 소모된다).
    /// </summary>
    UpgradeSlot = 10,
    /// <summary>
    /// 저장 블록(상자·아이템 저장소)의 칸.
    ///
    /// <b>평면 인덱스는 입력 구간과 같다</b> — 저장 칸은 새 구간이 아니라 <c>inputSlots</c> 이기 때문이다
    /// (<see cref="StorageBlock"/> 주석 참조). 역할을 따로 둔 것은 프리팹에서 저장용임을 드러내고
    /// 종류 제한(개체 데이터 거부)이 붙은 전용 프리팹을 쓰기 위해서다.
    /// 그래서 <b>한 프리팹에 InputSlot 과 StorageSlot 을 같이 두면 안 된다</b> — 같은 인덱스를 두 요소가 노린다.
    /// </summary>
    StorageSlot = 11,
    /// <summary>
    /// 코어 조합기의 "티어 업그레이드" 버튼. 재료는 <see cref="UpgradeSlot"/> 칸에 넣는다.
    ///
    /// 라벨은 <see cref="CraftingTableUI"/> 가 현재 티어에 맞춰 매 프레임 갈아 끼우므로
    /// 버튼 아래에 <c>TMP_Text</c> 가 하나 있어야 한다.
    /// 조합대 패널 하나를 5종(코어·고급 조합기 + 재단 3종)이 나눠 쓰지만
    /// <b>코어가 아닌 기계에서는 자동으로 꺼진다</b>(`AcceptsTierUpgrade`).
    /// </summary>
    CoreUpgradeButton = 12
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
