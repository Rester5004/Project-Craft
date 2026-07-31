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
        // 파이프 타일맵은 씬에 배선하지 않고 여기서 만든다(씬 파일을 건드리지 않기 위해).
        PipeNetworkManager.EnsureCreated(placeableObjectsTilemap != null ? placeableObjectsTilemap.transform.parent : null);
        if (WorldMap.Instance != null)
            WorldMap.Instance.OnBeforeSave += FlushAll;
        UpdateChunks();
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
        if (WorldMap.Instance != null)
            WorldMap.Instance.OnBeforeSave -= FlushAll;
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
        for (int x = playerChunk.x - renderDistance; x <= playerChunk.x + renderDistance - 1; x++)
        {
            for (int y = playerChunk.y - renderDistance; y <= playerChunk.y + renderDistance - 1; y++)
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
    }

    // ── 필드 드랍 ──────────────────────────────────────────────────────
    private void SpawnDropObject(DropRecord record, Chunk chunk)
    {
        if (record == null || loadedDrops.ContainsKey(record)) return;
        EnsureDropContainer();

        DroppedItem drop = DroppedItem.Create(dropContainer, record, chunk);
        if (drop != null) loadedDrops[record] = drop;
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
    public void SpawnPlaceableAt(Vector2Int worldCell, PlaceableRecord record) => SpawnPlaceable(worldCell, record);

    private void SpawnPlaceable(Vector2Int worldCell, PlaceableRecord record)
    {
        if (record == null || loadedMachines.ContainsKey(worldCell)) return;

        // 파이프는 프리팹을 세우지 않고 타일맵에 그린다. 아래 기계 경로를 타면
        // MachineInstance 가 붙어 "유령 기계"가 되므로 반드시 여기서 갈라야 한다.
        PipeBlock pipe = ItemDictionary.Instance != null ? ItemDictionary.Instance.GetPipeInfo(record.blockId) : null;
        if (pipe != null) { SpawnPipe(worldCell, record, pipe); return; }

        EnsurePlaceableContainer();

        GameObject prefab = ItemDictionary.Instance.GetGameObjectFromBlockDictionary(record.blockId);
        if (prefab == null)
        {
            Debug.LogError($"[MapGenerator] placeable 프리팹 '{record.blockId}' 를 찾을 수 없습니다.");
            return;
        }

        GameObject go = Instantiate(prefab, placeableContainer);
        go.transform.position = placeableObjectsTilemap.GetCellCenterWorld(new Vector3Int(worldCell.x, worldCell.y, 0));

        MachineInstance inst = go.GetComponent<MachineInstance>();
        if (inst == null) inst = go.AddComponent<MachineInstance>();
        inst.Bind(record, worldCell);

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = 2; // 인스턴스에만 설정(공유 프리팹 오염 방지)

        loadedMachines[worldCell] = inst;

        // 기계가 생기면 옆 파이프가 이쪽으로 붙는 모양으로 바뀐다.
        if (PipeNetworkManager.Active != null) PipeNetworkManager.Active.MarkTopologyDirty(worldCell);
    }

    /// <summary>파이프 한 칸을 등록하고 타일로 그린다(GameObject 를 만들지 않는다).</summary>
    private void SpawnPipe(Vector2Int worldCell, PlaceableRecord record, PipeBlock block)
    {
        if (loadedPipes.ContainsKey(worldCell)) return;

        PipeNetworkManager.EnsureCreated(placeableObjectsTilemap != null ? placeableObjectsTilemap.transform.parent : null);

        PipeCell pipe = new PipeCell(worldCell, block, record);
        loadedPipes[worldCell] = pipe;
        if (PipeNetworkManager.Active != null) PipeNetworkManager.Active.OnPipeLoaded(pipe);
    }

    public bool TryGetMachineAt(Vector2Int worldCell, out MachineInstance instance)
        => loadedMachines.TryGetValue(worldCell, out instance);

    /// <summary>로드된 청크에 깔려 있는 파이프(월드 셀 → 상태). loadedMachines 와 대칭이다.</summary>
    public bool TryGetPipeAt(Vector2Int worldCell, out PipeCell pipe)
        => loadedPipes.TryGetValue(worldCell, out pipe);

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
        Vector3 cellPos = new Vector3(worldCell.x, worldCell.y, 0f);
        Vector2Int chunkId = Chunk.GetChunkId(cellPos);
        Vector2Int local = Chunk.GetLocalCellPositionInChunk(cellPos);

        Chunk chunk = WorldMap.Instance.GetOrCreateChunk(chunkId);
        PlaceableRecord record = chunk.GetPlaceable(local);
        if (record == null) return false;

        // 파이프는 인벤토리 대신 운반 중인 짐을 쏟는다.
        if (loadedPipes.TryGetValue(worldCell, out PipeCell pipe))
        {
            DropParcel(worldCell, pipe);
            DropSelf(worldCell, record);

            chunk.RemovePlaceable(local);
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
            DropSlots(worldCell, inst.inventory.fuelSlots);
        }

        // 2) 기계 자신도 떨어뜨린다(아이템 ID 는 blockName 과 같다는 규약).
        DropSelf(worldCell, record);

        // 3) 레코드와 인스턴스를 없앤다.
        chunk.RemovePlaceable(local);
        loadedMachines.Remove(worldCell);
        if (inst != null) Destroy(inst.gameObject);

        // 기계가 사라지면 옆 파이프의 연결 모양도 바뀐다.
        if (PipeNetworkManager.Active != null) PipeNetworkManager.Active.MarkTopologyDirty(worldCell);
        return true;
    }

    /// <summary>배치물 자신을 아이템으로 돌려준다(아이템 ID 는 blockName 과 같다는 규약).</summary>
    private void DropSelf(Vector2Int worldCell, PlaceableRecord record)
    {
        Items item = ItemDictionary.Instance != null ? ItemDictionary.Instance.GetItem(record.blockId) : null;
        if (item != null) SpawnDrop(worldCell, new ItemStack { item = item, count = 1 });
        else Debug.LogWarning($"[MapGenerator] '{record.blockId}' 에 대응하는 아이템이 없어 회수하지 못했습니다.");
    }

    /// <summary>파이프가 싣고 있던 짐을 필드에 쏟는다. 짐을 회수할 수 있는 유일한 경로다.</summary>
    private void DropParcel(Vector2Int worldCell, PipeCell pipe)
    {
        if (pipe.parcel == null || pipe.parcel.count <= 0) return;

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
        foreach (MachineInstance inst in loadedMachines.Values)
            if (inst != null) inst.Flush();

        // 파이프가 싣고 있는 짐도 레코드로 옮긴다 — 빼먹으면 저장할 때 화물이 사라진다.
        if (PipeNetworkManager.Active != null) PipeNetworkManager.Active.FlushAll();
    }
    public void UnLoadChunk(Vector2Int id, Chunk chunk)
    {
        int size = WorldMap.ChunkSize;

        // 이 청크의 기계·파이프를 레코드로 동기화 후 디스폰
        foreach (var kvp in chunk.Placeables)
        {
            Vector2Int local = kvp.Key;
            Vector2Int worldCell = new Vector2Int(id.x * size + local.x, id.y * size + local.y);
            if (loadedMachines.TryGetValue(worldCell, out MachineInstance inst))
            {
                if (inst != null) { inst.Flush(); Destroy(inst.gameObject); }
                loadedMachines.Remove(worldCell);
            }
            else if (loadedPipes.ContainsKey(worldCell))
            {
                loadedPipes.Remove(worldCell);
                if (PipeNetworkManager.Active != null) PipeNetworkManager.Active.OnPipeUnloaded(worldCell);
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
