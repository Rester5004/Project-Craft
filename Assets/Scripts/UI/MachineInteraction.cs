using UnityEngine;

/// <summary>
/// 기계 UI 오픈/클로즈 함수만 제공한다. 입력(우클릭) 판별은 PlayerInteraction 이 전담하며,
/// 판별 결과로 이 컴포넌트의 <see cref="OpenMachine"/>/<see cref="Close"/> 를 호출한다.
/// </summary>
public class MachineInteraction : MonoBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private MachineUIHost machineUIHost;

    public MachineInstance CurrentMachine { get; private set; }
    public bool IsOpen => viewOpen;

    // "지금 기계 UI 가 떠 있다" 는 사실. CurrentMachine 만으로는 판정할 수 없다 —
    // 기계가 파괴되면 그 참조가 유니티 가짜 null 이 되어 IsOpen 이 false 로 뒤집히는데,
    // 정작 UIManager 에는 "Machine" 이 열린 것으로 남아 있기 때문이다.
    private bool viewOpen;

    /// <summary>
    /// 보고 있던 기계가 사라졌으면 UI 도 닫는다.
    ///
    /// 채굴 경로는 <see cref="PlayerInteraction.MineMachine"/> 가 미리 닫아 주지만,
    /// <b>청크 언로드·RemoveMachineAt 직접 호출</b>에는 그 보호가 없다. 그대로 두면
    /// UIManager 의 openNames 에 "Machine" 이 남아 <c>isAnyUIOpen</c> 이 영구히 true 가 되고
    /// 채굴·배치·핫바가 통째로 잠긴다. 파괴 경로를 하나하나 쫓는 대신
    /// <b>판정이 있어야 할 이 한 곳에서</b> 알아챈다.
    /// </summary>
    private void Update()
    {
        if (viewOpen && CurrentMachine == null) CloseView();
    }

    /// <summary>지정한 기계에 맞는 UI를 열고, 인벤토리 패널도 함께 활성화한다.</summary>
    public void OpenMachine(MachineInstance instance)
    {
        if (instance == null || machineUIHost == null) return;
        CurrentMachine = instance;
        viewOpen = true;
        machineUIHost.Open(instance);
        if (UIManager.Instance != null)
            UIManager.Instance.OpenUI("Inventory"); // 기계 UI 동안 인벤토리 항상 활성(핫바는 자동 숨김)
    }

    /// <summary>기계 UI와 인벤토리를 함께 닫는다.</summary>
    public void CloseView()
    {
        if (machineUIHost != null) machineUIHost.Close();
        if (UIManager.Instance != null)
            UIManager.Instance.CloseUI("Inventory");
        CurrentMachine = null;
        viewOpen = false;
    }
}
