using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 배치된 placeable(기계) 하나의 영속 데이터. blockId 는 머신 타입(=Items.itemName),
/// 입력/출력 슬롯 인벤토리를 분리해 (name, count) 배열로 보관한다(빈 슬롯은 name="").
/// 배열 크기(슬롯 개수)는 MachineInstance.WriteBack 시 기계 설정에 따라 확정된다.
/// </summary>
public class PlaceableRecord
{
    public string blockId;
    public string[] inputItemNames;
    public int[] inputCounts;
    public string[] outputItemNames;
    public int[] outputCounts;

    // 슬롯별 개체 데이터(커스텀 도구의 재질·내구도 등). 없으면 null.
    // 메모리에는 객체 그대로 두고, 디스크에 쓸 때만 ItemInstanceSerializer 로 바이트가 된다.
    public ItemInstance[] inputInstances;
    public ItemInstance[] outputInstances;

    // 연료 슬롯(화로 등). 연료를 쓰지 않는 기계는 길이 0.
    public string[] fuelItemNames;
    public int[] fuelCounts;
    public ItemInstance[] fuelInstances;

    /// <summary>지금 타고 있는 연료의 남은 에너지와 그 연료의 총량(진행 표시용).</summary>
    public float burnRemaining;
    public float burnTotal;

    public PlaceableRecord() { }

    public PlaceableRecord(string blockId)
    {
        this.blockId = blockId;
        inputItemNames = new string[0];
        inputCounts = new int[0];
        outputItemNames = new string[0];
        outputCounts = new int[0];
        inputInstances = new ItemInstance[0];
        outputInstances = new ItemInstance[0];
        fuelItemNames = new string[0];
        fuelCounts = new int[0];
        fuelInstances = new ItemInstance[0];
    }
}

/// <summary>
/// 필드에 떨어져 있는 아이템 하나. 청크에 저장되므로 청크를 벗어났다 돌아와도,
/// 게임을 껐다 켜도 그대로 남아 있다.
/// </summary>
public class DropRecord
{
    public float x, y;              // 월드 좌표(셀 중심에서 살짝 흩뿌린 자리)
    public string itemName;
    public int count;
    public ItemInstance instance;   // 커스텀 도구의 재질·내구도

    public Vector2 Position => new Vector2(x, y);
}

public class Chunk
{
    private string[,] tiles;
    // 지역 셀 좌표(0~15) → placeable 레코드
    private readonly Dictionary<Vector2Int, PlaceableRecord> placeables = new();
    // 이 청크 안에 떨어져 있는 아이템들
    private readonly List<DropRecord> drops = new();

    public Chunk()
    {
        tiles = new string[16, 16];
    }

    public static bool IsWall(string tileId)  => tileId != null && tileId.StartsWith("wall:");
    public static bool IsFloor(string tileId) => tileId != null && tileId.StartsWith("floor:");

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
    public string GetTile(int x, int y) => tiles[y, x];
    public void SetTile(int x, int y, string tileId) => tiles[y, x] = tileId;

    // ── placeable 접근자 ────────────────────────────────────────────────
    public PlaceableRecord GetPlaceable(Vector2Int local)
        => placeables.TryGetValue(local, out PlaceableRecord r) ? r : null;
    public void SetPlaceable(Vector2Int local, PlaceableRecord record) => placeables[local] = record;
    public void RemovePlaceable(Vector2Int local) => placeables.Remove(local);
    public IEnumerable<KeyValuePair<Vector2Int, PlaceableRecord>> Placeables => placeables;

    // ── 드랍 접근자 ─────────────────────────────────────────────────────
    public IReadOnlyList<DropRecord> Drops => drops;
    public void AddDrop(DropRecord drop) { if (drop != null) drops.Add(drop); }
    public void RemoveDrop(DropRecord drop) { if (drop != null) drops.Remove(drop); }

