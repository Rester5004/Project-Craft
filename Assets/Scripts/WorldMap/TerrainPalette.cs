/// <summary>
/// 월드 좌표 → 어떤 지형인가. <b>지형 규칙은 여기 한 곳에만</b> 있다.
///
/// 예전에는 청크 생성·채굴·타일 갱신 네 군데에 "wall:stone"/"floor:dirt" 가 흩어져 있어서
/// 지역별 지형을 넣으려면 전부 찾아 고쳐야 했다. 이제 좌표를 물어보면 된다.
///
/// 스폰(0,0)을 중심으로 반지름 <see cref="StageOneRadiusChunks"/> 청크 원 안쪽이 스테이지1,
/// 바깥이 스테이지2다. 반지름을 바꾸려면 아래 상수 하나만 고치면 된다.
/// </summary>
public static class TerrainPalette
{
    public const int StageOneRadiusChunks = 5;
    public const int StageOneRadiusCells = StageOneRadiusChunks * WorldMap.ChunkSize;   // 80

    public const string Stage1Wall = "wall:stone";       // 캐면 돌
    public const string Stage1Floor = "floor:stage1";
    public const string Stage2Wall = "wall:manastone";   // 캐면 마력석
    public const string Stage2Floor = "floor:dirt";

    /// <summary>
    /// 스폰(0,0)에서 반지름 5청크 원 안쪽인가.
    /// 청크가 아니라 <b>셀</b> 단위로 재기 때문에 경계가 청크 격자에 걸리지 않고 매끈한 원이 된다.
    /// </summary>
    public static bool IsStageOne(int worldX, int worldY)
        => worldX * worldX + worldY * worldY <= StageOneRadiusCells * StageOneRadiusCells;

    /// <summary>이 좌표에 벽을 놓는다면 어떤 벽인가.</summary>
    public static string WallIdAt(int worldX, int worldY)
        => IsStageOne(worldX, worldY) ? Stage1Wall : Stage2Wall;

    /// <summary>이 좌표에 바닥을 놓는다면 어떤 바닥인가(벽을 캔 자리도 이걸 쓴다).</summary>
    public static string FloorIdAt(int worldX, int worldY)
        => IsStageOne(worldX, worldY) ? Stage1Floor : Stage2Floor;
}
