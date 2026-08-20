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

    /// <summary>
    /// 지금 타고 있는 연료의 <b>초당 연소량</b>(세이브 v14). 연료가 정하므로 기계 값으로는 알 수 없다 —
    /// 없으면 로드 뒤 석탄을 갈탄 속도로 태운다. <b>0 은 "모름"</b> 이고 그때는 기계 값으로 떨어진다.
    /// </summary>
    public float burnRate;

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

    // 바닥 <b>위에</b> 겹쳐 그리는 것(지하 입구 · 석유 웅덩이 · 물웅덩이).
    // 바닥 타일을 갈아 끼우지 않는 이유: 그림이 모서리가 뚫린 웅덩이라 지형이 비쳐 보여야 하고,
    // 치울 때 "원래 무슨 바닥이었나" 를 기억할 필요도 없어진다.
    // 대부분의 칸에는 없으므로 배열이 아니라 <b>희소 사전</b>이다.
    private readonly Dictionary<Vector2Int, string> overlays = new();

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

    // ── 오버레이 접근자 ─────────────────────────────────────────────────
    public string GetOverlay(Vector2Int local) => overlays.TryGetValue(local, out string id) ? id : null;

    /// <summary>빈 문자열·null 을 주면 지운다 — "없음" 을 사전에 남겨 두면 세이브만 커진다.</summary>
    public void SetOverlay(Vector2Int local, string overlayId)
    {
        if (string.IsNullOrEmpty(overlayId)) overlays.Remove(local);
        else overlays[local] = overlayId;
    }

    public IEnumerable<KeyValuePair<Vector2Int, string>> Overlays => overlays;

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
            writer.Write(rec.burnRate);          // v14

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

        // v13: 바닥 위에 겹치는 오버레이. 대부분의 청크는 0 개라 4바이트로 끝난다.
        writer.Write(overlays.Count);
        foreach (KeyValuePair<Vector2Int, string> kvp in overlays)
        {
            writer.Write(kvp.Key.x);
            writer.Write(kvp.Key.y);
            writer.Write(kvp.Value ?? "");
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
                // 값형이라 else 가 필요 없다 — 안 읽으면 0 이고, 0 이 곧 "모름"(기계 값으로 떨어진다).
                if (version >= 14) rec.burnRate = reader.ReadSingle();
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

        // v13: 오버레이. <b>else 가 필요 없다</b> — overlays 는 readonly 로 생성자에서 이미 빈 사전이라
        // v12 이하 세이브에서도 null 이 될 수 없다(parcels·탱크와 다른 점이다).
        if (version >= 13)
        {
            int overlayCount = reader.ReadInt32();
            for (int i = 0; i < overlayCount; i++)
            {
                int ox = reader.ReadInt32();
                int oy = reader.ReadInt32();
                string id = reader.ReadString();
                if (!string.IsNullOrEmpty(id)) chunk.overlays[new Vector2Int(ox, oy)] = id;
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
    private const int SaveVersion = 14;
    private const int MinReadableVersion = 3;

    private Dictionary<Vector2Int, Chunk> chunks;
    private string savePath;
    private bool isLoaded;

    /// <summary>
    /// 여러 칸을 차지하는 기계가 <b>덮고 있는 칸 → 기준점(왼쪽 아래) 칸</b>.
    /// 기준점 자신은 넣지 않는다(레코드가 이미 거기 있다).
    ///
    /// <b>저장하지 않는다.</b> 크기는 <see cref="MachineBlock.Footprint"/> 에서 파생되므로
    /// 세이브 포맷은 v12 그대로고, 그림이 바뀌면 SO 한 곳만 고치면 된다
    /// (CLAUDE.md §6 "파생 상태는 저장하지 않고 매번 계산한다").
    /// </summary>
    private readonly Dictionary<Vector2Int, Vector2Int> occupancy = new();

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
            IndexChunk(chunkId, chunk);   // 생성기가 배치물을 심어 두었을 수도 있다
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
    /// 해당 월드 셀에 겹쳐 있는 오버레이 ID(지하 입구 · 웅덩이). 없으면 null.
    /// <see cref="GetTileId"/> 와 같은 규약으로 <b>청크를 만들지 않는다.</b>
    /// </summary>
    public string GetOverlayAt(Vector2Int worldCell)
    {
        Vector3 pos = new Vector3(worldCell.x, worldCell.y, 0f);
        if (!chunks.TryGetValue(Chunk.GetChunkId(pos), out Chunk chunk)) return null;

        return chunk.GetOverlay(Chunk.GetLocalCellPositionInChunk(pos));
    }

    /// <summary>
    /// 이 칸에 고여 있는 유체. <b>지형 유체의 정본은 이 함수 하나다.</b>
    /// 오버레이(석유·물 웅덩이)를 먼저 보고, 없으면 바닥 타일(옛 <c>floor:water</c>)을 본다.
    ///
    /// ⚠ <b>빈 그릇으로 퍼는 쪽(<c>PlayerInteraction.TryFillContainer</c>)과 펌프가 같은 함수를 봐야 한다</b> —
    /// 표가 둘로 갈리면 "양동이로는 퍼지는데 펌프는 안 도는" 상태가 생긴다.
    /// 어느 블록이 어느 유체인지는 여전히 <see cref="MainBlock.fluid"/> 하나가 정한다.
    /// </summary>
    public FluidDefine FluidAt(Vector2Int worldCell)
    {
        ItemDictionary dictionary = ItemDictionary.Instance;
        if (dictionary == null) return null;

        MainBlock overlay = dictionary.GetBlock(GetOverlayAt(worldCell)) as MainBlock;
        if (overlay != null && overlay.fluid != null) return overlay.fluid;

        MainBlock floor = dictionary.GetBlock(GetTileId(worldCell)) as MainBlock;
        return floor != null ? floor.fluid : null;
    }

    /// <summary>
    /// 이 칸을 걸을 때 나는 소리. <b>발소리의 정본은 이 함수 하나다.</b>
    /// 소리를 배정하지 않은 칸은 <b>조용한 것이 규칙</b>이다.
    ///
    /// ⚠ <b><see cref="FluidAt"/> 과 한 군데가 일부러 다르다 — 오버레이가 있으면 거기서 끝난다.</b>
    /// 유체는 "이 오버레이에 유체가 없으면 밑의 바닥을 본다" 가 맞지만, 발소리에서 같은 폴백을 두면
    /// <b>물웅덩이를 밟을 때 흙 소리가 난다.</b> 웅덩이·지하 입구는 바닥을 덮고 있으니
    /// 소리도 그쪽이 정한다(2026-08-19 사용자 결정: 물 위는 무음).
    /// 첨벙 소리가 생기면 <c>OverlayWater</c> 에셋에 클립만 꽂으면 되고 여기는 안 바뀐다.
    /// </summary>
    public AudioClip FootstepAt(Vector2Int worldCell)
    {
        ItemDictionary dictionary = ItemDictionary.Instance;
        if (dictionary == null) return null;

        MainBlock overlay = dictionary.GetBlock(GetOverlayAt(worldCell)) as MainBlock;
        if (overlay != null) return overlay.footstepSound;   // 비어 있으면 그 칸은 무음이다

        MainBlock floor = dictionary.GetBlock(GetTileId(worldCell)) as MainBlock;
        return floor != null ? floor.footstepSound : null;
    }

    /// <summary>
    /// 오버레이를 놓거나(null·빈 문자열이면) 치운다. <b>바닥 타일은 건드리지 않는다.</b>
    ///
    /// ⚠ 쓰기는 <see cref="SetPlaceableAt"/> 과 같은 이유로 월드 좌표 한 곳으로 모은다 —
    /// 읽기만 월드 좌표고 쓰기가 청크 로컬이면 호출부마다 좌표 변환이 흩어진다.
    /// 그리기는 호출부가 <c>MapGenerator.RefreshTile</c> 로 따로 시킨다(타일 변경과 같은 규약).
    /// </summary>
    public void SetOverlayAt(Vector2Int worldCell, string overlayId)
    {
        Vector3 pos = new Vector3(worldCell.x, worldCell.y, 0f);
        GetOrCreateChunk(Chunk.GetChunkId(pos)).SetOverlay(Chunk.GetLocalCellPositionInChunk(pos), overlayId);
    }

    /// <summary>
    /// 해당 월드 셀의 배치물. 청크가 아직 없으면 <b>만들지 않고</b> null 을 돌려준다
    /// (<see cref="GetTileId"/> 와 같은 규약) — 파이프 이웃을 살피다 화면 밖 청크를 통째로 만들면 안 된다.
    ///
    /// <b>여러 칸을 차지하는 기계는 덮인 칸에서도 기준점의 레코드를 돌려준다.</b> 이 한 곳 덕분에
    /// <see cref="PipeRouter"/> 의 <c>MachineAt·Connects·StorageAt·FaceAt</c> 와 배치 가능 판정이
    /// 코드 변경 없이 발자국을 안다.
    /// ⚠ <b>직접 조회를 반드시 먼저 한다</b> — 파이프·1×1 기계가 압도적 다수이고
    /// <see cref="PipeRouter.FindSinks"/> 가 이 함수를 대량으로 부르므로, 빈 칸에서만 조회가 한 번 는다.
    /// </summary>
    public PlaceableRecord GetPlaceableAt(Vector2Int worldCell)
    {
        PlaceableRecord direct = GetPlaceableExactly(worldCell);
        if (direct != null) return direct;

        return occupancy.TryGetValue(worldCell, out Vector2Int origin) ? GetPlaceableExactly(origin) : null;
    }

    /// <summary>덮인 칸을 해석하지 <b>않고</b> 그 칸 자체의 레코드만 본다(점유 색인을 세울 때 쓴다).</summary>
    private PlaceableRecord GetPlaceableExactly(Vector2Int worldCell)
    {
        Vector3 pos = new Vector3(worldCell.x, worldCell.y, 0f);
        if (!chunks.TryGetValue(Chunk.GetChunkId(pos), out Chunk chunk)) return null;

        return chunk.GetPlaceable(Chunk.GetLocalCellPositionInChunk(pos));
    }

    // ── 발자국(여러 칸 배치물) ──────────────────────────────────────────
    /// <summary>
    /// 이 칸을 차지하고 있는 배치물의 <b>기준점(왼쪽 아래) 칸</b>. 덮인 칸이 아니면 자기 자신을 돌려준다.
    /// 채굴·제거·전력 링크·파이프 도착지처럼 "칸으로 기계를 가리키는" 자리는 전부 이걸로 정규화한다 —
    /// 안 하면 2×2 기계 하나가 칸 수만큼 서로 다른 대상으로 세어진다.
    /// </summary>
    public Vector2Int OriginAt(Vector2Int worldCell)
        => GetPlaceableExactly(worldCell) != null ? worldCell
         : occupancy.TryGetValue(worldCell, out Vector2Int origin) ? origin
         : worldCell;

    /// <summary>기준점과 크기로 덮이는 칸을 훑는다(왼쪽 아래부터).</summary>
    public static IEnumerable<Vector2Int> Cells(Vector2Int origin, Vector2Int size)
    {
        int w = Mathf.Max(1, size.x);
        int h = Mathf.Max(1, size.y);
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
                yield return new Vector2Int(origin.x + dx, origin.y + dy);
    }

    /// <summary>이 배치물이 덮는 칸(레코드의 blockId 로 크기를 조회한다).</summary>
    public static IEnumerable<Vector2Int> CellsOf(Vector2Int origin, PlaceableRecord record)
        => Cells(origin, FootprintOf(record));

    /// <summary>
    /// 레코드의 발자국. 딕셔너리가 없거나 기계가 아니면 1×1.
    /// ⚠ <b><see cref="Singleton{T}.Instance"/> 가 아니라 <c>InstanceIfAlive</c> 다</b> —
    /// 에디트 모드나 초기화 순서에 따라 여기서 딕셔너리를 <b>만들어 버리면</b> 씬에 유령이 남는다.
    /// 아직 없어서 1×1 로 읽혀도, <see cref="ItemDictionary.BuildIndexes"/> 끝의
    /// <see cref="RebuildOccupancy"/> 가 딕셔너리가 선 뒤 다시 세워 준다.
    /// </summary>
    public static Vector2Int FootprintOf(PlaceableRecord record)
        => record != null && ItemDictionary.InstanceIfAlive != null
            ? ItemDictionary.InstanceIfAlive.FootprintOf(record.blockId)
            : Vector2Int.one;

    /// <summary>
    /// 배치물을 월드 좌표로 놓는다. <b>발자국 전체가 비어 있어야 성공한다.</b>
    ///
    /// 예전에는 읽기(<see cref="GetPlaceableAt"/>)만 월드 좌표고 쓰기는 청크 로컬 좌표라 좌표계가 갈려 있었다.
    /// 발자국은 청크 경계를 넘을 수 있어 그 비대칭을 그대로 둘 수 없다 — 쓰기도 여기로 모은다.
    /// </summary>
    public bool SetPlaceableAt(Vector2Int origin, PlaceableRecord record)
    {
        if (record == null) return false;

        Vector2Int size = FootprintOf(record);
        foreach (Vector2Int cell in Cells(origin, size))
            if (GetPlaceableAt(cell) != null) return false;

        Vector3 pos = new Vector3(origin.x, origin.y, 0f);
        Chunk chunk = GetOrCreateChunk(Chunk.GetChunkId(pos));
        chunk.SetPlaceable(Chunk.GetLocalCellPositionInChunk(pos), record);

        foreach (Vector2Int cell in Cells(origin, size))
            if (cell != origin) occupancy[cell] = origin;

        return true;
    }

    /// <summary>
    /// 배치물을 지운다. <b>덮인 칸을 짚어도 된다</b> — 기준점을 찾아 레코드와 점유 표를 함께 없앤다.
    /// </summary>
    public void RemovePlaceableAt(Vector2Int anyCoveredCell)
    {
        Vector2Int origin = OriginAt(anyCoveredCell);
        PlaceableRecord record = GetPlaceableExactly(origin);
        if (record == null) return;

        foreach (Vector2Int cell in CellsOf(origin, record))
            if (cell != origin) occupancy.Remove(cell);

        Vector3 pos = new Vector3(origin.x, origin.y, 0f);
        if (chunks.TryGetValue(Chunk.GetChunkId(pos), out Chunk chunk))
            chunk.RemovePlaceable(Chunk.GetLocalCellPositionInChunk(pos));
    }

    /// <summary>
    /// 점유 색인을 통째로 다시 세운다. 세이브를 읽은 뒤·월드를 갈아탄 뒤,
    /// 그리고 <see cref="ItemDictionary"/> 색인이 다시 만들어진 뒤에 부른다.
    /// </summary>
    public void RebuildOccupancy()
    {
        occupancy.Clear();
        if (chunks == null) return;

        // ⚠ 청크 순서를 정렬한다. 겹쳤을 때 "먼저 온 것이 이긴다" 는 규칙이 Dictionary 순회 순서에
        // 의존하면 실행할 때마다 이긴 쪽이 바뀌어, 같은 세이브가 매번 다르게 보인다.
        List<Vector2Int> ids = new(chunks.Keys);
        ids.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
        foreach (Vector2Int id in ids) IndexChunk(id, chunks[id]);
    }

    /// <summary>
    /// 청크 하나의 배치물을 점유 색인에 넣는다.
    ///
    /// ⚠ <b>겹치면 먼저 온 기준점이 이긴다.</b> 그림이 커져 옛 세이브의 두 기계가 겹칠 수 있는데,
    /// 진 쪽도 <see cref="GetPlaceableAt"/> 이 직접 조회를 먼저 하므로 <b>자기 칸에서는 계속 열리고 캘 수 있다</b>.
    /// 조용히 넘기지 않고 어느 두 기계가 어디서 겹쳤는지 한 줄 남긴다.
    /// </summary>
    private void IndexChunk(Vector2Int chunkId, Chunk chunk)
    {
        foreach (var kvp in chunk.Placeables)
        {
            Vector2Int origin = chunkId * ChunkSize + kvp.Key;
            foreach (Vector2Int cell in CellsOf(origin, kvp.Value))
            {
                if (cell == origin) continue;
                if (occupancy.TryGetValue(cell, out Vector2Int other) && other != origin)
                {
                    Debug.LogWarning($"[WorldMap] 배치물 발자국이 겹칩니다: {cell} 을 " +
                                     $"{other}('{GetPlaceableExactly(other)?.blockId}') 와 " +
                                     $"{origin}('{kvp.Value.blockId}') 가 함께 덮습니다. 앞의 것을 유지합니다.");
                    continue;
                }
                occupancy[cell] = origin;
            }
        }
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
        occupancy.Clear();      // 청크를 버렸으니 파생 색인도 함께 버린다(지상 좌표가 지하에 남는다)
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
        occupancy.Clear();
        chunkGenerator = GenerateSurfaceChunk;
        IsEphemeral = false;
        Load(savePath);         // 들어가기 직전에 확정해 둔 그 파일이다(Load 가 색인을 다시 세운다)
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

        // ⚠ <b>플레이 중에 스크립트를 재컴파일하면 여기가 null 이 된다.</b> 도메인 리로드는 MonoBehaviour 의
        //    직렬화 가능한 필드(<c>bool isLoaded</c>)는 살려 오지만 <c>Dictionary</c> 는 못 살려서,
        //    "로드는 됐다는데 청크가 하나도 없는" 상태가 된다(ItemDictionary.EnsureIndex 와 같은 함정).
        //    막지 않으면 <c>chunks.Count</c> 에서 NRE 가 나고 SafeFile 이 그것을 "기록 실패" 로만 알려 줘
        //    원인을 찾기 어렵다. <b>조용히 넘어가지 않고 반드시 로그를 남긴다</b> —
        //    아래 Load 의 isLoaded 주석과 같은 이유로, 저장이 소리 없이 무시되는 상태가 제일 나쁘다.
        if (chunks == null)
        {
            Debug.LogError("[WorldMap] 도메인 리로드로 청크가 사라져 저장을 건너뜁니다(플레이 중 스크립트 재컴파일). " +
                           "디스크의 세이브는 그대로입니다 — 플레이를 껐다 켜면 정상으로 돌아옵니다.");
            return;
        }
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
        occupancy.Clear();
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
            // 읽어 들인(또는 실패해 비운) 청크에서 발자국 색인을 파생시킨다.
            RebuildOccupancy();
        }
    }
}