    public void Save(BinaryWriter writer)
    {
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                writer.Write(tiles[y, x] ?? "");

        writer.Write(placeables.Count);
        foreach (KeyValuePair<Vector2Int, PlaceableRecord> kvp in placeables)
        {
            writer.Write(kvp.Key.x);
            writer.Write(kvp.Key.y);
            PlaceableRecord rec = kvp.Value;
            writer.Write(rec.blockId ?? "");
            WriteSlotArray(writer, rec.inputItemNames, rec.inputCounts, rec.inputInstances);
            WriteSlotArray(writer, rec.outputItemNames, rec.outputCounts, rec.outputInstances);
            WriteSlotArray(writer, rec.fuelItemNames, rec.fuelCounts, rec.fuelInstances);
            writer.Write(rec.burnRemaining);
            writer.Write(rec.burnTotal);
        }

        writer.Write(drops.Count);
        foreach (DropRecord drop in drops)
        {
            writer.Write(drop.x);
            writer.Write(drop.y);
            writer.Write(drop.itemName ?? "");
            writer.Write(drop.count);
            ItemInstanceSerializer.Write(writer, drop.instance);
        }
    }

    private static void WriteSlotArray(BinaryWriter writer, string[] names, int[] counts, ItemInstance[] instances)
    {
        int cap = names != null ? names.Length : 0;
        writer.Write(cap);
        for (int i = 0; i < cap; i++)
        {
            writer.Write(names[i] ?? "");
            writer.Write(counts[i]);
            ItemInstanceSerializer.Write(writer, instances != null && i < instances.Length ? instances[i] : null);
        }
    }

    private static void ReadSlotArray(BinaryReader reader, int version,
        out string[] names, out int[] counts, out ItemInstance[] instances)
    {
        int cap = reader.ReadInt32();
        names = new string[cap];
        counts = new int[cap];
        instances = new ItemInstance[cap];
        for (int i = 0; i < cap; i++)
        {
            names[i] = reader.ReadString();
            counts[i] = reader.ReadInt32();
            if (version >= 4) instances[i] = ItemInstanceSerializer.Read(reader);
        }
    }

