using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{
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

        for (int x = playerChunk.x - renderDistance; x <= playerChunk.x + renderDistance - 1; x++)
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

}
