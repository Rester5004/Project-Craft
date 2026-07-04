using UnityEngine;
using UnityEngine.InputSystem; 
using UnityEngine.EventSystems; 

public class MachineInteraction : MonoBehaviour
{
    [Header("UI Settings")]
    public GameObject machineUI; // ui는 만들고 비활성화된 상태로 두기 > 그래야 클릭할때 켜짐

    [Header("Interaction Settings")]
    public LayerMask machineLayer;  //태그 Machine으로 기계블록에 등록해두고 여기에 넣으면 돼요!
    public float interactRange = 1.5f; 

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current.IsPointerOverGameObject())
                return;

            if (machineUI != null && machineUI.activeSelf)  //이미 ui켜져있으면 다시 키기 x, 켜져있는동안 다른거 상호작용x
                return;

            TryInteractWithMachine();
        }
    }

    void TryInteractWithMachine()
    {
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue(); 
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

        Collider2D hitCollider = Physics2D.OverlapPoint(mousePos, machineLayer); 

        if (hitCollider != null)
        {
            Vector2 playerPos = transform.position;
            Vector2 machinePos = hitCollider.transform.position; 

            float diffX = Mathf.Abs(playerPos.x - machinePos.x);
            float diffY = Mathf.Abs(playerPos.y - machinePos.y);

            bool isAdjacent = (diffX <= interactRange && diffY < 0.5f) || 
                              (diffY <= interactRange && diffX < 0.5f);

            if (isAdjacent)
            {
                OpenUI();
            }
        }
    }

    private void OpenUI()
    {
        if (machineUI != null)
        {
            machineUI.SetActive(true);
        }
    }

    public void CloseUI()
    {
        if (machineUI != null)
        {
            machineUI.SetActive(false);
        }
    }
}