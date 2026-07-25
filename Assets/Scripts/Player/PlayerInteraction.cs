using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private MachineInteraction machineInteraction;
    private Inventory inventory;
    private Vector2Int targetGlobalCell;
    private Vector2Int playerGlobalCell;
    private Vector2 mousePosition;
    private bool isPointerOverUI;



    [SerializeField] private LayerMask occupiedLayers = (1 << 6) | (1 << 7);
    [SerializeField, Range(0.1f, 1f)] private float occupancyCheckSize = 0.8f;

    private void OnEnable()
    {
        if (InputActionManager.Instance != null){
            InputActionManager.Instance.OnHitPerformed += HandleHitPerformed;
            InputActionManager.Instance.OnUsePerformed += HandleUsePerformed;
        }
    }
    void Start()
    {
        inventory = Inventory.Instance;
    }

    private void OnDisable()
    {
        if (InputActionManager.Instance != null){
            InputActionManager.Instance.OnHitPerformed -= HandleHitPerformed;
            InputActionManager.Instance.OnUsePerformed -= HandleUsePerformed;
        }
    }

    private void HandleHitPerformed()
    {
        if (UIManager.Instance != null && UIManager.Instance.isAnyUIOpen && UIManager.Instance.OpenUICount > 0)
            return;

        if (isPointerOverUI)
            return;

        // 클릭 셀이 윤곽선 안이면: 그 아래 셀도 윤곽선에 속하면 아래 셀을, 아니면 클릭 셀을 채굴 대상으로 한다.
        Vector2Int mineCell = targetGlobalCell;
        TilemapTextureLoader loader = TilemapTextureLoader.Instance;
        if (loader != null && loader.IsOutlined(targetGlobalCell))
        {
            mineCell = loader.IsOutlined(targetGlobalCell + Vector2Int.down)
                ? targetGlobalCell + Vector2Int.down
                : targetGlobalCell;
        }

        if (!GetIsCardinalAdjacent(mineCell, playerGlobalCell))
            return;

        Vector3 cell = new Vector3(mineCell.x, mineCell.y, 0f);
        if (WorldMap.Instance.Mining(Chunk.GetChunkId(cell), Chunk.GetLocalCellPositionInChunk(cell)))
            mapGenerator.RefreshMinedTile(mineCell);
    }
    // 우클릭(Use)에 대한 단일 판별 지점: 기계 위면 그 기계 UI 오픈, 빈 칸이면 placeable 배치.
    private void HandleUsePerformed()
    {
        if (Camera.main == null) return;
        // 열린 UI 패널 위에서의 우클릭은 배치/상호작용으로 처리하지 않는다.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Vector2 mouseWorldPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2Int targetCell = (Vector2Int)mapGenerator.blocksTilemap.WorldToCell(mouseWorldPosition);
        Vector2Int playerCell = (Vector2Int)mapGenerator.blocksTilemap.WorldToCell(transform.position);
        bool adjacent = Mathf.Abs(targetCell.x - playerCell.x) + Mathf.Abs(targetCell.y - playerCell.y) == 1;

        // 1) 대상 셀에 기계가 있으면 → 그 기계 UI 오픈 (배치보다 우선)
        if (mapGenerator.TryGetMachineAt(targetCell, out MachineInstance machine))
        {
            if (adjacent && machineInteraction != null)
                machineInteraction.OpenMachine(machine);
            return;
        }

        // 2) placeable 선택 & 인접한 빈 칸 → 배치
        ItemStack selectedItemStack = inventory.GetSelectedItem();
        if (selectedItemStack == null || selectedItemStack.item == null || !selectedItemStack.item.placeable)
            return;
        if (!adjacent)
            return;

        Vector3 cellPos = new Vector3(targetCell.x, targetCell.y, 0f);
        Vector2Int chunkId = Chunk.GetChunkId(cellPos);
        Vector2Int localCell = Chunk.GetLocalCellPositionInChunk(cellPos);
        Chunk chunk = WorldMap.Instance.GetOrCreateChunk(chunkId);
        if (chunk.GetPlaceable(localCell) != null)
            return; // 이미 placeable 이 있는 칸

        PlaceableRecord record = new PlaceableRecord(selectedItemStack.item.itemName);
        chunk.SetPlaceable(localCell, record);
        mapGenerator.SpawnPlaceableAt(targetCell, record);

        selectedItemStack.count--;
        if (selectedItemStack.count <= 0)
            selectedItemStack.item = null;
        Inventory.Instance.OnChanged?.Invoke();
    }
    private bool GetIsCardinalAdjacent(Vector2Int targetGlobalCell, Vector2Int playerGlobalCell)
    {
        SetGlobalCellPositions();
        Vector2Int delta = targetGlobalCell - playerGlobalCell;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) <= 1.5f;
    }
    private void SetGlobalCellPositions()
    {
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue(); 
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
        mousePosition = mousePos;
        Vector2Int chunkToMining = Chunk.GetChunkId(mousePos);
        Vector2Int toMining = Chunk.GetLocalCellPositionInChunk(mousePos);

        Vector2Int playerChunk = Chunk.GetChunkId(transform.position);
        Vector2Int playerCell = Chunk.GetLocalCellPositionInChunk(transform.position);

        targetGlobalCell = chunkToMining * WorldMap.ChunkSize + toMining;
        playerGlobalCell = playerChunk * WorldMap.ChunkSize + playerCell;
    }
    private void Update()
    {
        SetGlobalCellPositions();
        isPointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if(GetIsCardinalAdjacent(targetGlobalCell, playerGlobalCell))
        {
            TilemapTextureLoader.Instance.ShowOutline(targetGlobalCell);
        } 
    }
}
