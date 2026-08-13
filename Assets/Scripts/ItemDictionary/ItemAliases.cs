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

        // 추출기 계열이 정식 기계가 되면서 흡수된 플레이스홀더 (Machine:Extractor00~23 12종은 이미 다 있다)
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
        // 마력석 분쇄 사슬은 '마력석 조각 → 마력석 가루 → 미세한 마력석 가루' 로 이름을 통일했다
        // (운석 사슬과 같은 꼴). <b>Resolve 는 한 단계만 푸므로 한글 줄도 최종 이름을 직접 가리켜야 한다.</b>
        { "파쇄 광석", "manastone_shard" },      { "crushed_ore", "manastone_shard" },
        { "광석 알갱이", "manastone_dust" },     { "ore_grain", "manastone_dust" },
        // 돌덩이 3단 사슬은 '돌 → 자갈 → 모래 → 돌 가루' 로 통합됐다.
        // <b>분쇄 횟수가 같은 것끼리</b> 이어야 옛 세이브의 그 칸이 뜻을 잃지 않는다.
        { "조각난 돌덩이", "gravel" },   { "chipped_stone",  "gravel" },        // 1회 분쇄
        { "부숴진 돌덩이", "sand" },     { "broken_stone",   "sand" },          // 2회 분쇄
        { "바스라진 돌덩이", "stone_powder" }, { "crumbled_stone", "stone_powder" }, // 3회 분쇄
        { "반짝이는 가루", "manastone_fine_dust" }, { "shiny_powder", "manastone_fine_dust" },
        // '미가공 우라늄' 은 '탁한 우라늄' 으로 개명됐다 — 0-3 추출이 이걸 내고 화학 처리기가 우라늄 조각으로 바꾼다.
        { "미가공 우라늄", "turbid_uranium" }, { "unrefined_uranium", "turbid_uranium" },
        { "우라늄 농축물", "uranium_concentrate" },

        // 판 · 부품
        { "철판", "iron_plate" },       { "구리판", "copper_plate" },   { "금 판", "gold_plate" },
        { "은 판", "silver_plate" },    { "청동 판", "bronze_plate" },  { "인바 판", "invar_plate" },
        { "실리콘 판", "silicon_plate" }, { "금속 판", "metal_plate" },
        { "철근", "rebar" },            { "철근 콘크리트", "reinforced_concrete" },
        { "강화 합금", "reinforced_alloy" },
        // 노션이 '2티어 업그레이드 모듈' 로 부르던 것이다 — 지금 이름은 '기계 강화 모듈'.
        { "기계 강화 모듈", "machine_upgrade_module" },
        { "2티어 업그레이드 모듈", "machine_upgrade_module" },
        { "크랭크", "crank" },          { "모터", "motor" },            { "베어링", "bearing" },
        { "프로펠러", "propeller" },    { "컴퓨터 칩", "computer_chip" },
        { "양동이", "bucket" },         { "유리 용기", "glass_container" },
        { "핵연료봉", "nuclear_fuel_rod" },

        // 가루 · 유체 · 전력
        { "전도체 가루", "conductor_powder" },
        // '레드스톤' 은 '전도체' 의 옛 이름이다 — 그림까지 같은 완전한 중복이라 전도체로 합쳤다.
        // (2026-08-08 사용자 결정. 분쇄 레시피 하나가 이 둘만 잇고 있었다)
        { "레드스톤 결정", "conductor_crystal" }, { "redstone_crystal", "conductor_crystal" },
        { "레드스톤 가루", "conductor_powder" },  { "redstone_powder",  "conductor_powder" },
        { "전도체 결정", "conductor_crystal" },
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
        { "자동 조합기", "auto_crafter" },
        { "공동 탐색기", "cavity_scanner" },

        // ── 정식 기계로 승격된 것 (플레이스홀더 → Machine:*) ──────────────────
        // <b>Resolve 는 한 단계만 푼다.</b> 그래서 한글 줄도 최종 이름을 직접 가리켜야 한다 —
        // "가스 발전기" → "gas_generator" 로 두면 그 다음 단계를 안 밟아 죽은 이름이 된다.
        { "수동 분쇄기", "Machine:ManualPulverizer" }, { "manual_crusher", "Machine:ManualPulverizer" },
        { "가스 발전기", "Machine:GasGenerator" },   { "gas_generator", "Machine:GasGenerator" },
        { "용광로", "Machine:BlastFurnace" },        { "blast_furnace", "Machine:BlastFurnace" },
        // '정유기' 는 삭제하고 원유 처리를 '증류기' 하나로 모았다 — 옛 세이브에 놓여 있어도 안 사라지게 잇는다.
        { "증류기", "Machine:Distiller" },           { "distiller", "Machine:Distiller" },
        { "정유기", "Machine:Distiller" },           { "Machine:Refinery", "Machine:Distiller" },
        { "고급 조합기", "Machine:AdvancedCrafter" }, { "advanced_crafter", "Machine:AdvancedCrafter" },
        // 시멘트 공장·유리 가공기는 삭제하고 레시피 4개를 코어 조합기로 옮겼다(2026-08-10 사용자 결정).
        // 갈 곳이 없으므로 코어로 잇는다 — <b>이 줄이 없으면 이미 놓아 둔 배치물이 세이브에서 통째로 사라진다.</b>
        { "시멘트 공장", "CoreCrafter" }, { "Machine:CementPlant", "CoreCrafter" },
        { "유리 가공기", "CoreCrafter" }, { "Machine:GlassWorks", "CoreCrafter" },
        // 압연기는 압축기와 같은 기계였다 — 압축기를 정본으로 두고 흡수했다(2026-08-10 사용자 결정).
        // 가공 레시피 8개가 압축기로 넘어갔고, <b>이 줄이 없으면 이미 놓아 둔 압연기가 세이브에서 사라진다.</b>
        { "압연기", "Machine:Compressor" },          { "Machine:RollingMill", "Machine:Compressor" },
        { "압축기", "Machine:Compressor" },
        { "아이템 강화기", "Machine:ItemEnhancer" },  { "item_enhancer", "Machine:ItemEnhancer" },
        { "수경 재배기", "Machine:Hydroponics" },     { "hydroponics", "Machine:Hydroponics" },

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
