/// <summary>
/// <b>어느 기계가 무엇을 태울 수 있고, 그것이 초당 얼마로 몇 초 타는지를 정하는 유일한 표.</b>
///
/// <see cref="ExtractionTable"/> · <see cref="CoreUpgradeTable"/> · <see cref="UndergroundLootTable"/> 과
/// 같은 꼴로 static 이다. 값이 전부 문자열과 숫자라 에셋을 가리킬 필요가 없기 때문이다
/// (<see cref="FluidColors"/> 가 static 인 것과 같은 이유 — 반대로 <see cref="MainBlock.footstepSound"/> 는
/// <c>AudioClip</c> 에셋을 가리켜야 해서 SO 필드다).
///
/// 이 표 하나가 네 가지를 함께 답한다:
/// <b>허용 목록</b>(행이 있는가) · <b>초당 발전량</b>(<see cref="Row.rate"/>) ·
/// <b>발전 시간</b>(<see cref="Row.seconds"/>) · <b>총 에너지</b>(둘의 곱).
///
/// ⚠ <b>표에 그 기계의 행이 하나도 없으면 지금까지와 완전히 같이 돈다</b> —
/// 연료 판정은 <see cref="Items.IsFuel"/>, 총량은 <see cref="Items.burnEnergy"/>,
/// 초당 소비는 <see cref="MachineBlock.fuelBurnRate"/>. 그래서 <b>화로와 지열 발전기는 한 줄도 안 바뀐다.</b>
/// 이 폴백이 없으면 표에 안 적힌 기계가 조용히 죽는다.
/// </summary>
public static class FuelTable
{
    /// <summary>표 한 줄. 총 에너지는 적지 않는다 — <c>rate × seconds</c> 로 파생되므로 둘이 어긋날 수 없다.</summary>
    public struct Row
    {
        /// <summary>이 연료를 받는 기계(<see cref="BlockBase.BlockName"/>).</summary>
        public string machineId;

        /// <summary>연료 이름. 아이템이면 <c>Items.itemName</c>, 유체면 <c>FluidDefine.fluidId</c>.</summary>
        public string fuelName;

        /// <summary>유체 연료인가(가스 발전기). 아이템 연료와 이름 공간이 겹칠 수 있어 함께 본다.</summary>
        public bool isFluid;

        /// <summary>한 번 점화에 소모하는 양. 아이템은 개수(보통 1), 유체는 단위(보통 1000 = 한 양동이).</summary>
        public int amount;

        /// <summary>초당 발전량(= 초당 태우는 에너지).</summary>
        public float rate;

        /// <summary>이 <see cref="amount"/> 한 묶음이 타는 시간(초).</summary>
        public float seconds;

        public Row(string machineId, string fuelName, bool isFluid, int amount, float rate, float seconds)
        {
            this.machineId = machineId;
            this.fuelName = fuelName;
            this.isFluid = isFluid;
            this.amount = amount;
            this.rate = rate;
            this.seconds = seconds;
        }

        /// <summary>이 한 묶음이 내는 총 에너지.</summary>
        public float TotalEnergy => rate * seconds;
    }

    /// <summary>
    /// ⚠ <b>연료 이름은 언제나 영문 snake_case 다</b>(<c>itemName</c>/<c>fluidId</c> 규약).
    /// 한글이나 옛 이름을 적으면 조용히 안 맞는다 — 부르는 쪽이 먼저 <see cref="ItemAliases"/> 로 풀어야 한다.
    ///
    /// ⚠ <b>같은 연료를 두 기계가 다른 값으로 태울 수 있다.</b> 그것이 이 표를 (기계 × 연료)로 둔 이유다 —
    /// 지금은 석탄을 화력 발전기만 태우지만, 화로 행을 더하면 화로에서만 다른 값이 되게 할 수도 있다.
    /// </summary>
    public static readonly Row[] Rows = new Row[]
    {
        // 핵발전소 — 핵연료봉만. 3000/s × 60초 = 180000
        new Row("Machine:NuclearPlant",     "nuclear_fuel_rod", false,    1, 3000f, 60f),

        // 화력 발전기 — 석탄·갈탄만. 초당 발전량은 같고 <b>지속 시간으로 갈린다</b>
        new Row("Machine:ThermalGenerator", "coal",             false,    1,  200f, 20f),
        new Row("Machine:ThermalGenerator", "brown_coal",       false,    1,  200f, 10f),

        // 가스 발전기 — 원유·가솔린만(유체). 정제하면 두 배로 탄다.
        // 원유 1000 = 6000, 증류해서 얻는 가솔린 500 = 6000 이라 총량은 같고 <b>초당 출력이 두 배</b>다
        // (증류 전력 200 을 빼면 순이득은 여기서 나오지 않는다 — 정제의 값어치는 속도다).
        new Row("Machine:GasGenerator",     "crude_oil",         true, 1000,  300f, 20f),
        new Row("Machine:GasGenerator",     "gasoline",          true, 1000,  600f, 20f),
    };

    /// <summary>이 기계가 표에 한 줄이라도 있는가. <b>false 면 예전 방식으로 돈다.</b></summary>
    public static bool HasAnyRow(string machineId)
    {
        for (int i = 0; i < Rows.Length; i++)
            if (Rows[i].machineId == machineId) return true;
        return false;
    }

    /// <summary>이 기계가 <b>유체를 태우는가</b>. 발전기의 점화 경로를 가르는 판정이다.</summary>
    public static bool HasFluidRow(string machineId)
    {
        for (int i = 0; i < Rows.Length; i++)
            if (Rows[i].isFluid && Rows[i].machineId == machineId) return true;
        return false;
    }

    /// <summary>(기계, 연료) 한 줄을 찾는다.</summary>
    public static bool TryGet(string machineId, string fuelName, bool isFluid, out Row row)
    {
        for (int i = 0; i < Rows.Length; i++)
        {
            if (Rows[i].isFluid != isFluid) continue;
            if (Rows[i].machineId != machineId || Rows[i].fuelName != fuelName) continue;
            row = Rows[i];
            return true;
        }
        row = default;
        return false;
    }

    /// <summary>이 기계가 이 아이템을 연료로 받는가.</summary>
    public static bool AcceptsItem(string machineId, string itemName) => TryGet(machineId, itemName, false, out _);

    /// <summary>이 기계가 이 유체를 연료로 받는가.</summary>
    public static bool AcceptsFluid(string machineId, string fluidId) => TryGet(machineId, fluidId, true, out _);
}
