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
        { "enchanted_conductor_powder", 1 },   // 마법이 부여된 전도체 가루 — 1차 · 초급 재단(t0)
        { "mana_chip",                  2 },   // 마력 칩 — 2차 · 중급 재단(t1)
        { "resonance_chip",             3 },   // 공명 칩 — 3차 · 고급 재단(t2)
    };

    // ⚠ <b>승급 재료는 언제나 "한 티어 아래의 재단" 에 있어야 한다.</b>
    //    노션 정본은 공명 칩을 코어 조합기 3티어 레시피로 적었는데, 그러면
    //    <b>3티어가 되어야 3티어로 올릴 재료를 만들 수 있어</b> 영원히 잠긴다.
    //    1·2차가 이미 재단에 있는 것이 우연이 아니라 이 규칙이다 — 3차도 고급 재단(recipe.tier = 2)에 둔다.
    //
    // ⚠ 아래 비교는 <see cref="ItemDictionary.NormalizeName"/> 을 거치지 않는 <b>정확한 == 비교</b>다
    //    (ExtractionTable 과 비대칭이다). itemName 을 한 글자라도 다르게 적으면 조용히 -1 이 된다.

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
