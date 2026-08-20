using UnityEngine;

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

    /// <summary>
    /// 등급 → 지하 입구 오버레이 id. 등급을 <b>id 에 넣는다</b> — 오버레이는 문자열 하나만 저장하므로
    /// 등급을 따로 실을 자리가 없고, <see cref="WallIdFor"/> 와 같은 꼴이라 표가 흩어지지 않는다.
    /// </summary>
    public static string HoleIdFor(int tier) => "overlay:hole" + Mathf.Clamp(tier, 0, 2);

    /// <summary>지하 입구 오버레이면 그 등급, 아니면 -1.</summary>
    public static int TierOfHole(string overlayId)
    {
        if (string.IsNullOrEmpty(overlayId) || !overlayId.StartsWith("overlay:hole")) return -1;
        return int.TryParse(overlayId.Substring("overlay:hole".Length), out int tier) ? Mathf.Clamp(tier, 0, 2) : -1;
    }

    /// <summary>석유 웅덩이 오버레이.</summary>
    public const string OilPool = "overlay:oil";

    /// <summary>지하 방에 고이는 물웅덩이 오버레이(바닥을 갈아 끼우지 않고 위에 얹는다).</summary>
    public const string WaterPool = "overlay:water";

    /// <summary>다우징 로드가 한 번에 무언가를 찾을 확률.</summary>
    public const float DiscoveryChance = 0.10f;

    /// <summary>
    /// 탐지기별 발견 확률. 표를 여기 두는 이유는 <see cref="DowsingTierOf"/> 와 같다 —
    /// 새 탐지기가 생겨도 <c>PlayerInteraction</c> 은 손대지 않는다.
    /// </summary>
    public static float DiscoveryChanceFor(string itemName)
    {
        switch (itemName)
        {
            case "cavity_scanner": return 0.25f;   // 내구도 30 짜리라 한 개로 평균 7~8번 찾는다
            default: return DiscoveryChance;
        }
    }

    /// <summary>
    /// 발견에 성공했을 때 <b>지하 입구 대신 석유 웅덩이</b>가 나올 확률.
    /// 다우징 로드는 0 이라 지금까지와 완전히 같다 — 공동 탐색기만 석유를 찾아낸다.
    /// </summary>
    public static float OilPoolChanceFor(string itemName)
    {
        switch (itemName)
        {
            case "cavity_scanner": return 0.20f;
            default: return 0f;
        }
    }

    /// <summary>
    /// 탐지기 아이템 → 등급. 탐지기가 아니면 -1.
    ///
    /// <b>표를 여기 두는 이유</b>: 상위 탐지기가 생겨도 <c>PlayerInteraction</c> 은 손대지 않고
    /// 이 표에 줄만 더하면 된다.
    /// ⚠ <c>dowsing_rod_t0</c> 는 그림까지 같은 옛 중복이라 <b>일부러 넣지 않았다</b>(ItemAliases 로 흡수할 것).
    /// </summary>
    public static int DowsingTierOf(string itemName)
    {
        switch (itemName)
        {
            case "dowsing_rod": return 0;
            case "cavity_scanner": return 1;   // 레이저 가공기로 만드는 고급품 — 마력석 방이 열린다
            case "dowsing_rod_t1": return 1;
            case "dowsing_rod_t2": return 2;
            default: return -1;
        }
    }
}
