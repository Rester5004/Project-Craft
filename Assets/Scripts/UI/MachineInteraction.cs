using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class MachineInteraction : MonoBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private GameObject machineUI;

    [Header("Interaction Settings")]
    [SerializeField] private LayerMask machineLayer;
    [SerializeField] private float interactRange = 1.5f;
    private bool useRequested;

    void OnEnable()
    {
        if (InputActionManager.Instance != null)
            InputActionManager.Instance.OnUsePerformed += HandleUsePerformed;
    }

    void OnDisable()
    {
        if (InputActionManager.Instance != null)
            InputActionManager.Instance.OnUsePerformed -= HandleUsePerformed;
    }

    private void HandleUsePerformed()
    {
        useRequested = true;
    }

    private void Update()
    {
        if (!useRequested)
            return;

        useRequested = false;

        if (machineUI != null && machineUI.activeSelf)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        TryInteractWithMachine();
    }

    private void TryInteractWithMachine()
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

            bool isAdjacent = (diffX <= interactRange && diffY < 0.5f) || (diffY <= interactRange && diffX < 0.5f);

            if (isAdjacent)
            {
                OpenUI();
            }
        }
    }

    private void OpenUI()
    {
        // UIManager를 통해서 UI를 열어달라고 요청!
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OpenUI("Machine");
            Debug.Log("기계 UI 열렸음");
        }
    }

    public void CloseUI() 
    {
        // UIManager를 통해서 UI를 닫아달라고 요청!
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseUI("Machine");
        }
    }
}
