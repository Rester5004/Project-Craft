using UnityEngine;

// 인벤토리 패널 자체가 비활성 상태로 시작할 수 있으므로,
// 항상 활성화되어 있는 별도의 오브젝트(플레이어 등)에 붙여서 토글 키를 감지합니다.
public class InventoryToggle : MonoBehaviour
{
    private MachineInteraction machineInteraction;

    void Start()
    {
        machineInteraction = FindAnyObjectByType<MachineInteraction>();
    }

    void OnEnable()
    {
        if (InputActionManager.Instance != null)
            InputActionManager.Instance.OnToggleInventoryPerformed += Toggle;
    }

    void OnDisable()
    {
        InputActionManager input = InputActionManager.InstanceIfAlive;   // 종료 중엔 Instance 가 null 이다
        if (input != null) input.OnToggleInventoryPerformed -= Toggle;
    }

    private void Toggle()
    {
        if (UIManager.Instance == null)
            return;

        // 기계 UI가 열려 있으면 i 키는 기계 뷰(기계+인벤토리)를 닫는다.
        if (machineInteraction != null && machineInteraction.IsOpen)
        {
            machineInteraction.CloseView();
            return;
        }

        // 그 외에는 인벤토리 패널만 토글(실제 열림 상태 기준으로 판단해 desync 방지).
        if (UIManager.Instance.IsOpen("Inventory"))
            UIManager.Instance.CloseUI("Inventory");
        else
            UIManager.Instance.OpenUI("Inventory");
    }
}
