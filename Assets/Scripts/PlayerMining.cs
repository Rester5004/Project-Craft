using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems; // UI 클릭 방지를 위해 추가

public class PlayerMining : MonoBehaviour
{
    [SerializeField] private MapGenerator mapGenerator;
    void OnEnable()
    {
        if (InputActionManager.Instance != null)
            InputActionManager.Instance.OnHitPerformed += HandleHitPerformed;
    }

    void OnDisable()
    {
        if (InputActionManager.Instance != null)
            InputActionManager.Instance.OnHitPerformed -= HandleHitPerformed;
    }

    private void HandleHitPerformed()
    {
        // 1. 인벤토리나 기계 UI 등이 열려있으면 광질 시도 자체를 차단!
        if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen){
            Debug.Log("UI가 열려있으므로 광질을 시도하지 않습니다.");
            return;
        }

        // 2. 허공에 떠 있는 UI(버튼 등)를 클릭한 경우 광질 안 함!
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()){
            Debug.Log("UI 위에서 클릭했으므로 광질을 시도하지 않습니다.");
            return;
        }

        TrySelectBlock();
    }

    private void TrySelectBlock()
    {
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue(); 
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

        Vector2Int chunkToMining = Chunk.GetChunkId(mousePos);
        Vector2Int toMining = Chunk.GetLocalCellPositionInChunk(mousePos);

        Vector2Int playerChunk = Chunk.GetChunkId(transform.position);
        Vector2Int playerCell = Chunk.GetLocalCellPositionInChunk(transform.position);

        Vector2Int targetGlobalCell = chunkToMining * WorldMap.ChunkSize + toMining;
        Vector2Int playerGlobalCell = playerChunk * WorldMap.ChunkSize + playerCell;
        Vector2Int delta = targetGlobalCell - playerGlobalCell;

        bool isCardinalAdjacent = Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1;
        if (isCardinalAdjacent)
        {
            if (WorldMap.Instance.Mining(chunkToMining, toMining))
            {
                mapGenerator.RefreshMinedTile(targetGlobalCell);
                Debug.Log($"플레이어가 선택한 블록을 광질했습니다. 청크: {chunkToMining}, 셀: {toMining}");
            }
        }
        else
        {
            Debug.Log("플레이어와 선택한 블록이 인접하지 않으므로 광질을 시도하지 않습니다.");
        }
    }
}