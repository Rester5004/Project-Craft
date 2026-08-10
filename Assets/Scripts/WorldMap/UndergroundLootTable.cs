using System.Collections.Generic;

/// <summary>
/// 지하맵 바닥에 떨어져 있는 보상의 <b>정본 표</b>. <see cref="ExtractionTable"/> ·
/// <see cref="CoreUpgradeTable"/> 과 같은 꼴로 static 이다 — 밸런스는 반드시 여러 번 바뀌는데
/// SO 로 두면 표와 에셋이 갈라져 어느 쪽이 진짜인지 알 수 없게 된다.
///
/// 고정 드랍이 아니라 <b>칸마다 굴린다</b>: 후보 칸 하나에서 위에서부터 행을 훑어
/// 처음 확률에 맞은 행 하나만 놓는다(한 칸에 한 종류). 그래서 <b>위에 적힌 행일수록 자주 나온다</b>.
/// </summary>
public static class UndergroundLootTable
{
    /// <summary>표 한 줄. 개수는 <c>[minCount, maxCount]</c> 균등이다.</summary>
    public struct Row
    {
        public string itemName;
        public float chance;     // 이 칸에서 이 행이 뽑힐 확률 (0~1)
        public int minCount;
        public int maxCount;
        public int minTier;      // 이 등급 이상의 탐지기로 들어와야 나온다

        public Row(string itemName, float chance, int minCount, int maxCount, int minTier)
        {
            this.itemName = itemName;
            this.chance = chance;
            this.minCount = minCount;
            this.maxCount = maxCount;
            this.minTier = minTier;
        }
    }

    /// <summary>
    /// ⚠ <c>iron_ingot</c> 를 빼면 <b>0티어가 통째로 막힌다</b> — 양동이 ← 철판 ← 철 주괴 사슬이
    /// 지하맵 말고는 시작점이 없고, 물·벽돌·화로·압연기가 전부 그 뒤에 달려 있다.
    /// </summary>
    public static readonly Row[] Rows = new Row[]
    {
        new Row("mana_shard",        0.25f, 1, 3, 0),   // 마력 파편 — 0티어 마법의 유일한 시작점
        new Row("iron_ingot",        0.20f, 1, 4, 0),   // 철 주괴  — 0티어 사슬의 매듭
        new Row("copper_ingot",      0.20f, 1, 4, 0),   // 구리 주괴
        new Row("coal",              0.15f, 2, 5, 0),   // 석탄
        new Row("quartz_crystal",    0.10f, 1, 2, 0),   // 석영 결정
        new Row("conductor_powder", 0.08f, 1, 2, 0),   // 전도체 결정
    };

    /// <summary>이 등급으로 들어왔을 때 나올 수 있는 행들(표 순서 그대로).</summary>
    public static IEnumerable<Row> RowsFor(int tier)
    {
        foreach (Row row in Rows)
            if (row.minTier <= tier) yield return row;
    }
}
