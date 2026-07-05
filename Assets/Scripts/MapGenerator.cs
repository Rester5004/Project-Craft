using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{

    [Header("타일맵")]
    [SerializeField] Tilemap blocksTilemap;
    [SerializeField] Tilemap floorTilemap;
    [SerializeField] Transform player;
    [SerializeField] int renderDistance = 2;


    WorldMap worldMap;
    Vector2Int lastChunk = Vector2Int.zero;
    private bool isFirstUpdate = true;
    private Dictionary<Vector2Int,Chunk> LoadedChunks = new Dictionary<Vector2Int,Chunk>();


    void Start()
    {
        worldMap = new WorldMap();
        UpdateChunks();
    }
    private TileBase LoadTile(int Tileid) //floor : 0~9999 , blocks :10000 ~ 
    {
        TileBase tile = Resources.Load<TileBase>(Tileid.ToString());
        if (tile == null)
            Debug.LogError($"Tile at id '{Tileid}' not found.");
        return tile;
    }
    void Update()
    {
        UpdateChunks();
    }

    void UpdateChunks()
    {
        Vector2Int playerChunk = Chunk.GetPlayerChunk(player.gameObject);

        if (playerChunk == lastChunk && !isFirstUpdate)
            return;

        lastChunk = playerChunk;
        isFirstUpdate = false;

        LoadChunksAround(playerChunk);
        worldMap.Save();
    }

    void LoadChunksAround(Vector2Int playerChunk)
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
                    Chunk chunk = worldMap.GetOrCreateChunk(id);
                    RenderChunk(id, chunk);
                    LoadedChunks[id] = chunk;
                }
            }
        }

        // 2. 바닥 기본 텍스처 일괄 로드 (전체 바닥 영역 순회)
        foreach (var pos in GetFloorTilePositions())
        {
            TilemapTextureLoader.Instance.LoadFloorTexture(pos);
        }

        // 3. [확인] 블록 데이터가 존재하는 좌표만 순회하며 벽(윗면 + 정면) 처리
        foreach (var pos in GetWallTilePositions())
        {
            TilemapTextureLoader.Instance.LoadWallTexture(pos);
        }
    }

    void RenderChunk(Vector2Int id, Chunk chunk)
    {
        int size = WorldMap.ChunkSize;
        for (int ty = 0; ty < size; ty++){
            for (int tx = 0; tx < size; tx++)
            {
                var pos = new Vector3Int(id.x * size + tx, id.y * size + ty, 0);
                int tileId = chunk.GetTile(tx, ty);
                if(tileId <= 9999)
                    floorTilemap.SetTile(pos, LoadTile(tileId));
                else
                    blocksTilemap.SetTile(pos, LoadTile(tileId));

                
            }
        }
    }
    void UnLoadChunk(Vector2Int id, Chunk chunk)
    {
        int size = WorldMap.ChunkSize;
        for (int ty = 0; ty < size; ty++){
            for (int tx = 0; tx < size; tx++)
            {
                var pos = new Vector3Int(id.x * size + tx, id.y * size + ty, 0);
                int tileId = chunk.GetTile(tx, ty);
                if(tileId <= 9999)
                    floorTilemap.SetTile(pos, null);
                else
                    blocksTilemap.SetTile(pos, null);

            }
        }
    }

    public IEnumerable<Vector2Int> GetFloorTilePositions()
    {
        BoundsInt bounds = floorTilemap.cellBounds;
        TileBase[] allTiles = floorTilemap.GetTilesBlock(bounds);
        int sizeX = bounds.size.x;
        int sizeY = bounds.size.y;
        int sizeZ = bounds.size.z;
        for (int z = 0; z < sizeZ; z++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    int index = x + (y * sizeX) + (z * sizeX * sizeY);
                    if (allTiles[index] != null)
                    {
                        int worldX = bounds.xMin + x;
                        int worldY = bounds.yMin + y;
                        yield return new Vector2Int(worldX, worldY);
                    }
                }
            }
        }
    }

    public IEnumerable<Vector2Int> GetWallTilePositions()
    {
        // 1. 타일맵의 현재 사각형 영역 영역(Bounds) 확보
        BoundsInt bounds = blocksTilemap.cellBounds;

        // 2. [현재 작성하신 부분] 모든 타일 배열로 통째로 긁어오기
        TileBase[] allTiles = blocksTilemap.GetTilesBlock(bounds);

        // 3. 유니티의 GetTilesBlock 내부 정렬 순서(X -> Y -> Z)대로 3중 루프를 돕니다.
        int sizeX = bounds.size.x;
        int sizeY = bounds.size.y;
        int sizeZ = bounds.size.z;

        for (int z = 0; z < sizeZ; z++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    // 유니티 공식: 3차원 바둑판을 1차원 배열로 펼쳤을 때의 인덱스 계산
                    int index = x + (y * sizeX) + (z * sizeX * sizeY);

                    // 현재 인덱스의 타일이 null이 아니라면
                    if (allTiles[index] != null)
                    {
                        // 원본 타일맵의 실제 월드 그리드 좌표(Vector2Int)를 역산하여 반환
                        int worldX = bounds.xMin + x;
                        int worldY = bounds.yMin + y;

                        yield return new Vector2Int(worldX, worldY);
                    }
                }
            }
        }
    }

}
