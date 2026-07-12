using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Chunk
{
    private int[,] tiles;

    public Chunk()
    {
        tiles = new int[16, 16];
    }

    //타일맵 transform이 (0,0,0)이고, 셀 사이즈가 1일때만 작동하는 함수. 
    public static Vector2Int GetChunkId(Vector3 pos)
    {
        return new Vector2Int((int)((Mathf.FloorToInt(pos.x) & ~15) / 16), (int)(((Mathf.FloorToInt(pos.y)) & ~15) / 16));
    }
    public static Vector2Int GetLocalCellPositionInChunk(Vector3 pos)
    {
        int localX = Mathf.FloorToInt(pos.x) & 15;
        int localY = Mathf.FloorToInt(pos.y) & 15;
        return new Vector2Int(localX, localY);
    }
    public int GetTile(int x, int y) => tiles[y, x];
    public void SetTile(int x, int y, int tileID) => tiles[y, x] = tileID;

    public void Save(BinaryWriter writer)
    {
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                writer.Write(tiles[y, x]);
    }

    public static Chunk Load(BinaryReader reader)
    {
        Chunk chunk = new();
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                chunk.tiles[y, x] = reader.ReadInt32();
        return chunk;
    }
}

public class WorldMap : Singleton<WorldMap>
{
    public const int ChunkSize = 16;
    public static string DefaultSavePath =>
        Path.Combine(Application.persistentDataPath, "worldmap.dat");

    public static string DefaultWorldmapPath =>
        Path.Combine(Application.streamingAssetsPath, "DefaultWorldmap.dat");

    private Dictionary<Vector2Int, Chunk> chunks;
    private string savePath;
    private bool isLoaded;

    protected override void Awake()
    {
        base.Awake();

        savePath = DefaultSavePath;
        chunks = new();
        if (File.Exists(savePath))
            Load(savePath);
        else if (File.Exists(DefaultWorldmapPath))
        {
            File.Copy(DefaultWorldmapPath, savePath);
            Load(savePath);
        }
        else
            isLoaded = true;
    }

    protected override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
        Save();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            Save();
    }

    public Chunk GetOrCreateChunk(Vector2Int chunkId)
    {
        if (!chunks.TryGetValue(chunkId, out Chunk chunk))
        {
            chunk = GenerateChunk(chunkId);
            chunks[chunkId] = chunk;
        }
        return chunk;
    }
    public bool Mining(Vector2Int chunkId, Vector2Int cellPos)
    {
        Chunk chunk = GetOrCreateChunk(chunkId);
        if(chunk.GetTile(cellPos.x, cellPos.y) > 9999)
        {
            chunk.SetTile(cellPos.x, cellPos.y, 0);
            return true;
        }
        return false;
    }

    Chunk GenerateChunk(Vector2Int chunkId) //추후 청크 id에 따라 다른 blockid를 사용하게 수정예정
    {
        Chunk chunk = new();
        for (int ty = 0; ty < ChunkSize; ty++)
            for (int tx = 0; tx < ChunkSize; tx++)
            {
                int wx = chunkId.x * ChunkSize + tx;
                int wy = chunkId.y * ChunkSize + ty;
                bool inSpawn = wx >= -3 && wx <= 2 && wy >= -2 && wy <= 3;
                chunk.SetTile(tx, ty, inSpawn ? 0 : 10000);
            }
        return chunk;
    }

    public void Save() => Save(savePath);

    public void Save(string path)
    {
        if (!isLoaded) return;
        using BinaryWriter writer = new(File.Open(path, FileMode.Create));
        writer.Write(chunks.Count);
        foreach (var kvp in chunks)
        {
            writer.Write(kvp.Key.x);
            writer.Write(kvp.Key.y);
            kvp.Value.Save(writer);
        }
    }

    public void Load(string path)
    {
        chunks.Clear();
        try
        {
            using BinaryReader reader = new(File.Open(path, FileMode.Open));
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                int cx = reader.ReadInt32();
                int cy = reader.ReadInt32();
                chunks[new Vector2Int(cx, cy)] = Chunk.Load(reader);
            }
            isLoaded = true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[WorldMap] 세이브 파일 로드 실패, 새로 생성합니다: {e.Message}");
            chunks.Clear();
            File.Delete(path);
            isLoaded = true;
        }
    }
}
