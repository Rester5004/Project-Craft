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
    public bool IsOpen => CurrentMachine != null;

    /// <summary>지정한 기계에 맞는 UI를 열고, 인벤토리 패널도 함께 활성화한다.</summary>
    public void OpenMachine(MachineInstance instance)
    {
        if (instance == null || machineUIHost == null) return;
        CurrentMachine = instance;
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
    }
}
