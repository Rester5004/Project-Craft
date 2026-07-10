using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>우클릭한 인접 칸에 대한 설치 요청을 플래그로 전달합니다.</summary>
public class MachinePlacement : MonoBehaviour
{
    [SerializeField] private LayerMask occupiedLayers = (1 << 6) | (1 << 7);
    [SerializeField, Range(0.1f, 1f)] private float occupancyCheckSize = 0.8f;
    [SerializeField] private int placementFlag;

    private void Update()
    {
        if (UIManager.Instance != null && UIManager.Instance.isAnyUIOpen) return;
        if (Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        TryRequestPlacement();
    }

    private void TryRequestPlacement()
    {
        if (Camera.main == null) return;

        Vector2 mouseWorldPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2Int playerCell = Vector2Int.RoundToInt(transform.position);
        Vector2Int targetCell = Vector2Int.RoundToInt(mouseWorldPosition);
        Vector2Int difference = targetCell - playerCell;

        if (Mathf.Abs(difference.x) + Mathf.Abs(difference.y) != 1) return;

        Collider2D occupiedCollider = Physics2D.OverlapBox(
            targetCell,
            Vector2.one * occupancyCheckSize,
            0f,
            occupiedLayers);

        if (occupiedCollider != null) return;

        placementFlag = 1;
        Debug.Log($"[MachinePlacement] 설치 요청 성공. placementFlag = {placementFlag}, 위치: {targetCell}");
    }

    public void ResetPlacementFlag()
    {
        placementFlag = 0;
    }
}
