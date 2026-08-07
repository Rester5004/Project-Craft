/// <summary>
/// <b>어느 추출기가 무엇을 얼마나 잘 뽑는지를 정하는 유일한 표.</b>
///
/// 레시피(<see cref="ChanceOutput"/>)에는 그 산출물의 <b>가장 낮은 확률</b>만 적혀 있고,
/// 등급 차이는 전부 여기서 낸다 — 등급마다 레시피를 복제하면 <c>SelectRecipe</c> 가
/// "만들 수 있는 첫 레시피"만 골라 뒷쪽이 죽은 레시피가 되기 때문이다.
///
/// <b>표에는 "그 등급에서 처음 열리는 것"만 적는다.</b> 상위 등급은 같은 계열의 하위 줄을 자동으로
/// 물려받는다(캔버스의 "N티어 추출기는 0~N-1 의 결과물을 모두 가짐"). 12종 × 17산출물을 다 적으면
/// 유지가 안 되고, 무엇보다 상속 규칙이 표 어딘가에서 조용히 어긋난다.
///
/// 최종 확률 = <c>ChanceOutput.chance</c> × <see cref="Multiplier"/> × <c>MachineBlock.chanceMultiplier</c>.
/// 표에 없으면 배수 0 — <b>그 기계는 그 산출물을 아예 못 얻는다.</b>
///
/// 정본은 `자원과 그 가공방식.canvas` 다.
/// </summary>
public static class ExtractionTable
{
    private const string Prefix = "Machine:Extractor";

    /// <summary>{ 계열, 등급, 산출물 itemName } — 그 등급에서 <b>처음 열리는</b> 산출물.</summary>
    private static readonly string[,] Opens =
    {
        // ── 0계열 (메인자원 = 돌) ────────────────────────────────
        { "0", "0", "raw_iron_ore" },      { "0", "0", "raw_copper_ore" },
        { "0", "0", "brown_coal" },
        { "0", "0", "conductor_powder" },  { "0", "0", "bone_meal" },

        { "0", "1", "raw_gold_ore" },      { "0", "1", "raw_tin_ore" },
        { "0", "1", "coal" },              { "0", "1", "quartz_powder" },

        { "0", "2", "raw_nickel_ore" },    { "0", "2", "raw_osmium_ore" },
        { "0", "2", "raw_lead_ore" },      { "0", "2", "raw_silver_ore" },
        { "0", "2", "sulfur_ore" },

        { "0", "3", "raw_titanium_ore" },  { "0", "3", "raw_aluminum_ore" },
        // 화학 처리기가 이걸 우라늄 조각으로 바꾼다. 추출기가 완제품(조각)을 바로 주면
        // 화학 처리기를 건너뛰게 되므로 <b>추출 산출은 반드시 가공 전 단계</b>여야 한다.
        { "0", "3", "turbid_uranium" },

        // ── 1계열 (메인자원 = 마력석) ────────────────────────────
        { "1", "0", "ruby" },              { "1", "0", "sapphire" },
        { "1", "0", "conductor_crystal" },

        { "1", "1", "raw_uranium_ore" },   { "1", "1", "raw_lithium_ore" },
        { "1", "1", "raw_thorium_ore" },   { "1", "1", "diamond" },
        // 1-2 · 1-3 은 새 산출이 없다. 속도·확률 배수만 다르다(에셋의 speed/chanceMultiplier).

        // ── 2계열 (메인자원 = 운석) ──────────────────────────────
        { "2", "0", "diamond" },
        { "2", "0", "energy_crystal" },    { "2", "0", "magic_crystal" },
        // 2-1 · 2-2 · 2-3 도 새 산출 없음.
    };

    /// <summary>
    /// 기본 배수(1)에서 벗어나는 예외. { 계열, 등급, 산출물, 배수 } —
    /// 여기 적은 등급 <b>이상</b>에 적용된다(상속은 개방 목록과 같은 규칙).
    /// </summary>
    private static readonly string[,] Boosts =
    {
        // 캔버스: 전도체 결정이 1-0 은 5%, 1-1 은 10%. 레시피에는 최저치(5%)만 있으므로 여기서 두 배.
        { "1", "1", "conductor_crystal", "2" },
    };

    /// <summary>
    /// 이 기계가 이 산출물에 적용할 배수. <b>0 이면 얻을 수 없다.</b>
    /// 추출기가 아닌 기계는 언제나 1 — 확률 부산물을 가진 다른 기계가 생겨도 표 없이 동작하도록.
    /// </summary>
    public static float Multiplier(string blockId, Items item)
    {
        if (item == null || string.IsNullOrEmpty(item.itemName)) return 0f;
        if (!Parse(blockId, out int series, out int grade)) return 1f;   // 추출기가 아니면 그대로

        string name = ItemDictionary.NormalizeName(item.itemName);

        bool opened = false;
        for (int i = 0; i < Opens.GetLength(0); i++)
        {
            if (Opens[i, 0][0] - '0' != series) continue;
            if (Opens[i, 1][0] - '0' > grade) continue;                  // 아직 안 열린 등급
            if (ItemDictionary.NormalizeName(Opens[i, 2]) != name) continue;
            opened = true;
            break;
        }
        if (!opened) return 0f;

        // 예외 배수는 가장 높은(= 가장 최근에 열린) 등급의 것을 쓴다.
        float multiplier = 1f;
        int bestGrade = -1;
        for (int i = 0; i < Boosts.GetLength(0); i++)
        {
            if (Boosts[i, 0][0] - '0' != series) continue;
            int at = Boosts[i, 1][0] - '0';
            if (at > grade || at <= bestGrade) continue;
            if (ItemDictionary.NormalizeName(Boosts[i, 2]) != name) continue;
            if (!float.TryParse(Boosts[i, 3], out float v)) continue;
            multiplier = v;
            bestGrade = at;
        }
        return multiplier;
    }

    /// <summary>이 기계가 추출기인가(표의 적용 대상인가).</summary>
    public static bool IsExtractor(string blockId) => Parse(blockId, out _, out _);

    /// <summary>
    /// <c>Machine:Extractor23</c> → 계열 2 · 등급 3.
    /// <c>Machine:Extractor00Plus</c>(수동의 전기판)는 캔버스에 없으므로 <b>0-0 과 같게</b> 본다.
    /// </summary>
    private static bool Parse(string blockId, out int series, out int grade)
    {
        series = grade = 0;
        if (string.IsNullOrEmpty(blockId) || !blockId.StartsWith(Prefix)) return false;

        string rest = blockId.Substring(Prefix.Length);
        if (rest.Length < 2) return false;
        if (rest[0] < '0' || rest[0] > '9' || rest[1] < '0' || rest[1] > '9') return false;

        series = rest[0] - '0';
        grade = rest[1] - '0';
        return true;
    }
}