    public static Chunk Load(BinaryReader reader, int version)
    {
        Chunk chunk = new();
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                chunk.tiles[y, x] = reader.ReadString();

        int placeableCount = reader.ReadInt32();
        for (int p = 0; p < placeableCount; p++)
        {
            int lx = reader.ReadInt32();
            int ly = reader.ReadInt32();
            PlaceableRecord rec = new() { blockId = reader.ReadString() };
            ReadSlotArray(reader, version, out rec.inputItemNames, out rec.inputCounts, out rec.inputInstances);
            ReadSlotArray(reader, version, out rec.outputItemNames, out rec.outputCounts, out rec.outputInstances);

            if (version >= 5)
            {
                ReadSlotArray(reader, version, out rec.fuelItemNames, out rec.fuelCounts, out rec.fuelInstances);
                rec.burnRemaining = reader.ReadSingle();
                rec.burnTotal = reader.ReadSingle();
            }
            else
            {
                rec.fuelItemNames = new string[0];
                rec.fuelCounts = new int[0];
                rec.fuelInstances = new ItemInstance[0];
            }

            chunk.placeables[new Vector2Int(lx, ly)] = rec;
        }

        if (version >= 6)
        {
            int dropCount = reader.ReadInt32();
            for (int d = 0; d < dropCount; d++)
            {
                DropRecord drop = new()
                {
                    x = reader.ReadSingle(),
                    y = reader.ReadSingle(),
                    itemName = reader.ReadString(),
                    count = reader.ReadInt32(),
                };
                drop.instance = ItemInstanceSerializer.Read(reader);

                // 아이템이 사라졌거나 개수가 0이면 되살릴 근거가 없다.
                if (!string.IsNullOrEmpty(drop.itemName) && drop.count > 0) chunk.drops.Add(drop);
            }
        }

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

    // 세이브 포맷 식별/버전. v2: placeable 레코드 포함. v3: placeable 인벤토리를 input/output 분리.
    // v4: 슬롯마다 ItemInstance(커스텀 도구의 재질·내구도)를 덧붙였다.
    // v5: 연료 슬롯과 연소 잔량을 추가했다.
    // v6: 필드에 떨어진 아이템(DropRecord)을 청크마다 저장한다. v3~v5 도 계속 읽는다.
    private const int SaveMagic = 0x50435730; // 'PCW0'
    private const int SaveVersion = 6;
    private const int MinReadableVersion = 3;

    private Dictionary<Vector2Int, Chunk> chunks;
    private string savePath;
    private bool isLoaded;

    /// <summary>Save 직전에 호출되는 훅. MapGenerator가 로드된 기계 인벤토리를 레코드로 flush 한다.</summary>
    public System.Action OnBeforeSave;

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
    public bool Mining(Vector2Int chunkId, Vector2Int cellPos) => Mining(chunkId, cellPos, out _);

    /// <summary>
    /// 벽을 캐고 바닥으로 바꾼다. <paramref name="minedTileId"/> 로 <b>캐기 전</b> 타일 ID 를 돌려주는데,
    /// 캔 뒤에는 무엇이었는지 알 수 없어 드랍을 정할 수 없기 때문이다.
    /// </summary>
    public bool Mining(Vector2Int chunkId, Vector2Int cellPos, out string minedTileId)
    {
        Chunk chunk = GetOrCreateChunk(chunkId);
        minedTileId = chunk.GetTile(cellPos.x, cellPos.y);

        if (!Chunk.IsWall(minedTileId)) return false;

        chunk.SetTile(cellPos.x, cellPos.y, "floor:dirt");
        return true;
    }

    /// <summary>드랍이 정해진 벽 블록만 캘 수 있다(블록 종류는 가리지 않는다).</summary>
    public bool IsMineable(Vector2Int chunkId, Vector2Int cellPos)
    {
        string tileId = GetOrCreateChunk(chunkId).GetTile(cellPos.x, cellPos.y);
        if (!Chunk.IsWall(tileId)) return false;

        BlockBase block = ItemDictionary.Instance != null ? ItemDictionary.Instance.GetBlock(tileId) : null;
        return block != null && block.dropItem != null;
    }

    public void EnsurePrototypeUndergroundRoom()
    {
        for (int y = -67; y <= -61; y++)
        {
            for (int x = -3; x <= 3; x++)
            {
                Vector2Int chunkId = Chunk.GetChunkId(new Vector3(x, y, 0f));
                Vector2Int localCell = Chunk.GetLocalCellPositionInChunk(new Vector3(x, y, 0f));
                GetOrCreateChunk(chunkId).SetTile(localCell.x, localCell.y, "floor:dirt");
            }
        }
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
                chunk.SetTile(tx, ty, inSpawn ? "floor:dirt" : "wall:stone");
            }
        return chunk;
    }

    public void Save() => Save(savePath);

    public void Save(string path)
    {
        if (!isLoaded) return;
        OnBeforeSave?.Invoke(); // 로드된 기계 인벤토리를 레코드로 동기화한 뒤 직렬화
        using BinaryWriter writer = new(File.Open(path, FileMode.Create));
        writer.Write(SaveMagic);
        writer.Write(SaveVersion);
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
            int magic = reader.ReadInt32();
            if (magic != SaveMagic)
                throw new IOException("Unsupported or legacy save format (magic mismatch).");
            int version = reader.ReadInt32();
            if (version < MinReadableVersion || version > SaveVersion)
                throw new IOException($"Unsupported save version {version} (expected {MinReadableVersion}..{SaveVersion}).");
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                int cx = reader.ReadInt32();
                int cy = reader.ReadInt32();
                chunks[new Vector2Int(cx, cy)] = Chunk.Load(reader, version);
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
