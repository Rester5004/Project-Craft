using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유체 id → 화면에 쓸 색. <see cref="ExtractionTable"/> · <see cref="CoreUpgradeTable"/> 과 같은
/// <b>static 정본 표</b>다.
///
/// 색을 <see cref="FluidDefine"/> 에 두지 않는 이유:
/// ① 탱크 바·슬롯·파이프가 <b>유체 이름만</b> 주고받으면 그리는 쪽이 유체 에셋을 몰라도 된다.
/// ② 색은 데이터가 아니라 표현이라 한 곳에 모여 있어야 한 눈에 대비를 볼 수 있다.
/// ③ 에셋과 표에 색을 둘 다 두면 언젠가 한쪽만 고쳐진다.
///
/// 표에 없는 이름(또는 빈 탱크)은 <see cref="Unknown"/> 을 돌려주므로,
/// 새 유체를 만들고 여기 줄을 안 넣어도 화면이 깨지지 않고 <b>회색으로 티가 난다</b>.
/// </summary>
public static class FluidColors
{
    /// <summary>표에 없는 유체 · 빈 탱크의 색. 눈에 띄되 어떤 유체 색과도 안 겹치는 회색.</summary>
    public static readonly Color Unknown = new Color(0.45f, 0.45f, 0.50f, 1f);

    // 16진수로 적는다 — 색은 이렇게 봐야 서로 얼마나 다른지 바로 읽힌다.
    private static readonly string[,] Table =
    {
        { "water",         "#4080FF" },   // 물      파랑
        { "lava",          "#FF731A" },   // 용암    주황
        { "crude_oil",     "#261F1A" },   // 원유    검정에 가까운 갈색
        { "gasoline",      "#F2D159" },   // 가솔린  밝은 노랑
        { "acid_solution", "#BFF233" },   // 산성 용액 연두
        { "mana",          "#A659FF" },   // 마나    보라
        { "hydrogen",      "#D9E6FF" },   // 수소    아주 옅은 파랑
        { "oxygen",        "#99D9F2" },   // 산소    옅은 하늘
    };

    // 매 프레임 파싱하지 않도록 처음 한 번만 만든다.
    private static Dictionary<string, Color> cache;

    /// <summary>이 유체를 무슨 색으로 그릴 것인가. 빈 문자열·모르는 이름이면 <see cref="Unknown"/>.</summary>
    public static Color Of(string fluidId)
    {
        if (string.IsNullOrEmpty(fluidId)) return Unknown;

        EnsureCache();
        return cache.TryGetValue(fluidId, out Color color) ? color : Unknown;
    }

    /// <summary>표에 이 유체의 색이 적혀 있는가(검증 툴용).</summary>
    public static bool Has(string fluidId)
    {
        if (string.IsNullOrEmpty(fluidId)) return false;

        EnsureCache();
        return cache.ContainsKey(fluidId);
    }

    /// <summary>표에 적힌 유체 id 전부(검증 툴용).</summary>
    public static IEnumerable<string> Ids
    {
        get { EnsureCache(); return cache.Keys; }
    }

    private static void EnsureCache()
    {
        if (cache != null) return;

        cache = new Dictionary<string, Color>();
        for (int i = 0; i < Table.GetLength(0); i++)
        {
            string id = Table[i, 0];
            if (!ColorUtility.TryParseHtmlString(Table[i, 1], out Color color))
            {
                Debug.LogWarning($"[FluidColors] '{id}' 의 색 '{Table[i, 1]}' 을 읽지 못했습니다.");
                color = Unknown;
            }
            cache[id] = color;
        }
    }
}
