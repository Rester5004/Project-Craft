using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{

    [Header("타일맵")]
    [SerializeField] public Tilemap blocksTilemap;
    [SerializeField] public Tilemap floorTilemap;
    [SerializeField] public Tilemap placeableObjectsTilemap;
    [SerializeField] Transform player;

    [Header("공용 표시 에셋")]
    [Tooltip("월드 안내 표시(설치 미리보기·파이프 면 막대)가 조명을 받지 않게 하는 Unlit 머티리얼.\n" +
             "Assets/Asset/Common/OverlayUnlit.mat")]
    [SerializeField] Material overlayMaterial;

    [Tooltip("크기를 localScale 로 내는 표시물이 함께 쓰는 1×1 흰 스프라이트.\n" +
             "⚠ PPU 는 반드시 1 이어야 한다(Assets/Asset/Common/White1x1.png).")]
    [SerializeField] Sprite whitePixel;

    [SerializeField] int renderDistance = 2;
    [SerializeField] float saveCooldown = 10f; // 이 시간(초)이 지난 뒤에만 청크 이동 시 저장


    Vector2Int lastChunk = Vector2Int.zero;
    private bool isFirstUpdate = true;
    private float lastSaveTime = -Mathf.Infinity;
    private Dictionary<Vector2Int,Chunk> LoadedChunks = new Dictionary<Vector2Int,Chunk>();

    // 로드된 청크에 스폰된 기계들(월드 셀 → 인스턴스)
    private readonly Dictionary<Vector2Int, MachineInstance> loadedMachines = new();
    // 로드된 청크에 깔린 파이프들. 파이프는 오브젝트가 아니라 타일이라 순수 데이터만 든다.
    private readonly Dictionary<Vector2Int, PipeCell> loadedPipes = new();
    private readonly Dictionary<Vector2Int, CropInstance> loadedCrops = new();
    private Transform placeableContainer;

    // 로드된 청크에 스폰된 필드 드랍들(레코드 → 표시 오브젝트)
    private readonly Dictionary<DropRecord, DroppedItem> loadedDrops = new();
    private Transform dropContainer;

    /// <summary>드랍이 한 자리에 겹쳐 보이지 않게 셀 중심에서 흩뿌리는 반경.</summary>
    private const float DropScatter = 0.3f;


    /// <summary>씬에 하나뿐인 맵. 기계와 전력 전송 모드가 셀 조회에 쓴다.</summary>
    public static MapGenerator Active { get; private set; }

    void Awake()
    {
        // Start 에서 청크를 렌더하며 기계가 스폰되므로 그보다 앞서 등록되어야 한다.
        if (Active == null) Active = this;
    }

    void Start()
    {
        EnsurePlaceableContainer();
        // ⚠ MapLighting · ChunkShadowCasters 는 <b>GameRig 프리팹에 저작</b>돼 있다(여기서 만들지 않는다).
        //   저작된 컴포넌트의 Awake 는 이 Start 보다 먼저 도므로, 아래 UpdateChunks() 가
        //   RenderChunk → ChunkShadowCasters.Active 를 부를 때 이미 서 있다.
        //   아직 코드로 만드는 둘은 개수·모양이 데이터에 달린 자식을 풀링해서다(TODO §M-3 이관 대상).
        Transform gridRoot = placeableObjectsTilemap != null ? placeableObjectsTilemap.transform.parent : null;
        PipeNetworkManager.EnsureCreated(gridRoot, whitePixel, overlayMaterial);
        PlacementPreview.EnsureCreated(gridRoot, placeableObjectsTilemap, whitePixel, overlayMaterial);
        if (WorldMap.Instance != null)
            WorldMap.Instance.OnBeforeSave += FlushAll;
        UpdateChunks();
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
        WorldMap map = WorldMap.InstanceIfAlive;   // 종료 중엔 Instance 가 null 이다
        if (map != null) map.OnBeforeSave -= FlushAll;
    }

    private void EnsurePlaceableContainer()
    {
        if (placeableContainer != null) return;
        GameObject go = new GameObject("Placeables");
        placeableContainer = go.transform;
        if (placeableObjectsTilemap != null)
            placeableContainer.SetParent(placeableObjectsTilemap.transform.parent, false);
    }
    private void EnsureDropContainer()
    {
        if (dropContainer != null) return;
        GameObject go = new GameObject("Drops");
        dropContainer = go.transform;
        if (placeableObjectsTilemap != null)
            dropContainer.SetParent(placeableObjectsTilemap.transform.parent, false);
    }

    private TileBase LoadTile(string blockId)
    {
        if (string.IsNullOrEmpty(blockId)) return null;
        TileBase tile = ItemDictionary.Instance.GetTileFromBlockDictionary(blockId);
        if(tile == null)
            Debug.LogError($"Tile '{blockId}' not found.");
        return tile;
    }
    void Update()
    {
        UpdateChunks();
    }

    void UpdateChunks()
    {
        Vector2Int playerChunk = Chunk.GetChunkId(player.gameObject.transform.position);

        if (playerChunk == lastChunk && !isFirstUpdate)
            return;

        lastChunk = playerChunk;
        isFirstUpdate = false;

        LoadChunksAround(playerChunk);

        if (Time.time - lastSaveTime >= saveCooldown)
        {
            WorldMap.Instance.Save();
            lastSaveTime = Time.time;
        }
    }

    public void LoadChunksAround(Vector2Int playerChunk)
    {
        var toUnload = new List<Vector2Int>();
        foreach (var id in LoadedChunks.Keys)
        {
            int dist = Mathf.Max(Mathf.Abs(id.x - playerChunk.x), Mathf.Abs(id.y - playerChunk.y));
            if (dist > renderDistance)
                toUnload.Add(id);
        }
        foreach (var id in toUnload)
        {
            UnLoadChunk(id, LoadedChunks[id]);
            LoadedChunks.Remove(id);
        }

        // 1. 범위 내 청크 데이터 일괄 로드
        // 로드 범위와 위 언로드 판정(체비셰프 거리 > renderDistance)은 <b>반드시 같은 창</b>이어야 한다.
        // 예전에는 여기가 +renderDistance-1 이라 +renderDistance 청크가 새로 로드되지는 않는데
        // 이미 로드돼 있으면 언로드도 안 됐다 — 같은 자리인데 접근 경로에 따라 IsCellLoaded 가 달라져
        // 파이프 배달·전력 링크 판정 결과가 갈렸다.
        for (int x = playerChunk.x - renderDistance; x <= playerChunk.x + renderDistance; x++)
        {
            for (int y = playerChunk.y - renderDistance; y <= playerChunk.y + renderDistance; y++)
            {
                var id = new Vector2Int(x, y);
                if (!LoadedChunks.ContainsKey(id))
                {
                    Chunk chunk = WorldMap.Instance.GetOrCreateChunk(id);
                    RenderChunk(id, chunk);
                    LoadedChunks[id] = chunk;
                }
            }
        }

        RefreshAllTileTextures();
    }

    /// <summary>
    /// 로드된 모든 청크의 바닥/벽 텍스처를 처음부터 다시 계산해서 그립니다.
    /// LoadChunksAround가 청크 이동 시 항상 하던 것과 동일한 전체 재계산이라,
    /// 채굴 등으로 블록 데이터가 바뀐 뒤에도 이걸 호출하면 청크를 넘어간 것과 같은 결과를 보장합니다.
    /// </summary>
    private void RefreshAllTileTextures()
    {
        // 바닥 기본 텍스처 일괄 로드
        foreach (var pos in GetFloorTilePositions())
        {
            TilemapTextureLoader.Instance.LoadFloorTexture(pos);
        }

        // 탑다운 뷰 특성을 고려하여 위(Y 최고값)에서부터 아래로 순회
        List<Vector2Int> sortedChunkIds = new List<Vector2Int>(LoadedChunks.Keys);
        sortedChunkIds.Sort((a, b) => b.y.CompareTo(a.y)); // Y가 큰(위쪽) 청크부터 처리

        foreach (var chunkId in sortedChunkIds)
        {
            int size = WorldMap.ChunkSize;

            // 청크 내부를 위에서 아래로 순회
            for (int ty = size - 1; ty >= 0; ty--)
            {
                for (int tx = 0; tx < size; tx++)
                {
                    Vector2Int worldPos = new Vector2Int(chunkId.x * size + tx, chunkId.y * size + ty);

                    TilemapTextureLoader.Instance.LoadWallTexture(worldPos);
                }
            }
        }
    }

    public void RenderChunk(Vector2Int id, Chunk chunk)
    {
        int size = WorldMap.ChunkSize;
        for (int ty = 0; ty < size; ty++){
            for (int tx = 0; tx < size; tx++)
            {
                var pos = new Vector3Int(id.x * size + tx, id.y * size + ty, 0);
                string tileId = chunk.GetTile(tx, ty);
                if (Chunk.IsWall(tileId)){
                    floorTilemap.SetTile(pos, null);
                    blocksTilemap.SetTile(pos, LoadTile(tileId));
                }
                else{
                    blocksTilemap.SetTile(pos, null);
                    floorTilemap.SetTile(pos, LoadTile(tileId));
                }
            }
        }

        // 이 청크에 저장된 placeable(기계) 스폰
        foreach (var kvp in chunk.Placeables)
        {
            Vector2Int local = kvp.Key;
            Vector2Int worldCell = new Vector2Int(id.x * size + local.x, id.y * size + local.y);
            SpawnPlaceable(worldCell, kvp.Value);
        }

        // 이 청크에 떨어져 있던 아이템 스폰
        foreach (DropRecord drop in chunk.Drops) SpawnDropObject(drop, chunk);

        // 벽이 빛을 막게 하는 그림자 사각형. 타일을 다 칠한 뒤라야 벽 배치가 확정된다.
        if (ChunkShadowCasters.Active != null) ChunkShadowCasters.Active.Build(id, chunk);
    }

    // ── 필드 드랍 ──────────────────────────────────────────────────────
    private void SpawnDropObject(DropRecord record, Chunk chunk)
    {
        if (record == null || loadedDrops.ContainsKey(record)) return;
        EnsureDropContainer();

        DroppedItem drop = DroppedItem.Create(dropContainer, record, chunk);
        if (drop != null) loadedDrops[record] = drop;
    }

    /// <summary>
    /// 주워서 사라진 드랍을 표시 목록에서 지운다. <b>줍기 경로가 반드시 불러야 한다.</b>
    ///
    /// <see cref="UnLoadChunk"/> 는 <c>chunk.Drops</c> 를 순회해 정리하는데, 주운 레코드는
    /// 이미 청크에서 빠져 있어 그 순회에 절대 걸리지 않는다. 그래서 이 통지가 없으면
    /// (파괴된 오브젝트 + 레코드) 짝이 주울 때마다 하나씩 영구히 쌓인다.
    /// </summary>
    public void NotifyDropRemoved(DropRecord record)
    {
        if (record != null) loadedDrops.Remove(record);
    }

    /// <summary>필드에 아이템을 떨어뜨린다. 청크에 기록되므로 세이브에도 남는다.</summary>
    public void SpawnDrop(Vector2 worldPos, Items item, int count, ItemInstance instance = null)
    {
        if (item == null || count <= 0) return;

        Vector2 scattered = worldPos + Random.insideUnitCircle * DropScatter;
        Vector2Int chunkId = Chunk.GetChunkId(scattered);
        Chunk chunk = WorldMap.Instance.GetOrCreateChunk(chunkId);

        DropRecord record = new()
        {
            x = scattered.x,
            y = scattered.y,
            itemName = item.itemName,
            count = count,
            instance = instance,
        };
        chunk.AddDrop(record);
        SpawnDropObject(record, chunk);
    }

    /// <summary>스택 하나를 통째로 떨어뜨린다(기계를 캘 때 슬롯 내용을 쏟는 용도).</summary>
    public void SpawnDrop(Vector2Int cell, ItemStack stack)
    {
        if (stack == null || stack.item == null || stack.count <= 0) return;

        Vector3 center = placeableObjectsTilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
        SpawnDrop(center, stack.item, stack.count, stack.instance);
    }

    // ── placeable 스폰/디스폰/조회 ──────────────────────────────────────
    /// <summary>
    /// 배치물을 실제로 세운다. <b>성공 여부를 돌려준다</b> — 배치 경로가 실패를 알아야
    /// 레코드를 되돌리고 아이템을 소모하지 않을 수 있다(안 그러면 아무것도 못 놓고 캘 수도 없는 칸이 남는다).
    /// </summary>
    public bool SpawnPlaceableAt(Vector2Int worldCell, PlaceableRecord record) => SpawnPlaceable(worldCell, record);

    private bool SpawnPlaceable(Vector2Int worldCell, PlaceableRecord record)
    {
        if (record == null) return false;

        CropBlock crop = ItemDictionary.Instance != null ? ItemDictionary.Instance.GetCropInfo(record.blockId) : null;
        if (crop != null)
        {
            if (loadedCrops.ContainsKey(worldCell)) return false;
            SpawnCrop(worldCell, record, crop);
            return false;
        }

        // 파이프는 프리팹을 세우지 않고 타일맵에 그린다. 아래 기계 경로를 타면
        // MachineInstance 가 붙어 "유령 기계"가 되므로 반드시 여기서 갈라야 한다.
        PipeBlock pipe = ItemDictionary.Instance != null ? ItemDictionary.Instance.GetPipeInfo(record.blockId) : null;
        if (pipe != null) return SpawnPipe(worldCell, record, pipe);

        // 여러 칸을 차지하는 기계는 <b>덮는 칸 전부</b>가 비어 있어야 세운다. 한 칸만 보면
        // 이미 다른 기계가 있는 자리에 겹쳐 서고, 그 칸의 등록이 덮어써져 앞의 기계를 못 캐게 된다.
        Vector2Int size = WorldMap.FootprintOf(record);
        foreach (Vector2Int cell in WorldMap.Cells(worldCell, size))
            if (loadedMachines.ContainsKey(cell) || loadedCrops.ContainsKey(cell) || loadedPipes.ContainsKey(cell))
                return false;

        EnsurePlaceableContainer();

        GameObject prefab = ItemDictionary.Instance.GetGameObjectFromBlockDictionary(record.blockId);
        if (prefab == null)
        {
            Debug.LogError($"[MapGenerator] placeable 프리팹 '{record.blockId}' 를 찾을 수 없습니다.");
            return false;
        }

        GameObject go = Instantiate(prefab, placeableContainer);
        // 기준점은 왼쪽 아래 칸이고 스프라이트 피벗은 전부 Center 라, 발자국 정중앙으로 반 칸씩 민다.
        // 1×1 이면 보정이 0 이라 예전과 완전히 같은 자리다.
        Vector3 centre = placeableObjectsTilemap.GetCellCenterWorld(new Vector3Int(worldCell.x, worldCell.y, 0));
        go.transform.position = centre + new Vector3((size.x - 1) * 0.5f, (size.y - 1) * 0.5f, 0f);

        MachineInstance inst = go.GetComponent<MachineInstance>();
        if (inst == null) inst = go.AddComponent<MachineInstance>();
        inst.Bind(record, worldCell);   // worldCell = 기준점. 전력 링크·거리 판정이 이걸 본다

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = 120; // 인스턴스에만 설정(공유 프리팹 오염 방지)

        ApplyFootprintCollider(go, size);

        // <b>덮는 칸마다 같은 인스턴스를 등록한다.</b> TryGetMachineAt 이 이 사전 하나뿐이라,
        // 여기서 다 넣어야 어느 칸을 우클릭해도 UI 가 열리고 어느 칸을 캐도 회수되며
        // 파이프가 어느 면에서든 붙는다.
        foreach (Vector2Int cell in WorldMap.Cells(worldCell, size))
            loadedMachines[cell] = inst;

        // 기계가 생기면 옆 파이프가 이쪽으로 붙는 모양으로 바뀐다.
        // ⚠ 칸마다 부르지 않는다 — 위상 버전이 발자국 크기만큼 튀면 경로 캐시가 그만큼 더 버려진다.
        if (PipeNetworkManager.Active != null) PipeNetworkManager.Active.MarkTopologyDirty(worldCell);
        return true;
    }

    /// <summary>
    /// 여러 칸을 차지하는 기계의 콜라이더를 발자국에 맞춘다.
    ///
    /// ⚠ 지금 기계 프리팹의 <c>BoxCollider2D</c> 는 대부분 복붙된 <c>{0.8125, 1.09375}</c> 라,
    /// 손대지 않으면 <b>2×2 기계의 절반을 뚫고 지나간다</b>. 반대로 1×1 기계는 건드리지 않는다 —
    /// 그림이 한 칸보다 살짝 큰 것은 탑다운 오버행이라 콜라이더가 작은 것이 의도다.
    /// 값은 <see cref="SpriteRenderer.sortingOrder"/> 와 같은 규약으로 <b>인스턴스에만</b> 준다.
    /// </summary>
    private static void ApplyFootprintCollider(GameObject go, Vector2Int size)
    {
        if (size.x <= 1 && size.y <= 1) return;

        BoxCollider2D box = go.GetComponent<BoxCollider2D>();
        if (box == null) box = go.AddComponent<BoxCollider2D>();   // tmp_crafter 처럼 아예 없는 프리팹도 있다
        box.size = new Vector2(size.x, size.y);
        box.offset = Vector2.zero;   // 오브젝트가 이미 발자국 중앙에 서 있다
    }

    private void SpawnCrop(Vector2Int worldCell, PlaceableRecord record, CropBlock crop)
    {
        EnsurePlaceableContainer();
        GameObject go = new GameObject(crop.DisplayName);
        go.transform.SetParent(placeableContainer, false);
        go.transform.position = placeableObjectsTilemap.GetCellCenterWorld(new Vector3Int(worldCell.x, worldCell.y, 0));
        CropInstance instance = go.AddComponent<CropInstance>();
        instance.Bind(crop, record, worldCell);
        loadedCrops[worldCell] = instance;
    }



    /// <summary>파이프 한 칸을 등록하고 타일로 그린다(GameObject 를 만들지 않는다).</summary>
    private bool SpawnPipe(Vector2Int worldCell, PlaceableRecord record, PipeBlock block)
    {
        if (loadedPipes.ContainsKey(worldCell)) return false;

        PipeNetworkManager.EnsureCreated(
            placeableObjectsTilemap != null ? placeableObjectsTilemap.transform.parent : null,
            whitePixel, overlayMaterial);

        PipeCell pipe = new PipeCell(worldCell, block, record);
        loadedPipes[worldCell] = pipe;
        if (PipeNetworkManager.Active != null) PipeNetworkManager.Active.OnPipeLoaded(pipe);
        return true;
    }

    public bool TryGetMachineAt(Vector2Int worldCell, out MachineInstance instance)
        => loadedMachines.TryGetValue(worldCell, out instance);

    /// <summary>로드된 청크에 깔려 있는 파이프(월드 셀 → 상태). loadedMachines 와 대칭이다.</summary>
    public bool TryGetPipeAt(Vector2Int worldCell, out PipeCell pipe)
        => loadedPipes.TryGetValue(worldCell, out pipe);

    public bool TryGetCropAt(Vector2Int worldCell, out CropInstance crop)
        => loadedCrops.TryGetValue(worldCell, out crop);


    public IEnumerable<KeyValuePair<Vector2Int, PipeCell>> LoadedPipes => loadedPipes;

    /// <summary>로드된 청크에 스폰돼 있는 기계 전부(월드 셀 → 인스턴스). 전력 범위 검색에 쓴다.</summary>
    public IEnumerable<KeyValuePair<Vector2Int, MachineInstance>> LoadedMachines => loadedMachines;

    /// <summary>
    /// 이 셀이 속한 청크가 지금 로드돼 있는가.
    /// "기계가 없다"와 "아직 안 불러왔다"를 가르는 기준 — 전력 링크를 함부로 지우지 않으려면 필요하다.
    /// </summary>
    public bool IsCellLoaded(Vector2Int worldCell)
        => LoadedChunks.ContainsKey(Chunk.GetChunkId(new Vector3(worldCell.x, worldCell.y, 0f)));

    /// <summary>
    /// 기계를 캔다. 기계 자신과 내부 슬롯(입력·출력·연료)의 아이템을 전부 필드에 떨어뜨리고 제거한다.
    /// 에너지·가스·연소 잔량은 <see cref="PlaceableRecord"/> 가 사라지면서 함께 없어진다.
    ///
    /// UI 가 이 기계를 보고 있다면 호출자가 먼저 닫아야 한다(입력 판별은 PlayerInteraction 이 전담).
    /// </summary>
    public bool RemoveMachineAt(Vector2Int worldCell)
    {
        // ⚠ 가장 먼저 기준점으로 정규화한다. 여러 칸 기계는 <b>덮인 칸을 캐도</b> 지워져야 하는데,
        // 레코드는 기준점에만 있어 정규화하지 않으면 "캘 수 없는 칸" 으로 보인다.
        worldCell = WorldMap.Instance.OriginAt(worldCell);

        PlaceableRecord record = WorldMap.Instance.GetPlaceableAt(worldCell);
        if (record == null) return false;

        // 파이프는 인벤토리 대신 운반 중인 짐을 쏟는다.
        if (loadedPipes.TryGetValue(worldCell, out PipeCell pipe))
        {
            DropParcel(worldCell, pipe);
            DropSelf(worldCell, record);
            ClearMirroredCuts(worldCell, record);

            WorldMap.Instance.RemovePlaceableAt(worldCell);
            loadedPipes.Remove(worldCell);
            if (PipeNetworkManager.Active != null) PipeNetworkManager.Active.OnPipeUnloaded(worldCell);
            return true;
        }

        loadedMachines.TryGetValue(worldCell, out MachineInstance inst);

        // 1) 내부 슬롯을 쏟는다. 개체 데이터를 그대로 넘겨 도구의 재질·내구도가 보존된다.
        if (inst != null && inst.inventory != null)
        {
            DropSlots(worldCell, inst.inventory.inputSlots);
            DropSlots(worldCell, inst.inventory.outputSlots);
            DropSlots(worldCell, inst.inventory.upgradeSlots);   // 빠뜨리면 기계를 캘 때 모듈이 증발한다
            DropSlots(worldCell, inst.inventory.fuelSlots);
        }

        // 2) 기계 자신도 떨어뜨린다(아이템 ID 는 blockName 과 같다는 규약).
        DropSelf(worldCell, record);

        // 3) 레코드와 인스턴스를 없앤다. 덮고 있던 칸의 등록도 <b>전부</b> 지운다 —
        //    남기면 사라진 기계를 가리키는 유령 키가 되어 그 칸에 다시 놓을 수 없다.
        Vector2Int size = WorldMap.FootprintOf(record);
        WorldMap.Instance.RemovePlaceableAt(worldCell);
        foreach (Vector2Int cell in WorldMap.Cells(worldCell, size))
            if (loadedMachines.TryGetValue(cell, out MachineInstance at) && at == inst)
                loadedMachines.Remove(cell);
        if (inst != null) Destroy(inst.gameObject);

        // 기계가 사라지면 옆 파이프의 연결 모양도 바뀐다.
        if (PipeNetworkManager.Active != null) PipeNetworkManager.Active.MarkTopologyDirty(worldCell);
        return true;
    }

    /// <summary>
    /// 캐 낼 파이프가 이웃 쪽에 남겨 둔 "끊김" 표시를 지운다.
    ///
    /// 렌치는 끊을 때 양쪽 레코드에 같은 값을 쓴다. 한쪽이 사라져도 남은 쪽 표시가 살아 있으면,
    /// 같은 자리에 파이프를 새로 깔았을 때 영문 모르게 끊긴 채로 시작하게 된다.
    /// <b>"끊김은 살아 있는 파이프 두 칸 사이에만 있다"</b> 는 불변식을 여기서 지킨다.
    /// </summary>
    private void ClearMirroredCuts(Vector2Int worldCell, PlaceableRecord record)
    {
        for (int d = 0; d < PipeRouter.Directions.Length; d++)
        {
            if (PipeRouter.FaceOf(record, d) != PipeFaceMode.Cut) continue;

            Vector2Int neighbour = worldCell + PipeRouter.Directions[d];
            PlaceableRecord other = WorldMap.Instance != null ? WorldMap.Instance.GetPlaceableAt(neighbour) : null;
            if (other == null) continue;   // 이웃 청크가 아직 없으면 지울 것도 없다

            if (PipeRouter.FaceOf(other, PipeRouter.Opposite(d)) == PipeFaceMode.Cut)
                PipeRouter.SetFace(other, PipeRouter.Opposite(d), PipeFaceMode.Default);
        }
    }

    /// <summary>다 자란 작물을 수확하고 설정된 수확물/씨앗을 떨어뜨린다.</summary>
    public bool HarvestCropAt(Vector2Int worldCell)
    {
        if (!loadedCrops.TryGetValue(worldCell, out CropInstance instance) || instance == null || !instance.IsMature)
            return false;

        CropBlock crop = instance.Crop;
        WorldMap.Instance.RemovePlaceableAt(worldCell);
        loadedCrops.Remove(worldCell);
        Destroy(instance.gameObject);

        if (crop.harvestItem != null) SpawnDrop(worldCell, new ItemStack { item = crop.harvestItem, count = crop.harvestCount });
        if (crop.dropItem != null && crop.seedReturnCount > 0)
            SpawnDrop(worldCell, new ItemStack { item = crop.dropItem, count = crop.seedReturnCount });
        return true;
    }


    /// <summary>배치물 자신을 아이템으로 돌려준다(아이템 ID 는 blockName 과 같다는 규약).</summary>
    private void DropSelf(Vector2Int worldCell, PlaceableRecord record)
    {
        Items item = ItemDictionary.Instance != null ? ItemDictionary.Instance.GetItem(record.blockId) : null;
        if (item != null) SpawnDrop(worldCell, new ItemStack { item = item, count = 1 });
        else Debug.LogWarning($"[MapGenerator] '{record.blockId}' 에 대응하는 아이템이 없어 회수하지 못했습니다.");
    }

    /// <summary>
    /// 파이프가 싣고 있던 짐을 필드에 쏟는다. 짐을 회수할 수 있는 유일한 경로다.
    ///
    /// <b>유체 짐은 그냥 버린다.</b> 필드에 유체를 떨어뜨릴 수단이 없고(드랍은 아이템뿐),
    /// 양동이 없이 유체를 손에 쥘 수도 없다 — 파이프를 캐면 안에 있던 유체가 사라지는 것이 의도다.
    /// </summary>
    private void DropParcel(Vector2Int worldCell, PipeCell pipe)
    {
        if (pipe.parcel == null) return;
        if (pipe.parcel.IsFluid) { pipe.parcel = null; pipe.WriteBack(); return; }
        if (pipe.parcel.count <= 0) return;

        Items item = ItemDictionary.Instance != null ? ItemDictionary.Instance.GetItem(pipe.parcel.itemName) : null;
        if (item != null)
            SpawnDrop(worldCell, new ItemStack { item = item, count = pipe.parcel.count, instance = pipe.parcel.instance });

        pipe.parcel = null;
        pipe.WriteBack();
    }

    private void DropSlots(Vector2Int worldCell, List<ItemStack> slots)
    {
        if (slots == null) return;

        foreach (ItemStack stack in slots)
        {
            if (stack == null || stack.item == null || stack.count <= 0) continue;
            SpawnDrop(worldCell, stack);
            stack.Clear();
        }
    }

    /// <summary>로드된 모든 기계의 인벤토리를 레코드로 동기화(저장 직전 호출).</summary>
    public void FlushAll()
    {
        // ⚠ 여러 칸 기계는 덮는 칸마다 같은 인스턴스가 등록돼 있다. 값만 순회하면 한 기계를
        // 칸 수만큼 Flush 하므로, <b>기준점 칸에서만</b> 한 번 부른다.
        foreach (var kvp in loadedMachines)
            if (kvp.Value != null && kvp.Value.worldCell == kvp.Key) kvp.Value.Flush();

        // 파이프가 싣고 있는 짐도 레코드로 옮긴다 — 빼먹으면 저장할 때 화물이 사라진다.
        if (PipeNetworkManager.Active != null) PipeNetworkManager.Active.FlushAll();
    }
    public void UnLoadChunk(Vector2Int id, Chunk chunk)
    {
        int size = WorldMap.ChunkSize;

        if (ChunkShadowCasters.Active != null) ChunkShadowCasters.Active.Release(id);

        // 이 청크의 기계·파이프를 레코드로 동기화 후 디스폰
        foreach (var kvp in chunk.Placeables)
        {
            Vector2Int local = kvp.Key;
            Vector2Int worldCell = new Vector2Int(id.x * size + local.x, id.y * size + local.y);
            if (loadedMachines.TryGetValue(worldCell, out MachineInstance inst))
            {
                if (inst != null) { inst.Flush(); Destroy(inst.gameObject); }
                // 덮고 있던 칸의 등록도 함께 지운다. chunk.Placeables 는 기준점만 담으므로
                // 이웃 청크를 언로드할 때 남의 기계를 잘못 지울 일은 없다.
                foreach (Vector2Int cell in WorldMap.CellsOf(worldCell, kvp.Value))
                    if (loadedMachines.TryGetValue(cell, out MachineInstance at) && at == inst)
                        loadedMachines.Remove(cell);
            }
            else if (loadedPipes.ContainsKey(worldCell))
            {
                loadedPipes.Remove(worldCell);
                if (PipeNetworkManager.Active != null) PipeNetworkManager.Active.OnPipeUnloaded(worldCell);
            }
            else if (loadedCrops.TryGetValue(worldCell, out CropInstance crop))
            {
                if (crop != null) Destroy(crop.gameObject);
                loadedCrops.Remove(worldCell);
            }
        }

        // 드랍은 표시 오브젝트만 없앤다. 레코드는 청크에 남아 다시 오면 그대로 보인다.
        foreach (DropRecord drop in chunk.Drops)
        {
            if (!loadedDrops.TryGetValue(drop, out DroppedItem obj)) continue;
            if (obj != null) Destroy(obj.gameObject);
            loadedDrops.Remove(drop);
        }

        for (int ty = 0; ty < size; ty++){
            for (int tx = 0; tx < size; tx++)
            {
                var pos = new Vector3Int(id.x * size + tx, id.y * size + ty, 0);
                string tileId = chunk.GetTile(tx, ty);
                if (Chunk.IsWall(tileId))
                    blocksTilemap.SetTile(pos, null);
                else
                    floorTilemap.SetTile(pos, null);
            }
        }

        for (int ty = 0; ty < size; ty++)
        {
            for (int tx = 0; tx < size; tx++)
            {
                var pos = new Vector3Int(id.x * size + tx, id.y * size + ty, 0);
                TilemapTextureLoader.Instance.ClearTileTexture(new Vector2Int(pos.x, pos.y));
            }
        }
    }

    /// <summary>
    /// 한 칸의 지형이 바뀐 뒤(채굴·설치) 데이터 타일맵(blocks/floor)과
    /// 벽 오토타일링(자신 + 8방향 이웃)을 즉시 갱신합니다.
    /// 무엇으로 바뀌었는지는 <see cref="WorldMap"/> 이 이미 정해 뒀으므로 여기서는 읽기만 합니다.
    /// </summary>
    public void RefreshTile(Vector2Int worldPos)
    {
        Vector3Int pos = (Vector3Int)worldPos;
        string tileId = WorldMap.Instance.GetTileId(worldPos);

        if (Chunk.IsWall(tileId))
        {
            floorTilemap.SetTile(pos, null);
            blocksTilemap.SetTile(pos, LoadTile(tileId));
        }
        else
        {
            blocksTilemap.SetTile(pos, null);
            floorTilemap.SetTile(pos, LoadTile(tileId));
        }

        // 벽이 생기거나 사라졌으니 이 청크의 그림자 사각형을 다시 만든다.
        // 사각형은 청크 안에서만 묶이므로 이웃 청크는 건드릴 필요가 없다.
        if (ChunkShadowCasters.Active != null)
        {
            Vector2Int chunkId = Chunk.GetChunkId(new Vector3(worldPos.x, worldPos.y, 0f));
            if (LoadedChunks.TryGetValue(chunkId, out Chunk owner))
                ChunkShadowCasters.Active.Build(chunkId, owner);
        }

        // 이 칸과 한 칸 위에 남아 있던 예전 벽 텍스처를 먼저 지운다.
        // LoadWallTexture는 블록이 있을 때만 새로 그릴 뿐, 없어진 블록의 흔적을 지우지는 않기 때문.
        // (ClearTileTexture가 floorTextureTilemap도 같이 지우므로, 바닥 텍스처는 반드시 이 다음에 그려야 함)
        TilemapTextureLoader.Instance.ClearTileTexture(worldPos);
        TilemapTextureLoader.Instance.ClearTileTexture(worldPos + Vector2Int.up);

        // 지운 두 자리 중 실제로 바닥 데이터가 있는 칸은 바닥 텍스처를 다시 그려준다.
        // (worldPos + up이 원래 바닥이었다면, 캔 블록의 "윗면" 텍스처를 지우면서
        //  그 자리의 바닥 텍스처까지 같이 지워졌기 때문에 복원이 필요함)
        if (floorTilemap.GetTile(pos) != null)
            TilemapTextureLoader.Instance.LoadFloorTexture(worldPos);

        Vector3Int upPos = pos + Vector3Int.up;
        if (floorTilemap.GetTile(upPos) != null)
            TilemapTextureLoader.Instance.LoadFloorTexture(worldPos + Vector2Int.up);

        // 영향받는 범위(자신 + 8방향 이웃)만 갱신 - LoadChunksAround와 동일하게
        // Y가 큰(위쪽) 칸부터 순서대로 다시 그려야 아래 블록의 윗면이 위 블록의 앞면을 올바르게 덮어쓴다.
        List<Vector2Int> affected = new() { worldPos };
        foreach (Vector2Int dir in TileAtlasManager.All8Directions)
        {
            affected.Add(worldPos + dir);
        }
        affected.Sort((a, b) => b.y.CompareTo(a.y));

        foreach (Vector2Int p in affected)
        {
            TilemapTextureLoader.Instance.LoadWallTexture(p);
        }
    }

    public IEnumerable<Vector2Int> GetFloorTilePositions()
    {
        foreach (var chunkId in LoadedChunks.Keys)
        {
            for (int tx = 0; tx < WorldMap.ChunkSize; tx++)
            {
                for (int ty = 0; ty < WorldMap.ChunkSize; ty++)
                {
                    Vector2Int worldPos = new Vector2Int(chunkId.x * WorldMap.ChunkSize + tx, chunkId.y * WorldMap.ChunkSize + ty);
                    if (floorTilemap.GetTile((Vector3Int)worldPos) != null)
                        yield return worldPos;
                }
            }
        }
    }

    

}
