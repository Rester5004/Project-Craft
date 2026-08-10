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

    /// <summary>
    /// 이번 홀드를 시작할 때 손에 들고 있던 곡괭이. 도중에 다른 도구로 바꾸면 진행도를 버리기 위해 기억한다
    /// (칸을 바꿔도 <see cref="ItemStack"/> 객체는 슬롯의 것이라 그대로다 — 개체 데이터로 비교해야 한다).
    /// </summary>
    private ToolInstance miningTool;

    /// <summary>
    /// 이번 프레임에 우클릭이 들어왔는가. <b>입력 콜백에서 곧바로 처리하지 않고 <see cref="Update"/> 로 미룬다.</b>
    ///
    /// 우클릭 판정의 핵심 가드가 <c>EventSystem.IsPointerOverGameObject()</c> 인데, 이것을 InputAction
    /// 콜백 안에서 부르면 <b>지난 프레임의 UI 상태</b>가 돌아온다(콜백이 UI 레이캐스트보다 먼저 돈다).
    /// 그러면 이번 프레임에 뜬 패널 위에서 누른 우클릭이 월드로 새어 배치·기계 열기가 일어나고,
    /// Unity 도 경고를 낸다. <c>Update</c> 는 UI 갱신 뒤라 <see cref="isPointerOverUI"/> 가 이번 프레임 값이다.
    /// </summary>
    private bool usePending;

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
    }

    private void OnDisable()
    {
        // 구독 해제는 InstanceIfAlive 로 찾는다. Instance 는 종료 중 null 을 돌려줘 해제가 조용히 건너뛰어진다.
        InputActionManager input = InputActionManager.InstanceIfAlive;
        if (input != null){
            input.OnUsePerformed -= HandleUsePerformed;
            input.OnInteractPerformed -= HandleInteractPerformed;
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
    /// <summary>E(Interact) 에 대한 단일 판별 지점. 지금은 발밑 포탈뿐이다.</summary>
    private void HandleInteractPerformed()
    {
        if (UndergroundPortal.TryUseNearest(transform)) return;
    }
    /// <summary>입력 콜백은 "눌렸다"만 적어 둔다. 판정은 전부 <see cref="PerformUse"/> 가 한다(<see cref="usePending"/> 참고).</summary>
    private void HandleUsePerformed() => usePending = true;

    /// <summary>
    /// 우클릭(Use)에 대한 단일 판별 지점: 기계 위면 그 기계 UI 오픈, 빈 칸이면 placeable 배치.
    /// <b><see cref="Update"/> 에서만 부른다</b> — UI 위인지 판정하려면 이번 프레임 값이 필요하다.
    /// </summary>
    private void PerformUse()
    {
        if (Camera.main == null) return;
        if (PowerLinkMode.IsActive) return;   // 전송 모드의 우클릭은 연결 해제지 배치가 아니다
        // 열린 UI 패널 위에서의 우클릭은 배치/상호작용으로 처리하지 않는다.
        if (isPointerOverUI) return;

        Vector2 mouseWorldPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2Int targetCell = (Vector2Int)mapGenerator.blocksTilemap.WorldToCell(mouseWorldPosition);
        Vector2Int playerCell = (Vector2Int)mapGenerator.blocksTilemap.WorldToCell(transform.position);
        bool adjacent = Mathf.Abs(targetCell.x - playerCell.x) + Mathf.Abs(targetCell.y - playerCell.y) == 1;

        // 0) 렌치를 들고 있으면 파이프 연결면 설정이 가장 앞선다.
        //    파이프 면이 아니면 흘려보낸다 — 렌치를 들었다고 기계 UI 까지 막히면 안 된다.
        ItemStack heldStack = inventory.GetSelectedItem();
        if (adjacent && heldStack != null && heldStack.item is WrenchItem
            && TryWrench(mouseWorldPosition, targetCell))
            return;

        // 1) 대상 셀에 기계가 있으면 → 그 기계 UI 오픈 (배치보다 우선)
        if (mapGenerator.TryGetMachineAt(targetCell, out MachineInstance machine))
        {
            if (adjacent && machineInteraction != null)
                machineInteraction.OpenMachine(machine);
            return;
        }

        // 1-b) 탐지기를 들고 인접한 빈 바닥을 누르면 지하 포탈을 찾는다(확률).
        //      배치보다 앞이지만 기계보다는 뒤다 — 탐지기를 들었다고 기계를 못 열면 안 된다.
        if (adjacent && TryDowse(heldStack, targetCell))
            return;

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
            // 농지 같은 바닥 블록은 현재 바닥 한 칸을 교체한다.
            if (Chunk.IsFloor(terrain.blockName))
            {
                if (!WorldMap.Instance.PlaceFloor(chunkId, localCell, terrain.blockName)) return;
                mapGenerator.RefreshTile(targetCell);
                ConsumeSelected(selectedItemStack);
                return;
            }

            // 농지 같은 바닥 블록은 현재 바닥 한 칸을 교체한다.
            if (Chunk.IsFloor(terrain.blockName))
            {
                if (!WorldMap.Instance.PlaceFloor(chunkId, localCell, terrain.blockName)) return;
                mapGenerator.RefreshTile(targetCell);
                ConsumeSelected(selectedItemStack);
                return;
            }

            if (!IsCellClearForWall(targetCell))
                return; // 플레이어나 기계 위에 벽을 세우면 갇히거나 겹친다
            if (!WorldMap.Instance.Place(chunkId, localCell, terrain.blockName))
                return; // 이미 벽인 칸

            mapGenerator.RefreshTile(targetCell);
            ConsumeSelected(selectedItemStack);
            return;
        }


        // 2-b) 씨앗/묘목은 설정된 농지 위에만 심는다.
        CropBlock crop = ItemDictionary.Instance != null
            ? ItemDictionary.Instance.GetCropForSeed(selectedItemStack.item)
            : null;
        if (crop != null)
        {
            if (WorldMap.Instance.GetTileId(targetCell) != crop.requiredSoilId) return;
            PlaceableRecord cropRecord = new PlaceableRecord(crop.blockName)
            {
                plantedAtUtcTicks = System.DateTime.UtcNow.Ticks
            };
            chunk.SetPlaceable(localCell, cropRecord);
            mapGenerator.SpawnPlaceableAt(targetCell, cropRecord);
            ConsumeSelected(selectedItemStack);
            return;
        }

        // 2-c) 파이프는 프리팹이 아니라 전용 타일맵에 그린다(수백 개가 깔리므로).
        PipeBlock pipe = ItemDictionary.Instance != null
            ? ItemDictionary.Instance.GetPipeBlockFor(selectedItemStack.item)
            : null;
        if (pipe != null)
        {
            // 벽 속에 묻으면 벽 텍스처에 완전히 가려 보이지 않는다.
            if (!Chunk.IsFloor(WorldMap.Instance.GetTileId(targetCell)))
                return;

            PlaceableRecord pipeRecord = new PlaceableRecord(selectedItemStack.item.itemName);
            if (!TryPlaceRecord(chunk, localCell, targetCell, pipeRecord)) return;
            ConsumeSelected(selectedItemStack);
            return;
        }

        // 2-d) 기계는 프리팹을 세운다.
        // 지형·파이프와 달리 예전에는 여기에만 자리 검사가 없어 플레이어가 선 칸에도 기계를 세울 수 있었다.
        if (!IsCellClearForWall(targetCell))
            return;

        PlaceableRecord record = new PlaceableRecord(selectedItemStack.item.itemName);
        if (!TryPlaceRecord(chunk, localCell, targetCell, record)) return;

        ConsumeSelected(selectedItemStack);
    }

    /// <summary>
    /// 레코드를 청크에 박고 실제로 세운다. <b>세우지 못하면 레코드를 되돌린다.</b>
    ///
    /// 되돌리지 않으면 그 칸은 <c>GetPlaceable() != null</c> 이라 다시 놓을 수 없고,
    /// 기계도 파이프도 아니고 바닥이라 <see cref="UpdateMining"/> 의 세 분기가 전부 탈락해
    /// <b>캘 수조차 없는 칸</b>이 되어 세이브에 영구히 남는다.
    /// </summary>
    private bool TryPlaceRecord(Chunk chunk, Vector2Int localCell, Vector2Int targetCell, PlaceableRecord record)
    {
        chunk.SetPlaceable(localCell, record);
        if (mapGenerator.SpawnPlaceableAt(targetCell, record)) return true;

        chunk.RemovePlaceable(localCell);
        return false;
    }

    /// <summary>
    /// 탐지기 우클릭. 빈 바닥을 짚으면 탐지기를 하나 쓰고, <see cref="UndergroundPalette.DiscoveryChance"/>
    /// 확률로 그 자리에 지하 포탈이 열린다.
    ///
    /// <b>실패해도 탐지기는 사라진다</b> — 그것이 확률의 대가다. 어느 등급인지는
    /// <see cref="UndergroundPalette.DowsingTierOf"/> 한 곳이 정하므로 상위 탐지기가 생겨도 여기는 그대로다.
    /// ⚠ 포탈은 <b>세이브에 남지 않는다</b>(런타임 오브젝트). 찾았으면 그 자리에서 들어가야 한다.
    /// </summary>
    /// <returns>탐지기를 실제로 썼으면 true. false 면 호출자가 평소 동작으로 흘려보낸다.</returns>
    private bool TryDowse(ItemStack held, Vector2Int targetCell)
    {
        if (UndergroundSession.IsActive) return false;   // 지하에서 또 파고들 수는 없다
        if (held == null || held.item == null || held.count <= 0) return false;

        int tier = UndergroundPalette.DowsingTierOf(held.item.itemName);
        if (tier < 0) return false;

        // 벽·기계·파이프가 있는 칸에는 열 수 없다(포탈이 파묻히거나 겹친다).
        if (!Chunk.IsFloor(WorldMap.Instance.GetTileId(targetCell))) return false;
        if (WorldMap.Instance.GetPlaceableAt(targetCell) != null) return false;

        ConsumeSelected(held);

        if (Random.value >= UndergroundPalette.DiscoveryChance)
        {
            Debug.Log("[Dowsing] 아무것도 찾지 못했습니다.");
            return true;
        }

        UndergroundPortal.Create(new Vector2(targetCell.x + 0.5f, targetCell.y + 0.5f),
                                 UndergroundPortal.Kind.ToUnderground, tier);
        Debug.Log($"[Dowsing] {tier}등급 지하 포탈을 찾았습니다! ({targetCell.x}, {targetCell.y}) 에서 E");
        return true;
    }

    /// <summary>
    /// 렌치 우클릭. <b>어느 파이프의 어느 면인지 고르는 것까지</b>가 여기 몫이고,
    /// 그 면이 무엇으로 바뀌는지는 <see cref="PipeNetworkManager.CycleFace"/> 가 정한다.
    ///
    /// 파이프 칸뿐 아니라 <b>기계 칸의 그쪽 절반</b>을 눌러도 통한다 —
    /// "기계와 파이프 사이"는 양쪽 어디를 눌러도 같은 이음매여야 자연스럽다.
    /// </summary>
    /// <returns>면을 실제로 바꿨으면 true. false 면 호출자가 평소 동작으로 흘려보낸다.</returns>
    private bool TryWrench(Vector2 mouseWorldPosition, Vector2Int targetCell)
    {
        PipeNetworkManager network = PipeNetworkManager.Active;
        if (network == null || mapGenerator == null) return false;

        int face = NearestFace(mouseWorldPosition, targetCell);

        if (mapGenerator.TryGetPipeAt(targetCell, out _))
            return network.CycleFace(targetCell, face);

        // 기계 쪽 절반을 눌렀다면 그 너머 파이프의 반대쪽 면을 만진다.
        if (mapGenerator.TryGetMachineAt(targetCell, out _))
        {
            Vector2Int across = targetCell + PipeRouter.Directions[face];
            if (mapGenerator.TryGetPipeAt(across, out _))
                return network.CycleFace(across, PipeRouter.Opposite(face));
        }

        return false;
    }

    /// <summary>
    /// 커서가 셀 안 어디에 있는지로 가장 가까운 면(N=0, E=1, S=2, W=3)을 고른다.
    /// 사각지대를 두지 않는다 — 한가운데를 눌러도 어느 한 면은 잡히는 편이 손에 붙는다.
    /// </summary>
    private int NearestFace(Vector2 mouseWorldPosition, Vector2Int cell)
    {
        float dx = mouseWorldPosition.x - (cell.x + 0.5f);
        float dy = mouseWorldPosition.y - (cell.y + 0.5f);

        if (Mathf.Abs(dy) > Mathf.Abs(dx)) return dy > 0f ? 0 : 2;   // N : S
        return dx > 0f ? 1 : 3;                                       // E : W
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
        // 전력 전송 모드에서는 커서 윤곽선을 그리지 않는다(전용 오버레이가 대신 표시한다).
        // 여기서 return 하면 진행 중이던 채굴 홀드도 함께 끊긴다.
        if (PowerLinkMode.IsActive)
        {
            if (TilemapTextureLoader.Instance != null) TilemapTextureLoader.Instance.ClearOutline();
            CancelMining();
            usePending = false;   // 미뤄 둔 우클릭을 흘려보낸다 — 안 지우면 모드를 끄는 순간 묵은 클릭이 터진다
            return;
        }

        SetGlobalCellPositions();
        isPointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // 채굴보다 먼저 처리해야 방금 놓은 기계를 같은 프레임의 채굴 판정이 본다.
        if (usePending) { usePending = false; PerformUse(); }

        UpdateMining();
        if(GetIsCardinalAdjacent(targetGlobalCell, playerGlobalCell))
        {
            TilemapTextureLoader.Instance.ShowOutline(targetGlobalCell);
        } 
    }

    /// <summary>좌클릭 홀드로 캘 수 있는 대상의 종류.</summary>
    private enum MineTarget { None, Wall, Machine, Pipe, Crop }

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

        // 배치물이 놓인 칸은 그 아래 지형이 아니라 배치물을 캔다.
        // <b>곡괭이는 벽에만 요구한다</b> — 기계·파이프까지 막으면 곡괭이가 부러졌을 때
        // 이미 지어 둔 공장을 뜯지 못해 갇힌다.
        MineTarget target = MineTarget.None;
        ToolInstance tool = null;
        if (inputAllowed)
        {
            if (mapGenerator.TryGetCropAt(mineCell, out CropInstance crop) && crop != null && crop.IsMature) target = MineTarget.Crop;
            else if (mapGenerator.TryGetMachineAt(mineCell, out _)) target = MineTarget.Machine;
            else if (mapGenerator.TryGetPipeAt(mineCell, out _)) target = MineTarget.Pipe;
            else if (WorldMap.Instance.IsMineable(chunkId, localCell) && TryGetMiningTool(out _, out tool))
                target = MineTarget.Wall;
        }

        if (target == MineTarget.None)
        {
            CancelMining();
            return;
        }

        // 캐는 도중 다른 곡괭이로 바꾸면 처음부터다(도구를 갈아 끼워 진행도를 이어받지 못하게).
        if (!isMining || miningTarget != mineCell || miningTool != tool)
        {
            miningTarget = mineCell;
            miningTool = tool;
            miningProgress = 0f;
            isMining = true;
        }

        miningProgress += Time.deltaTime;
        if (miningProgress < miningHoldDuration)
            return;

        if (target == MineTarget.Machine) MineMachine(mineCell);
        else if (target == MineTarget.Pipe) mapGenerator.RemoveMachineAt(mineCell);   // 안에서 파이프로 갈라진다
        else if (target == MineTarget.Crop) mapGenerator.HarvestCropAt(mineCell);
        else if (target == MineTarget.Crop) mapGenerator.HarvestCropAt(mineCell);
        else if (MineWall(mineCell, chunkId, localCell)) WearMiningTool();            // 실제로 캔 경우에만 닳는다

        CancelMining();
    }

    /// <summary>
    /// 지금 손에 든 것이 벽을 캘 수 있는 도구인가.
    ///
    /// 곡괭이는 망치·드라이버와 <see cref="ToolItem"/> 하나를 공유하므로 <see cref="WrenchItem"/> 처럼
    /// <b>타입으로는 구분되지 않는다</b>. 설계도의 <see cref="ToolDefinition.canMineBlocks"/> 가 정본이라
    /// 문자열 비교도 씬 참조도 없이 에셋만 보고 판정된다(재료 티어별 제한도 나중에 같은 자리에 붙는다).
    /// </summary>
    private bool TryGetMiningTool(out ItemStack stack, out ToolInstance tool)
    {
        stack = null;
        tool = null;
        if (inventory == null) return false;

        // GetSelectedItem() 이 아니라 GetSelectedStack() 을 쓴다 — 전자는 선택 칸이 없을 때
        // (ConsumeSelectedItem 이 -1 로 만든다) 예외를 던진다.
        ItemStack held = inventory.GetSelectedStack();
        if (held == null) return false;
        if (held.item is not ToolItem item || item.definition == null || !item.definition.canMineBlocks) return false;
        if (held.instance is not ToolInstance instance || instance.durability <= 0) return false;

        stack = held;
        tool = instance;
        return true;
    }

    /// <summary>
    /// 벽 한 칸을 캘 때마다 곡괭이 내구도를 1 깎는다. 0 이 되면 스택째 사라진다 —
    /// <see cref="RecipeSolver.ConsumeTools"/> 와 <b>같은 규약</b>이라 도구가 없어지는 방식이 한 가지뿐이다.
    /// </summary>
    private void WearMiningTool()
    {
        if (!TryGetMiningTool(out ItemStack stack, out ToolInstance tool)) return;

        tool.durability -= 1;
        if (tool.durability <= 0) stack.Clear();
        inventory.NotifyChanged();   // 핫바의 내구도 표시를 갱신한다
    }

    /// <summary>벽을 캐고 그 블록에 지정된 아이템을 필드에 떨어뜨린다. 실제로 캤으면 true.</summary>
    private bool MineWall(Vector2Int mineCell, Vector2Int chunkId, Vector2Int localCell)
    {
        if (!WorldMap.Instance.Mining(chunkId, localCell, out string minedTileId)) return false;

        BlockBase block = ItemDictionary.Instance != null ? ItemDictionary.Instance.GetBlock(minedTileId) : null;
        if (block != null && block.dropItem != null)
            mapGenerator.SpawnDrop(mineCell, new ItemStack { item = block.dropItem, count = block.dropCount });

        mapGenerator.RefreshTile(mineCell);
        return true;
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
        miningTool = null;
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
