using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{

    [Header("타일맵")]
    [SerializeField] Tilemap blocksTilemap;
    [SerializeField] Tilemap floorTilemap;
    [SerializeField] Tilemap placeableObjectsTilemap;
    [SerializeField] Transform player;
    [SerializeField] int renderDistance = 2;


    Vector2Int lastChunk = Vector2Int.zero;
    private bool isFirstUpdate = true;
    private Dictionary<Vector2Int,Chunk> LoadedChunks = new Dictionary<Vector2Int,Chunk>();


    void Start()
    {
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
        Vector2Int playerChunk = Chunk.GetChunkId(player.gameObject.transform.position);

        if (playerChunk == lastChunk && !isFirstUpdate)
            return;

        lastChunk = playerChunk;
        isFirstUpdate = false;

        LoadChunksAround(playerChunk);
        WorldMap.Instance.Save();
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
                int tileId = chunk.GetTile(tx, ty);
                if(tileId <= 9999){
                    blocksTilemap.SetTile(pos, null);
                    floorTilemap.SetTile(pos, LoadTile(tileId));
                }
                else{
                    floorTilemap.SetTile(pos, null);
                    blocksTilemap.SetTile(pos, LoadTile(tileId));       
                }
            }
        }
    }
    public void UnLoadChunk(Vector2Int id, Chunk chunk)
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
    /// 채굴로 블록이 제거된 위치의 데이터 타일맵(blocks/floor)과
    /// 벽 오토타일링(자신 + 8방향 이웃)을 즉시 갱신합니다.
    /// </summary>
    public void RefreshMinedTile(Vector2Int worldPos)
    {
        Vector3Int pos = (Vector3Int)worldPos;

        blocksTilemap.SetTile(pos, null);
        floorTilemap.SetTile(pos, LoadTile(0));

        // 캔 블록이 자신(앞면)과 한 칸 위(윗면)에 그려뒀던 예전 벽 텍스처를 먼저 지운다.
        // LoadWallTexture는 블록이 있을 때만 새로 그릴 뿐, 없어진 블록의 흔적을 지우지는 않기 때문.
        TilemapTextureLoader.Instance.ClearTileTexture(worldPos);
        TilemapTextureLoader.Instance.ClearTileTexture(worldPos + Vector2Int.up);

        RefreshAllTileTextures();
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

    

}
