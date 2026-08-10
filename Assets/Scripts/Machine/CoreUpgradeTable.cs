/// <summary>
/// 코어 조합기를 어떤 아이템으로 몇 티어까지 올릴 수 있는가. <see cref="ExtractionTable"/> 과 같은
/// <b>static 정본 표</b>다 — SO 로 빼면 "표와 에셋 중 어느 쪽이 정본인가" 가 흐려진다.
///
/// 코어의 티어는 SO(<see cref="MachineBlock.tier"/>)가 아니라 <see cref="PlaceableRecord.tier"/> 에 산다.
/// SO 를 런타임에 고치면 에디터에서 에셋이 <b>영구히</b> 바뀌고, 코어가 둘일 때 한쪽만 올릴 수도 없다.
/// </summary>
public static class CoreUpgradeTable
{
    /// <summary>{ 재료 itemName, 올라가는 티어 }. 티어는 결과값이지 증가량이 아니다.</summary>
    private static readonly object[,] Table =
    {
        { "enchanted_conductor_powder", 1 },   // 마법이 부여된 전도체 가루 — 1차
        { "mana_chip",                  2 },   // 마력 칩 — 2차
        // 3차(공명 칩 → 3)는 3티어 기획이 확정되면 여기 한 줄 추가한다.
    };

    /// <summary>
    /// 이 아이템을 넣으면 코어가 몇 티어가 되는가. 업그레이드 재료가 아니면 -1.
    /// <b>현재 티어와 비교하는 것은 호출자의 몫이다</b> — 이미 그 티어 이상이면 넣어도 소용없다.
    /// </summary>
    public static int TargetTier(Items item)
    {
        if (item == null) return -1;

        for (int i = 0; i < Table.GetLength(0); i++)
            if ((string)Table[i, 0] == item.itemName) return (int)Table[i, 1];

        return -1;
    }

    /// <summary>이 아이템이 코어 업그레이드 재료인가(칸에 넣을 수 있는가).</summary>
    public static bool IsUpgradeItem(Items item) => TargetTier(item) >= 0;
}
