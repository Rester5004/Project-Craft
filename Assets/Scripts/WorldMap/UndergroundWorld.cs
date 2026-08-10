using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지하맵 한 판을 만든다. <see cref="WorldMap.EnterEphemeralWorld"/> 에 이 객체의
/// <see cref="Generate"/> 를 넘기면, 청크를 요구할 때마다 여기서 만들어 준다.
///
/// <b>인스턴스인 이유</b>: 방(7×7)은 원점을 중심으로 하므로 <b>청크 네 장에 걸쳐 있다.</b>
/// 물·전리품을 청크마다 굴리면 같은 방인데 청크 경계에서 규칙이 갈린다 — 그래서
/// 생성자에서 방 전체를 한 번에 정해 두고, <see cref="Generate"/> 는 그 결과를 나눠 담기만 한다.
///
/// 지형 규칙은 <see cref="UndergroundPalette"/>, 보상 규칙은 <see cref="UndergroundLootTable"/> 이 정본이다.
/// </summary>
public class UndergroundWorld
{
    /// <summary>한 판에 고이는 물 칸 수(균등). 7×7 = 49칸 중이라 이 정도가 눈에 띄면서 길을 막지 않는다.</summary>
    private const int MinWaterCells = 2;
    private const int MaxWaterCells = 4;

    /// <summary>플레이어가 서는 칸. 여기엔 물도 전리품도 놓지 않는다.</summary>
    public static readonly Vector2Int SpawnCell = Vector2Int.zero;

    private readonly int tier;
    private readonly string wallId;
    private readonly string floorId;

    private readonly HashSet<Vector2Int> waterCells = new();
    private readonly Dictionary<Vector2Int, DropRecord> loot = new();

    public int Tier => tier;

    public UndergroundWorld(int tier, int seed)
    {
        this.tier = tier;
        wallId = UndergroundPalette.WallIdFor(tier);
        floorId = UndergroundPalette.FloorIdFor(tier);

        System.Random rng = new System.Random(seed);
        PlaceWater(rng);
        PlaceLoot(rng);
    }

    // ── 방 배치 (생성자에서 한 번) ──────────────────────────────────────
    private void PlaceWater(System.Random rng)
    {
        List<Vector2Int> candidates = RoomCells(UndergroundPalette.RoomRadius);
        candidates.Remove(SpawnCell);   // 스폰 칸은 비운다

        int want = rng.Next(MinWaterCells, MaxWaterCells + 1);
        for (int i = 0; i < want && candidates.Count > 0; i++)
        {
            int pick = rng.Next(candidates.Count);
            waterCells.Add(candidates[pick]);
            candidates.RemoveAt(pick);
        }
    }

    private void PlaceLoot(System.Random rng)
    {
        foreach (Vector2Int cell in RoomCells(UndergroundPalette.RoomRadius))
        {
            // 스폰과 동시에 주워지지 않게 중앙 3×3 은 비우고, 물에 잠긴 칸에도 두지 않는다.
            if (Mathf.Abs(cell.x) <= UndergroundPalette.SpawnClearRadius
                && Mathf.Abs(cell.y) <= UndergroundPalette.SpawnClearRadius) continue;
            if (waterCells.Contains(cell)) continue;

            foreach (UndergroundLootTable.Row row in UndergroundLootTable.RowsFor(tier))
            {
                if (rng.NextDouble() >= row.chance) continue;

                loot[cell] = new DropRecord
                {
                    x = cell.x + 0.5f,   // 셀 중앙
                    y = cell.y + 0.5f,
                    itemName = row.itemName,
                    count = rng.Next(row.minCount, row.maxCount + 1),
                };
                break;   // 한 칸에 한 종류
            }
        }
    }

    private static List<Vector2Int> RoomCells(int radius)
    {
        List<Vector2Int> cells = new();
        for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
                cells.Add(new Vector2Int(x, y));
        return cells;
    }

    // ── WorldMap 이 부르는 청크 생성기 ──────────────────────────────────
    public Chunk Generate(Vector2Int chunkId)
    {
        Chunk chunk = new();
        int size = WorldMap.ChunkSize;

        for (int ty = 0; ty < size; ty++)
        {
            for (int tx = 0; tx < size; tx++)
            {
                Vector2Int cell = new(chunkId.x * size + tx, chunkId.y * size + ty);
                chunk.SetTile(tx, ty, TileAt(cell));

                if (loot.TryGetValue(cell, out DropRecord drop)) chunk.AddDrop(drop);
            }
        }
        return chunk;
    }

    private string TileAt(Vector2Int cell)
    {
        int ring = Mathf.Max(Mathf.Abs(cell.x), Mathf.Abs(cell.y));   // 체비셰프 거리 = 정사각형 테두리

        if (ring > UndergroundPalette.DigRadius) return UndergroundPalette.BoundaryWall;
        if (ring > UndergroundPalette.RoomRadius) return wallId;
        return waterCells.Contains(cell) ? UndergroundPalette.WaterFloor : floorId;
    }

    /// <summary>검증·디버그용 집계(생성 결과를 밖에서 세어 볼 수 있게).</summary>
    public int WaterCellCount => waterCells.Count;
    public int LootCellCount => loot.Count;
}
