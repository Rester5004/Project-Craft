using UnityEngine;
using UnityEngine.Tilemaps;

public class MapGenerator : MonoBehaviour
{
    [SerializeField] Tilemap blocksTilemap;
    [SerializeField] Tilemap floorTilemap;
    [SerializeField] Transform player;

    WorldMap worldMap;
    TileBase blockTile;
    TileBase floorTile;

    Vector2Int lastChunk = Vector2Int.zero;
    private bool isFirstUpdate = true;

    void Start()
    {
        blockTile = Resources.Load<TileBase>("blocktest");
        floorTile = Resources.Load<TileBase>("floortest");

        worldMap = new WorldMap();
        UpdateChunks();
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
        for (int x = playerChunk.x - 2; x <= playerChunk.x + 1; x++)
            for (int y = playerChunk.y - 2; y <= playerChunk.y + 1; y++)
                RenderChunk(new Vector2Int(x, y), worldMap.GetOrCreateChunk(x, y));
    }

    void RenderChunk(Vector2Int id, Chunk chunk)
    {
        int size = WorldMap.ChunkSize;
        for (int ty = 0; ty < size; ty++)
            for (int tx = 0; tx < size; tx++)
            {
                var pos = new Vector3Int(id.x * size + tx, id.y * size + ty, 0);
                floorTilemap.SetTile(pos, floorTile);
                blocksTilemap.SetTile(pos, chunk.GetTile(tx, ty) == 1 ? blockTile : null);
            }
    }
}
