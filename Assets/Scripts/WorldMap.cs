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

    public static Vector2Int GetPlayerChunk(GameObject player)
    {
        Vector3 pos = player != null ? player.transform.position : Vector3.zero;
        int pcx = Mathf.FloorToInt(pos.x / WorldMap.ChunkSize);
        int pcy = Mathf.FloorToInt(pos.y / WorldMap.ChunkSize);
        return new Vector2Int(pcx, pcy);
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

public class WorldMap
{
    public const int ChunkSize = 16;
    public static string DefaultSavePath =>
        Path.Combine(Application.persistentDataPath, "worldmap.dat");

    private readonly Dictionary<Vector2Int, Chunk> chunks;
    private readonly string savePath;

    public WorldMap(string path = null)
    {
        savePath = path ?? DefaultSavePath;
        chunks = new();
        if (File.Exists(savePath))
            Load(savePath);
    }

    public Chunk GetOrCreateChunk(int cx, int cy)
    {
        var key = new Vector2Int(cx, cy);
        if (!chunks.TryGetValue(key, out Chunk chunk))
        {
            chunk = GenerateChunk(cx, cy);
            chunks[key] = chunk;
        }
        return chunk;
    }

    Chunk GenerateChunk(int cx, int cy)
    {
        Chunk chunk = new();
        for (int ty = 0; ty < ChunkSize; ty++)
            for (int tx = 0; tx < ChunkSize; tx++)
            {
                int wx = cx * ChunkSize + tx;
                int wy = cy * ChunkSize + ty;
                bool inSpawn = wx >= -3 && wx <= 2 && wy >= -2 && wy <= 3;
                chunk.SetTile(tx, ty, inSpawn ? 0 : 1);
            }
        return chunk;
    }

    public void Save() => Save(savePath);

    public void Save(string path)
    {
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
        using BinaryReader reader = new(File.Open(path, FileMode.Open));
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            int cx = reader.ReadInt32();
            int cy = reader.ReadInt32();
            chunks[new Vector2Int(cx, cy)] = Chunk.Load(reader);
        }
    }
}
