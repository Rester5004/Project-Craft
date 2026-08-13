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

    /// <summary>보유 전력. 발전기에겐 발전 버퍼, 소비 기계에겐 남은 잔량이다.</summary>
    public float energy;

    /// <summary>
    /// 라운드로빈 분배가 다음에 시작할 링크 번호.
    /// 저장하지 않으면 청크를 오갈 때마다 0으로 돌아가 앞쪽 기계만 계속 편애하게 된다.
    /// </summary>
    public int roundRobinCursor;

    /// <summary>발전기가 전력을 보내는 대상들의 월드 셀. 발전기가 아니면 길이 0.</summary>
    public Vector2Int[] links;

    /// <summary>파이프가 운반 중인 짐. 파이프가 아니면 길이 0.</summary>
    public ParcelRecord[] parcels;

    /// <summary>
    /// 플레이어가 렌치로 지정한 네 면의 상태(N/E/S/W 순서로 2비트씩). 0 이면 전부 기본이다.
    /// 읽고 쓰는 것은 <see cref="PipeRouter.FaceOf"/> · <see cref="PipeRouter.SetFace"/> 를 쓴다
    /// — 시프트 규칙을 아는 곳이 한 군데뿐이어야 어긋나지 않는다.
    /// </summary>
    public byte faceModes;

    /// <summary>
    /// 지금 가공 중인 진행도(초). 기계가 아니면 0 이다.
    ///
    /// 자동 기계는 몇 초면 다시 채워지므로 없어도 티가 안 났지만, <b>수동 기계는 20번을 눌러야 하나가 나온다</b>
    /// — 19번 누른 뒤 청크가 내려가면 그 노동이 통째로 사라진다. 그래서 v10 부터 저장한다.
    /// 어느 레시피의 진행도인지는 저장하지 않는다: 청크가 내려가 있는 동안에는 입력 슬롯을 바꿀 수단이
    /// 없으므로, 다시 올라올 때 <see cref="MachineInstance"/> 가 고르는 레시피가 저장 시점과 같다.
    /// </summary>
    public float progress;

    /// <summary>
    /// 기계 탱크의 내용(v11). 유체 종류는 <see cref="FluidDefine.fluidId"/> 로 저장한다 —
    /// 에셋 참조가 아니라 문자열이라 유체 에셋을 옮겨도 세이브가 깨지지 않는다(아이템의 itemName 과 같은 규약).
    /// 기계가 아니거나 탱크가 없으면 길이 0.
    /// </summary>
    public string[] inputFluidIds;
    public int[] inputFluidAmounts;
    public string[] outputFluidIds;
    public int[] outputFluidAmounts;

    /// <summary>업그레이드 모듈 칸(v12). 소모되지 않으므로 개수 자체가 곧 성능이다.</summary>
    public string[] upgradeItemNames;
    public int[] upgradeCounts;
    public ItemInstance[] upgradeInstances;

    /// <summary>
    /// 이 배치물이 업그레이드로 올린 티어(v12). 0 이면 <see cref="MachineBlock.tier"/> 를 그대로 쓴다.
    ///
    /// SO 를 런타임에 고칠 수는 없으므로(에디터에서 에셋이 영구히 바뀐다) 코어 조합기의 티어는
    /// <b>인스턴스마다</b> 여기에 둔다 — 코어가 둘이면 한쪽만 올라가는 것이 맞다.
    /// </summary>
    public int tier;

    /// <summary>작물을 심은 UTC 시각. 작물이 아닌 배치물은 0.</summary>
    public long plantedAtUtcTicks;

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
        links = System.Array.Empty<Vector2Int>();
        parcels = System.Array.Empty<ParcelRecord>();
        inputFluidIds = new string[0];
        inputFluidAmounts = new int[0];
        outputFluidIds = new string[0];
        outputFluidAmounts = new int[0];
        upgradeItemNames = new string[0];
        upgradeCounts = new int[0];
        upgradeInstances = new ItemInstance[0];
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

            writer.Write(rec.energy);
            writer.Write(rec.roundRobinCursor);
            int linkCount = rec.links != null ? rec.links.Length : 0;
            writer.Write(linkCount);
            for (int i = 0; i < linkCount; i++)
            {
                writer.Write(rec.links[i].x);
                writer.Write(rec.links[i].y);
            }

            // v8: 파이프가 운반 중인 짐. 파이프가 아니면 0 이라 4바이트만 든다.
            int parcelCount = rec.parcels != null ? rec.parcels.Length : 0;
            writer.Write(parcelCount);
            for (int i = 0; i < parcelCount; i++)
            {
                ParcelRecord parcel = rec.parcels[i];
                writer.Write(parcel.itemName ?? "");
                writer.Write(parcel.count);
                ItemInstanceSerializer.Write(writer, parcel.instance);
                writer.Write(parcel.destX);
                writer.Write(parcel.destY);
                writer.Write(parcel.remaining);

                // v11: 유체 짐. 아이템 짐이면 빈 문자열 + 0 이라 아이템 파이프에는 몇 바이트만 더 든다.
                writer.Write(parcel.fluidId ?? "");
                writer.Write(parcel.amount);
            }

            // v9: 렌치로 지정한 네 면의 상태. 파이프가 아니면 언제나 0 이라 1바이트로 끝난다.
            writer.Write(rec.faceModes);

            // v10: 가공 진행도(초). 기계가 아니면 언제나 0 이다.
            writer.Write(rec.progress);

            // v11: 기계 탱크. 탱크가 없으면 길이 0 이라 4바이트씩만 든다.
            WriteFluidArray(writer, rec.inputFluidIds, rec.inputFluidAmounts);
            WriteFluidArray(writer, rec.outputFluidIds, rec.outputFluidAmounts);

            // v12: 업그레이드 모듈 칸 + 업그레이드로 올린 티어.
            WriteSlotArray(writer, rec.upgradeItemNames, rec.upgradeCounts, rec.upgradeInstances);
            writer.Write(rec.tier);
            writer.Write(rec.plantedAtUtcTicks);
            writer.Write(rec.plantedAtUtcTicks);
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

    private static void WriteFluidArray(BinaryWriter writer, string[] ids, int[] amounts)
    {
        int cap = ids != null ? ids.Length : 0;
        writer.Write(cap);
        for (int i = 0; i < cap; i++)
        {
            writer.Write(ids[i] ?? "");
            writer.Write(amounts != null && i < amounts.Length ? amounts[i] : 0);
        }
    }

    private static void ReadFluidArray(BinaryReader reader, out string[] ids, out int[] amounts)
    {
        int cap = reader.ReadInt32();
        ids = new string[cap];
        amounts = new int[cap];
        for (int i = 0; i < cap; i++)
        {
            ids[i] = reader.ReadString();
            amounts[i] = reader.ReadInt32();
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

            if (version >= 7)
            {
                rec.energy = reader.ReadSingle();
                rec.roundRobinCursor = reader.ReadInt32();
                int linkCount = reader.ReadInt32();
                rec.links = new Vector2Int[linkCount];
                for (int i = 0; i < linkCount; i++)
                    rec.links[i] = new Vector2Int(reader.ReadInt32(), reader.ReadInt32());
            }
            else
            {
                rec.links = System.Array.Empty<Vector2Int>();
            }

            if (version >= 8)
            {
                int parcelCount = reader.ReadInt32();
                rec.parcels = new ParcelRecord[parcelCount];
                for (int i = 0; i < parcelCount; i++)
                {
                    ParcelRecord parcel = new ParcelRecord
                    {
                        itemName = reader.ReadString(),
                        count = reader.ReadInt32(),
                        instance = ItemInstanceSerializer.Read(reader),
                        destX = reader.ReadInt32(),
                        destY = reader.ReadInt32(),
                        remaining = reader.ReadSingle(),
                    };
                    // v11 부터 유체 짐이 있다. v10 이하는 전부 아이템 짐이라 기본값(빈 문자열 · 0)이 곧 정답이다.
                    if (version >= 11)
                    {
                        parcel.fluidId = reader.ReadString();
                        parcel.amount = reader.ReadInt32();
                    }
                    rec.parcels[i] = parcel;
                }
            }
            else
            {
                // 이 else 를 빼면 v7 이하 세이브에서 parcels 가 null 로 남아 PipeCell 이 터진다.
                rec.parcels = System.Array.Empty<ParcelRecord>();
            }

            // parcels 와 달리 else 가 없어도 된다. byte 의 기본값 0 이 곧 "네 면 모두 기본" 이다.
            if (version >= 9) rec.faceModes = reader.ReadByte();

            // 같은 이유로 else 가 필요 없다. float 기본값 0 이 곧 "진행 없음" 이다.
            if (version >= 10) rec.progress = reader.ReadSingle();

            // v11: 기계 탱크. <b>else 로 빈 배열을 넣어야 한다</b> — 참조형이라 null 로 두면
            // MachineInstance.LoadTanks 가 v10 이하 세이브에서 통째로 터진다(parcels 와 같은 함정).
            if (version >= 11)
            {
                ReadFluidArray(reader, out rec.inputFluidIds, out rec.inputFluidAmounts);
                ReadFluidArray(reader, out rec.outputFluidIds, out rec.outputFluidAmounts);
            }
            else
            {
                rec.inputFluidIds = new string[0];
                rec.inputFluidAmounts = new int[0];
                rec.outputFluidIds = new string[0];
                rec.outputFluidAmounts = new int[0];
            }

            // v12: 업그레이드 칸과 티어. 참조형이라 여기도 else 로 빈 배열을 넣어야 한다.
            if (version >= 12)
            {
                ReadSlotArray(reader, version, out rec.upgradeItemNames, out rec.upgradeCounts, out rec.upgradeInstances);
                rec.tier = reader.ReadInt32();
            }
            else
            {
                rec.upgradeItemNames = new string[0];
                rec.upgradeCounts = new int[0];
                rec.upgradeInstances = new ItemInstance[0];
            }

            if (version >= 9) rec.plantedAtUtcTicks = reader.ReadInt64();

            if (version >= 9) rec.plantedAtUtcTicks = reader.ReadInt64();

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
    // v7: 기계의 보유 전력·라운드로빈 커서·발전기의 전력 링크 목록을 추가했다.
    // v8: 파이프가 운반 중인 짐(ParcelRecord)을 배치물마다 저장한다.
    // v9: 렌치로 지정한 파이프 네 면의 상태(faceModes 1바이트)를 배치물마다 저장한다.
    // v10: 기계의 가공 진행도(progress). 수동 기계는 20번을 눌러야 하나가 나오므로 잃으면 안 된다.
    // v11: 기계 유체 탱크(입력·출력)와 파이프가 나르는 유체 짐(ParcelRecord.fluidId/amount).
    // v12: 업그레이드 모듈 칸과 인스턴스별 티어(코어 조합기 업그레이드).
    private const int SaveMagic = 0x50435730; // 'PCW0'
    private const int SaveVersion = 12;
    private const int MinReadableVersion = 3;

    private Dictionary<Vector2Int, Chunk> chunks;
    private string savePath;
    private bool isLoaded;

    /// <summary>
    /// 없는 청크를 무엇으로 채울지. 지상은 <see cref="GenerateSurfaceChunk"/> 고,
    /// 지하맵은 <see cref="EnterEphemeralWorld"/> 로 이것만 갈아 끼운다.
    ///
    /// <b>월드를 객체로 나누지 않고 델리게이트 하나로 가는 것이 요점이다.</b> 나누면
    /// <c>MapGenerator</c>·<c>PlayerInteraction</c>·<c>PipeRouter</c>·<c>MachineInstance</c> 가
    /// 전부 "어느 월드냐"를 인자로 받아야 한다 — 지금은 호출부를 한 줄도 안 고치고 지하에서 그대로 돈다.
    /// </summary>
    private System.Func<Vector2Int, Chunk> chunkGenerator;

    /// <summary>
    /// 지금 들고 있는 월드가 <b>디스크에 남지 않는</b> 임시 월드인가(지하맵).
    /// 참이면 <see cref="Save"/> 가 통째로 아무것도 하지 않는다 — 자동 저장·종료 저장이 한 번에 막힌다.
    /// </summary>
    public bool IsEphemeral { get; private set; }

    /// <summary>Save 직전에 호출되는 훅. MapGenerator가 로드된 기계 인벤토리를 레코드로 flush 한다.</summary>
    public System.Action OnBeforeSave;

    protected override void Awake()
    {
        base.Awake();

        savePath = DefaultSavePath;
        chunks = new();
        chunkGenerator = GenerateSurfaceChunk;
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
            chunk = chunkGenerator(chunkId);
            chunks[chunkId] = chunk;
        }
        return chunk;
    }
    /// <summary>
    /// 해당 월드 셀의 타일 ID. 청크가 아직 없으면 <b>생성하지 않고</b> null 을 돌려준다
    /// — 텍스처를 그리다 화면 밖 청크를 통째로 만들어 버리면 안 되기 때문이다.
    /// </summary>
    public string GetTileId(Vector2Int worldCell)
    {
        Vector3 pos = new Vector3(worldCell.x, worldCell.y, 0f);
        if (!chunks.TryGetValue(Chunk.GetChunkId(pos), out Chunk chunk)) return null;

        Vector2Int local = Chunk.GetLocalCellPositionInChunk(pos);
        return chunk.GetTile(local.x, local.y);
    }

    /// <summary>
    /// 해당 월드 셀의 배치물. 청크가 아직 없으면 <b>만들지 않고</b> null 을 돌려준다
    /// (<see cref="GetTileId"/> 와 같은 규약) — 파이프 이웃을 살피다 화면 밖 청크를 통째로 만들면 안 된다.
    /// </summary>
    public PlaceableRecord GetPlaceableAt(Vector2Int worldCell)
    {
        Vector3 pos = new Vector3(worldCell.x, worldCell.y, 0f);
        if (!chunks.TryGetValue(Chunk.GetChunkId(pos), out Chunk chunk)) return null;

        return chunk.GetPlaceable(Chunk.GetLocalCellPositionInChunk(pos));
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

        // 캔 자리는 그 지역의 바닥이 된다(스테이지1 안이면 돌바닥, 밖이면 흙).
        int worldX = chunkId.x * ChunkSize + cellPos.x;
        int worldY = chunkId.y * ChunkSize + cellPos.y;
        chunk.SetTile(cellPos.x, cellPos.y, TerrainPalette.FloorIdAt(worldX, worldY));
        return true;
    }

    /// <summary>
    /// 바닥 칸에 벽을 세운다(<see cref="Mining"/> 의 대칭). 이미 벽이면 아무것도 하지 않고 false.
    /// 칸이 비어 있는지(기계·플레이어) 는 호출자가 판단한다 — 월드는 타일만 안다.
    /// </summary>
    public bool Place(Vector2Int chunkId, Vector2Int cellPos, string wallTileId)
    {
        if (!Chunk.IsWall(wallTileId)) return false;

        Chunk chunk = GetOrCreateChunk(chunkId);
        if (Chunk.IsWall(chunk.GetTile(cellPos.x, cellPos.y))) return false;

        chunk.SetTile(cellPos.x, cellPos.y, wallTileId);
        return true;
    }

    /// <summary>기존 바닥 한 칸을 다른 바닥으로 교체한다(농지 설치용).</summary>
    public bool PlaceFloor(Vector2Int chunkId, Vector2Int cellPos, string floorTileId)
    {
        if (!Chunk.IsFloor(floorTileId)) return false;
        Chunk chunk = GetOrCreateChunk(chunkId);
        if (!Chunk.IsFloor(chunk.GetTile(cellPos.x, cellPos.y))) return false;
        chunk.SetTile(cellPos.x, cellPos.y, floorTileId);
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

    // ── 월드 교체 (지하맵) ──────────────────────────────────────────────
    /// <summary>
    /// 디스크에 남지 않는 임시 월드로 갈아탄다(지하맵 진입).
    ///
    /// <b>지금 월드를 먼저 디스크에 확정한 뒤</b> 청크를 통째로 버린다 — 돌아올 때 그 파일을 다시 읽으므로
    /// 여기서 저장을 빠뜨리면 마지막 자동 저장 이후의 지상 작업이 통째로 사라진다.
    /// 씬을 로드하기 <b>전에</b> 부를 것: 이 싱글톤은 씬을 넘어 살아남으므로 교체가 그대로 따라오고,
    /// 새 씬 <c>MapGenerator.Start</c> 가 곧바로 <c>UpdateChunks</c> 를 부르는 것과 순서를 다투지 않는다.
    /// </summary>
    public void EnterEphemeralWorld(System.Func<Vector2Int, Chunk> generator)
    {
        if (generator == null) return;

        Save();                 // 지상을 확정한다(IsEphemeral 이 아직 false 라 실제로 쓰인다)
        chunks.Clear();
        chunkGenerator = generator;
        IsEphemeral = true;
    }

    /// <summary>
    /// 영속 월드(지상)로 되돌아온다. 임시 월드의 청크는 <b>저장하지 않고 버린다</b> — 그것이 의도다.
    /// </summary>
    public void ReturnToPersistentWorld()
    {
        if (!IsEphemeral) return;

        chunks.Clear();
        chunkGenerator = GenerateSurfaceChunk;
        IsEphemeral = false;
        Load(savePath);         // 들어가기 직전에 확정해 둔 그 파일이다
    }

    /// <summary>
    /// 지상 청크를 새로 만든다. 스폰 앞 6x6 만 뚫어 두고 나머지는 벽으로 채우며,
    /// 어떤 벽·바닥을 쓸지는 <see cref="TerrainPalette"/> 가 좌표를 보고 정한다.
    /// </summary>
    Chunk GenerateSurfaceChunk(Vector2Int chunkId)
    {
        Chunk chunk = new();
        for (int ty = 0; ty < ChunkSize; ty++)
            for (int tx = 0; tx < ChunkSize; tx++)
            {
                int wx = chunkId.x * ChunkSize + tx;
                int wy = chunkId.y * ChunkSize + ty;
                bool inSpawn = wx >= -3 && wx <= 2 && wy >= -2 && wy <= 3;
                chunk.SetTile(tx, ty, inSpawn
                    ? TerrainPalette.FloorIdAt(wx, wy)
                    : TerrainPalette.WallIdAt(wx, wy));
            }
        return chunk;
    }

    public void Save() => Save(savePath);

    public void Save(string path)
    {
        if (!isLoaded) return;
        // 지하맵은 디스크에 닿지 않는다. 여기 한 줄이 MapGenerator 의 10초 자동 저장 ·
        // OnApplicationQuit · OnApplicationPause 를 <b>한꺼번에</b> 막는다 — 호출부마다 가드를 두면
        // 언젠가 한 곳이 빠져 지하 청크가 지상 세이브를 덮어쓴다.
        if (IsEphemeral) return;
        OnBeforeSave?.Invoke(); // 로드된 기계 인벤토리를 레코드로 동기화한 뒤 직렬화

        // 임시 파일에 다 쓴 뒤에야 교체한다. 예전처럼 원본을 먼저 비우고 쓰면
        // 기록 도중 프로세스가 죽었을 때 잘린 파일이 남고, 그것이 다음 실행에서 월드 전손으로 이어졌다.
        SafeFile.WriteAtomic(path, writer =>
        {
            writer.Write(SaveMagic);
            writer.Write(SaveVersion);
            writer.Write(chunks.Count);
            foreach (var kvp in chunks)
            {
                writer.Write(kvp.Key.x);
                writer.Write(kvp.Key.y);
                kvp.Value.Save(writer);
            }
        });
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
            // 지우지 않고 옆으로 치운다. 포맷 오류인지 일시적 IO 오류(파일 잠김 등)인지 여기서는 구분할 수 없는데,
            // 예전에는 둘 다 File.Delete 로 처리해 멀쩡한 월드가 사라졌다.
            Debug.LogWarning($"[WorldMap] 세이브 파일 로드 실패, 새로 생성합니다: {e.Message}");
            chunks.Clear();
            SafeFile.Quarantine(path);
        }
        finally
        {
            // <b>반드시 선다.</b> 예전에는 격리(옛 File.Delete)가 던지면 이 줄에 닿지 못해
            // isLoaded 가 false 로 남았고, Save 첫 줄의 가드 때문에 그 세션 내내 저장이 조용히 무시됐다.
            isLoaded = true;
        }
    }
}
