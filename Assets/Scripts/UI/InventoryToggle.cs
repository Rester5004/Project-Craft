using UnityEngine;

// 인벤토리 패널 자체가 비활성 상태로 시작할 수 있으므로,
// 항상 활성화되어 있는 별도의 오브젝트(플레이어 등)에 붙여서 토글 키를 감지합니다.
public class InventoryToggle : MonoBehaviour
{
    private bool isOpen = false;
    void OnEnable()
    {
        if (InputActionManager.Instance != null)
            InputActionManager.Instance.OnToggleInventoryPerformed += Toggle;
    }

    void OnDisable()
    {
        if (InputActionManager.Instance != null)
            InputActionManager.Instance.OnToggleInventoryPerformed -= Toggle;
    }

    private void Toggle()
    {
        if (UIManager.Instance == null) 
            return;
        if(isOpen)
        {
            UIManager.Instance.CloseUI("Inventory");
            isOpen = false;
        }
        else
        {
            UIManager.Instance.OpenUI("Inventory");
            isOpen = true;
        }
    }
}
