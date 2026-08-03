using System.Collections.Generic;

/// <summary>
/// 통합된 옛 아이템 이름 → 정본 <c>itemName</c>.
///
/// <b>이 표 하나를 세 곳이 함께 본다</b> — 나뉘면 반드시 한쪽이 낡는다.
///   ① <see cref="ItemDictionary.GetItem"/> : 옛 세이브·인벤토리에 남은 이름을 읽을 때의 폴백
///   ② RecipeJsonImporter : JSON 재료 이름을 정본으로 돌려, 지운 플레이스홀더가 되살아나지 않게
///   ③ ItemMerger(에디터) : 실제로 에셋 참조를 갈아 끼울 때의 근거
///
/// ①이 있어서 <b>아이템을 지워도 세이브가 깨지지 않는다</b>. <c>itemName</c> 은 세이브 키라
/// 이 폴백이 유일한 안전망이다 — 표에서 줄을 지우면 그 이름이 든 옛 세이브는 그 칸을 잃는다.
///
/// 런타임 어셈블리에 둔다(에디터 전용이 아니다). 세이브를 읽는 것은 게임 쪽이기 때문.
/// </summary>
public static class ItemAliases
{
    /// <summary>옛 이름, 정본 이름 순의 짝. 근거는 _AuditReport.md 와 `자원과 그 가공방식.canvas`.</summary>
    private static readonly string[,] Table =
    {
        // 정본 기계가 이미 있는데 플레이스홀더가 따로 남은 것
        { "분쇄기", "Machine:ElectricPulverizer" },

        // 구역 티어 메인자원으로 통합 (0티어 = 돌 · 1티어 = 마력석 · 2티어 = 운석)
        { "암석", "돌" },
        { "광석", "마력석" },

        // 추출기 산출 금속은 '조각'. 재련하면 주괴가 된다(캔버스 범례 "재련 → 순수한 OO")
        { "철",     "raw_iron_ore" },
        { "구리",   "raw_copper_ore" },
        { "금",     "raw_gold_ore" },
        { "니켈",   "raw_nickel_ore" },
        { "주석",   "raw_tin_ore" },
        { "티타늄", "raw_titanium_ore" },

        // 표기 흔들림 — 띄어쓰기와 영문 병기
        { "금속판", "금속 판" },
        { "인바(invar) 판", "인바 판" },
        { "인바(invar)", "인바" },

        // 추출기 계열이 정식 기계가 되면서 흡수된 플레이스홀더 (ExtractorSetup 을 먼저 돌려야 한다)
        // 뭉뚱그린 옛 '추출기' 는 전력을 쓰는 첫 등급(0-1)으로 본다 — 옛 세이브에 놓여 있어도 안 사라지게.
        { "Machine:Extractor", "Machine:Extractor01" },
        { "수동 0-0티어 추출기", "Machine:Extractor00" },
        { "0-1티어 추출기",      "Machine:Extractor01" },
        { "0-2티어 추출기",      "Machine:Extractor02" },
        { "0-3티어 추출기",      "Machine:Extractor03" },
        { "0티어 자원 생성기",       "Machine:ResourceGenerator0" },
        { "0티어 자원 생성기(강화)", "Machine:ResourceGenerator0Plus" },
        { "펌프", "Machine:Pump" },

        // ── 한글 itemName → 영문 ID (표시명은 한글 그대로 남는다) ──────────────
        // itemName 은 세이브 키라 영어 규약이다. 아래는 그 규약을 어기고 있던 87개를 옮긴 것으로,
        // 옛 세이브가 한글 이름을 들고 있어도 이 표를 거쳐 읽힌다.

        // 광물 · 원료
        { "강철", "steel" },            { "청동", "bronze" },         { "인바", "invar" },
        { "실리콘", "silicon" },        { "금속", "metal" },          { "금속 주괴", "metal_ingot" },
        { "석회", "lime" },             { "시멘트", "cement" },       { "유리", "glass" },
        { "모래", "sand" },             { "자갈", "gravel" },         { "흙", "dirt" },
        { "벽돌", "brick" },            { "운석", "meteorite" },      { "유황석", "sulfur_ore" },
        { "파쇄 광석", "crushed_ore" }, { "광석 알갱이", "ore_grain" },
        { "조각난 돌덩이", "chipped_stone" }, { "부숴진 돌덩이", "broken_stone" },
        { "바스라진 돌덩이", "crumbled_stone" },
        { "반짝이는 돌", "shiny_stone" },     { "반짝이는 가루", "shiny_powder" },
        { "미가공 우라늄", "unrefined_uranium" }, { "우라늄 농축물", "uranium_concentrate" },

        // 판 · 부품
        { "철판", "iron_plate" },       { "구리판", "copper_plate" },   { "금 판", "gold_plate" },
        { "은 판", "silver_plate" },    { "청동 판", "bronze_plate" },  { "인바 판", "invar_plate" },
        { "실리콘 판", "silicon_plate" }, { "금속 판", "metal_plate" },
        { "철근", "rebar" },            { "철근 콘크리트", "reinforced_concrete" },
        { "크랭크", "crank" },          { "모터", "motor" },            { "베어링", "bearing" },
        { "프로펠러", "propeller" },    { "컴퓨터 칩", "computer_chip" },
        { "양동이", "bucket" },         { "유리 용기", "glass_container" },
        { "핵연료봉", "nuclear_fuel_rod" },

        // 가루 · 유체 · 전력
        { "전도체 가루", "conductor_powder" },
        { "마법이 부여된 전도체 가루", "enchanted_conductor_powder" },
        { "마법 가루", "magic_powder" }, { "황 가루", "sulfur_powder" },
        { "마력 파편", "mana_shard" },   { "마력 칩", "mana_chip" },
        { "물", "water" },              { "용암", "lava" },
        { "원유", "crude_oil" },        { "석유", "petroleum" },
        { "산성 용액", "acid_solution" }, { "저전압 전력", "low_voltage_power" },

        // 씨앗
        { "나무 씨앗", "tree_seed" },   { "사과 나무 씨앗", "apple_tree_seed" },

        // 도구 (도구 체계로 흡수는 별건 — 지금은 이름만 규약에 맞춘다)
        { "칼", "knife" },              { "돌 칼", "stone_knife" },   { "철 칼", "iron_knife" },
        { "칼날", "blade" },            { "돌 칼날", "stone_blade" }, { "철 칼날", "iron_blade" },
        { "다우징 로드", "dowsing_rod" }, { "0티어 다우징 로드", "dowsing_rod_t0" },
        { "업그레이드 모듈 - 속도", "upgrade_speed" },
        { "업그레이드 모듈 - 효율", "upgrade_efficiency" },

        // 아직 정식 기계가 아닌 기계 아이템 (승격되면 Machine:* 로 다시 옮긴다)
        { "수동 분쇄기", "manual_crusher" },   { "용광로", "blast_furnace" },
        { "자동 조합기", "auto_crafter" },     { "고급 조합기", "advanced_crafter" },
        { "가스 발전기", "gas_generator" },    { "증류기", "distiller" },
        { "수경 재배기", "hydroponics" },      { "아이템 강화기", "item_enhancer" },
        { "공동 탐색기", "cavity_scanner" },

        // 배치물 — 아이템 이름을 바꾸면 blockName 도 같이 바꿔야 한다(파이프 에셋 설정이 복사해 간다)
        { "돌", "stone" },              { "마력석", "manastone" },
        { "아이템 파이프", "item_pipe" },      { "고체 운송 파이프", "solid_pipe" },
        { "액체 파이프", "liquid_pipe" },      { "기체 파이프", "gas_pipe" },
        { "산성 파이프", "acid_pipe" },        { "유리 파이프", "glass_pipe" },

        // "이 중 아무거나" 를 뜻하는 가짜 아이템. 합치지 않기로 했으므로 넷 다 남기되 이름만 규약에 맞춘다.
        { "석탄 or 갈탄", "any_coal_lignite" },
        { "석탄 또는 갈탄", "any_coal_lignite_2" },
        { "석탄 / 갈탄 / 석유", "any_coal_lignite_oil" },
        { "석탄/갈탄/석유 중 1", "any_coal_lignite_oil_2" },
    };

