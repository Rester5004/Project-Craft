using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private Inventory inventory;
    private bool hitRequested;

    private void OnEnable()
    {
        if (InputActionManager.Instance != null)
            InputActionManager.Instance.OnHitPerformed += HandleHitPerformed;
    }

    private void OnDisable()
    {
        if (InputActionManager.Instance != null)
            InputActionManager.Instance.OnHitPerformed -= HandleHitPerformed;
    }

    private void HandleHitPerformed()
    {
        hitRequested = true;
    }

    private void Update()
    {
        if (!hitRequested)
            return;

        hitRequested = false;

        if (UIManager.Instance != null && UIManager.Instance.isAnyUIOpen && UIManager.Instance.OpenUICount > 0)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        TrySelectBlock();
    }

    private void TrySelectBlock()
    {
        if (Camera.main == null || WorldMap.Instance == null)
            return;

        Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2Int targetChunk = Chunk.GetChunkId(mousePosition);
        Vector2Int targetCell = Chunk.GetLocalCellPositionInChunk(mousePosition);

        Vector2Int playerChunk = Chunk.GetChunkId(transform.position);
        Vector2Int playerCell = Chunk.GetLocalCellPositionInChunk(transform.position);

        Vector2Int targetGlobalCell = targetChunk * WorldMap.ChunkSize + targetCell;
        Vector2Int playerGlobalCell = playerChunk * WorldMap.ChunkSize + playerCell;

        if (Mathf.Abs(targetGlobalCell.x - playerGlobalCell.x) + Mathf.Abs(targetGlobalCell.y - playerGlobalCell.y) != 1)
            return;

        if (WorldMap.Instance.Mining(targetChunk, targetCell))
            mapGenerator.RefreshMinedTile(targetGlobalCell);
    }
}
