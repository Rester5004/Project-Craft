using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>핫바에서 선택한 machine 아이템을 우클릭한 인접 칸에 설치합니다.</summary>
public class MachinePlacement : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    // Machine(6), Mine(7) 레이어만 설치를 막습니다. 바닥 Collider는 설치를 막지 않습니다.
    [SerializeField] private LayerMask occupiedLayers = (1 << 6) | (1 << 7);
    [SerializeField, Range(0.1f, 1f)] private float occupancyCheckSize = 0.8f;

    [Header("Temporary Test Settings")]
    [Tooltip("핫바 선택 기능이 없을 때만 사용하는 테스트용 머신입니다.")]
    [SerializeField] private machine testSelectedMachine;
    [SerializeField] private Sprite testMachineSprite;

    private void Awake()
    {
        if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
    }

    private void Update()
    {
        if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen) return;
        if (Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        TryPlaceMachine();
    }

    private void TryPlaceMachine()
    {
        ItemStack selectedStack = inventory != null ? inventory.GetSelectedStack() : null;
        machine selectedMachine = selectedStack != null ? selectedStack.item as machine : testSelectedMachine;
        bool useRuntimeTestMachine = selectedMachine == null || selectedMachine.worldPrefab == null;

        if (Camera.main == null)
        {
            Debug.LogWarning("[MachinePlacement] MainCamera를 찾지 못했습니다.");
            return;
        }

        Vector2 mouseWorldPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2Int playerCell = Vector2Int.RoundToInt(transform.position);
        Vector2Int targetCell = Vector2Int.RoundToInt(mouseWorldPosition);
        Vector2Int difference = targetCell - playerCell;

        // 대각선과 두 칸 이상 떨어진 땅에는 설치하지 않습니다.
        if (Mathf.Abs(difference.x) + Mathf.Abs(difference.y) != 1)
        {
            Debug.Log($"[MachinePlacement] 설치 실패: 플레이어 칸 {playerCell}의 상하좌우 한 칸을 클릭해야 합니다. 클릭 칸: {targetCell}");
            return;
        }

        Vector2 targetPosition = targetCell;
        Collider2D occupiedCollider = Physics2D.OverlapBox(targetPosition, Vector2.one * occupancyCheckSize, 0f, occupiedLayers);
        if (occupiedCollider != null)
        {
            Debug.Log($"[MachinePlacement] 설치 실패: {occupiedCollider.name}이(가) 해당 칸을 점유하고 있습니다.");
            return;
        }

        if (useRuntimeTestMachine)
            CreateRuntimeTestMachine(targetPosition);
        else
            Instantiate(selectedMachine.worldPrefab, targetPosition, Quaternion.identity);

        Debug.Log($"[MachinePlacement] 설치 성공: {targetCell}");

        // 실제 핫바에서 선택한 아이템일 때만 수량을 차감합니다.
        if (!useRuntimeTestMachine && selectedStack != null)
            inventory.ConsumeSelectedItem();
    }

    private void CreateRuntimeTestMachine(Vector2 position)
    {
        GameObject testMachine = new GameObject("TestMachine (Runtime)");
        testMachine.layer = LayerMask.NameToLayer("Machine");
        testMachine.transform.position = position;

        SpriteRenderer spriteRenderer = testMachine.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = testMachineSprite;
        spriteRenderer.color = Color.red;

        BoxCollider2D collider = testMachine.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one * occupancyCheckSize;
    }
}