    private static readonly Dictionary<string, string> map = new Dictionary<string, string>();

    static ItemAliases()
    {
        for (int i = 0; i < Table.GetLength(0); i++)
        {
            // 양쪽 다 NFC 로 맞춘다. 한글은 완성형/조합형이 겉보기엔 같아도 문자열 비교가 실패한다.
            string from = ItemDictionary.NormalizeName(Table[i, 0]);
            string to = ItemDictionary.NormalizeName(Table[i, 1]);
            if (!string.IsNullOrEmpty(from) && !map.ContainsKey(from)) map[from] = to;
        }
    }

    /// <summary>정본 이름(별칭이 아니면 넘긴 이름 그대로).</summary>
    public static string Resolve(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return itemName;
        return map.TryGetValue(ItemDictionary.NormalizeName(itemName), out string canonical) ? canonical : itemName;
    }

    /// <summary>이 이름이 통합돼 사라진 이름인가.</summary>
    public static bool IsAlias(string itemName)
        => !string.IsNullOrEmpty(itemName) && map.ContainsKey(ItemDictionary.NormalizeName(itemName));

    /// <summary>표 전체(옛 이름 → 정본). 통합 툴이 이걸 근거로 참조를 갈아 끼운다.</summary>
    public static IEnumerable<KeyValuePair<string, string>> All => map;

    public static int Count => map.Count;
}
