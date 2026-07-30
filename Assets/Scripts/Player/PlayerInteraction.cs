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
    [SerializeField] private int placementFlag;
    [Header("Mining")]
    [SerializeField, Min(0.1f)] private float miningHoldDuration = 0.75f;

    private Vector2Int miningTarget;
    private float miningProgress;
    private bool isMining;

    private void OnEnable()
    {
        if (InputActionManager.Instance != null){
            // 채굴은 좌클릭 홀드(UpdateMining)로 처리하므로 OnHitPerformed(클릭 즉시 채굴)는 구독하지 않는다.
            InputActionManager.Instance.OnUsePerformed += HandleUsePerformed;
            InputActionManager.Instance.OnInteractPerformed += HandleInteractPerformed;
        }
    }
    void Start()
    {
        inventory = Inventory.Instance;
        PrototypeMapTransitions.Initialize();
    }

    private void OnDisable()
    {
        if (InputActionManager.Instance != null){
            InputActionManager.Instance.OnUsePerformed -= HandleUsePerformed;
            InputActionManager.Instance.OnInteractPerformed -= HandleInteractPerformed;
        }
    }

    /// <summary>커서 셀이 벽 윤곽선의 윗면이면 아래 벽 셀을 채굴 대상으로 보정해 반환한다.</summary>
    private Vector2Int ResolveMineCell()
    {
        Vector2Int cell = targetGlobalCell;
        TilemapTextureLoader loader = TilemapTextureLoader.Instance;
        if (loader != null && loader.IsOutlined(cell))
            cell = loader.IsOutlined(cell + Vector2Int.down) ? cell + Vector2Int.down : cell;
        return cell;
    }
    // 우클릭(Use)에 대한 단일 판별 지점: 기계 위면 그 기계 UI 오픈, 빈 칸이면 placeable 배치.
    private void HandleInteractPerformed()
    {
        if(PrototypeMapTransitions.TryUseNearest(gameObject.transform))
            PrototypeMapTransitions.Initialize();
    }
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

        // 2-a) 지형 블록(돌·마력석 등)은 프리팹이 아니라 타일로 놓는다.
        MainBlock terrain = ItemDictionary.Instance != null
            ? ItemDictionary.Instance.GetTerrainBlockFor(selectedItemStack.item)
            : null;
        if (terrain != null)
        {
            if (!IsCellClearForWall(targetCell))
                return; // 플레이어나 기계 위에 벽을 세우면 갇히거나 겹친다
            if (!WorldMap.Instance.Place(chunkId, localCell, terrain.blockName))
                return; // 이미 벽인 칸

            mapGenerator.RefreshTile(targetCell);
            ConsumeSelected(selectedItemStack);
            return;
        }

        // 2-b) 기계는 프리팹을 세운다.
        PlaceableRecord record = new PlaceableRecord(selectedItemStack.item.itemName);
        chunk.SetPlaceable(localCell, record);
        mapGenerator.SpawnPlaceableAt(targetCell, record);

        ConsumeSelected(selectedItemStack);
    }

    private void ConsumeSelected(ItemStack stack)
    {
        stack.count--;
        if (stack.count <= 0)
            stack.Clear();
        Inventory.Instance.OnChanged?.Invoke();
    }

    /// <summary>
    /// 이 칸에 벽을 세워도 되는가. 벽은 통과할 수 없으니 무언가가 서 있는 칸에 세우면 그 안에 갇힌다.
    /// </summary>
    private bool IsCellClearForWall(Vector2Int cell)
    {
        Vector3 center = mapGenerator.blocksTilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
        Vector2 size = Vector2.one * occupancyCheckSize;

        if (Physics2D.OverlapBox(center, size, 0f, occupiedLayers) != null)
            return false;

        // 플레이어는 Default 레이어라 위 마스크에 걸리지 않는다. 자기 자신은 따로 확인한다
        // — 콜라이더가 한 칸보다 크지 않아도 두 칸에 걸쳐 서 있을 수 있다.
        Collider2D self = GetComponent<Collider2D>();
        return self == null || !self.bounds.Intersects(new Bounds(center, new Vector3(size.x, size.y, 1f)));
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
        UpdateMining();
        if(GetIsCardinalAdjacent(targetGlobalCell, playerGlobalCell))
        {
            TilemapTextureLoader.Instance.ShowOutline(targetGlobalCell);
        } 
    }

    /// <summary>좌클릭 홀드로 캘 수 있는 대상의 종류.</summary>
    private enum MineTarget { None, Wall, Machine }

    private void UpdateMining()
    {
        // 커서 셀을 윤곽선 기준으로 보정(벽 윗면 클릭 → 아래 벽)해 대상 셀을 정한다.
        Vector2Int mineCell = ResolveMineCell();
        Vector3 cellPos = new Vector3(mineCell.x, mineCell.y, 0f);
        Vector2Int chunkId = Chunk.GetChunkId(cellPos);
        Vector2Int localCell = Chunk.GetLocalCellPositionInChunk(cellPos);

        bool inputAllowed = !isPointerOverUI
            && (UIManager.Instance == null || !UIManager.Instance.isAnyUIOpen || UIManager.Instance.OpenUICount == 0)
            && Mouse.current != null
            && Mouse.current.leftButton.isPressed
            && GetIsCardinalAdjacent(mineCell, playerGlobalCell)
            && WorldMap.Instance != null;

        // 기계가 놓인 칸은 그 아래 바닥이 아니라 기계를 캔다.
        MineTarget target = MineTarget.None;
        if (inputAllowed)
        {
            if (mapGenerator.TryGetMachineAt(mineCell, out _)) target = MineTarget.Machine;
            else if (WorldMap.Instance.IsMineable(chunkId, localCell)) target = MineTarget.Wall;
        }

        if (target == MineTarget.None)
        {
            CancelMining();
            return;
        }

        if (!isMining || miningTarget != mineCell)
        {
            miningTarget = mineCell;
            miningProgress = 0f;
            isMining = true;
        }

        miningProgress += Time.deltaTime;
        if (miningProgress < miningHoldDuration)
            return;

        if (target == MineTarget.Machine) MineMachine(mineCell);
        else MineWall(mineCell, chunkId, localCell);

        CancelMining();
    }

    /// <summary>벽을 캐고 그 블록에 지정된 아이템을 필드에 떨어뜨린다.</summary>
    private void MineWall(Vector2Int mineCell, Vector2Int chunkId, Vector2Int localCell)
    {
        if (!WorldMap.Instance.Mining(chunkId, localCell, out string minedTileId)) return;

        BlockBase block = ItemDictionary.Instance != null ? ItemDictionary.Instance.GetBlock(minedTileId) : null;
        if (block != null && block.dropItem != null)
            mapGenerator.SpawnDrop(mineCell, new ItemStack { item = block.dropItem, count = block.dropCount });

        mapGenerator.RefreshTile(mineCell);
    }

    /// <summary>기계를 캔다. 내부 아이템은 필드로 쏟아지고 에너지·가스·연소 상태는 사라진다.</summary>
    private void MineMachine(Vector2Int mineCell)
    {
        if (!mapGenerator.TryGetMachineAt(mineCell, out MachineInstance machine)) return;

        // 이 기계를 보고 있던 UI 를 먼저 닫는다. 안 닫으면 파괴된 인스턴스를 계속 붙들고 있게 된다.
        if (machineInteraction != null && machineInteraction.CurrentMachine == machine)
            machineInteraction.CloseView();

        mapGenerator.RemoveMachineAt(mineCell);
    }

    private void CancelMining()
    {
        isMining = false;
        miningProgress = 0f;
    }

    private void OnGUI()
    {
        if (!isMining || miningHoldDuration <= 0f)
            return;

        const float width = 170f;
        const float height = 14f;
        Rect background = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.72f, width, height);
        GUI.Box(background, GUIContent.none);
        Color previous = GUI.color;
        GUI.color = new Color(0.25f, 0.9f, 1f, 1f);
        GUI.DrawTexture(new Rect(background.x + 2f, background.y + 2f, (width - 4f) * Mathf.Clamp01(miningProgress / miningHoldDuration), height - 4f), Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
