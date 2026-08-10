/// <summary>
/// 지하맵 지형 규칙의 <b>정본</b>. <see cref="TerrainPalette"/> 와 같은 꼴(좌표·등급을 물으면 타일 ID 를 준다)이라
/// 규칙이 흩어지지 않는다.
///
/// 지상은 좌표가 지형을 정하지만 지하는 <b>들어올 때 쓴 탐지기 등급</b>이 정한다 —
/// 같은 방 안에서는 벽이 한 종류다. 그래서 인자가 좌표가 아니라 등급이다.
/// </summary>
public static class UndergroundPalette
{
    /// <summary>플레이어가 서는 열린 방의 반지름(셀). 3 이면 7×7.</summary>
    public const int RoomRadius = 3;

    /// <summary>드랍을 두지 않는 스폰 주변 반지름. 1 이면 중앙 3×3.</summary>
    public const int SpawnClearRadius = 1;

    /// <summary>캘 수 있는 벽이 뻗는 반지름(셀). 15 면 31×31.</summary>
    public const int DigRadius = 15;

    /// <summary>어느 등급에서도 캘 수 없는 경계벽. <see cref="BedrockObject"/> 의 dropItem 이 비어 있어
    /// <see cref="WorldMap.IsMineable"/> 이 저절로 false 가 된다 — 좌표를 보는 새 분기가 필요 없다.</summary>
    public const string BoundaryWall = "wall:bedrock";

    /// <summary>바닥에 고인 물. 지금은 그림뿐이고 통행을 막지도, 퍼낼 수도 없다(지형 유체는 별건).</summary>
    public const string WaterFloor = "floor:water";

    /// <summary>탐지기 등급 → 이 방을 채우는 벽.</summary>
    public static string WallIdFor(int tier)
    {
        if (tier >= 2) return "wall:meteorite";
        if (tier == 1) return "wall:manastone";
        return "wall:stone";
    }

    /// <summary>탐지기 등급 → 열린 방의 바닥.</summary>
    public static string FloorIdFor(int tier)
    {
        return tier >= 1 ? "floor:dirt" : "floor:stage1";
    }

    /// <summary>탐지기를 한 번 써서 포탈을 찾을 확률.</summary>
    public const float DiscoveryChance = 0.10f;

    /// <summary>
    /// 탐지기 아이템 → 등급. 탐지기가 아니면 -1.
    ///
    /// <b>표를 여기 두는 이유</b>: 상위 탐지기가 생겨도 <c>PlayerInteraction</c> 은 손대지 않고
    /// 이 표에 줄만 더하면 된다. 지금은 <c>dowsing_rod</c>(0등급) 하나뿐이다.
    /// ⚠ <c>dowsing_rod_t0</c> 는 그림까지 같은 옛 중복이라 <b>일부러 넣지 않았다</b>(ItemAliases 로 흡수할 것).
    /// </summary>
    public static int DowsingTierOf(string itemName)
    {
        switch (itemName)
        {
            case "dowsing_rod": return 0;
            case "dowsing_rod_t1": return 1;
            case "dowsing_rod_t2": return 2;
            default: return -1;
        }
    }
}
