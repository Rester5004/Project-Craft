using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파이프 망을 훑는 순수 탐색기. MonoBehaviour 도 상태도 없어 에디터에서 그대로 불러 검증할 수 있다.
///
/// 위상은 <see cref="WorldMap.GetPlaceableAt"/> 로 <b>레코드에서</b> 읽는다.
/// MapGenerator 의 렌더 창과 무관하므로 망이 청크 경계에서 끊기지 않는다 —
/// 실제로 물건을 주고받는 순간에만 상대가 로드돼 있는지 따진다.
/// </summary>
public static class PipeRouter
{
    /// <summary>4방향. 마스크 비트는 N=1, E=2, S=4, W=8 순서다.</summary>
    public static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, 1),    // N
        new Vector2Int(1, 0),    // E
        new Vector2Int(0, -1),   // S
        new Vector2Int(-1, 0),   // W
    };

    /// <summary>도착 후보 하나. 기계까지 걸리는 총 이동 시간을 함께 들고 있다.</summary>
    public readonly struct Sink
    {
        public readonly Vector2Int machineCell;
        public readonly float seconds;

        public Sink(Vector2Int machineCell, float seconds)
        {
            this.machineCell = machineCell;
            this.seconds = seconds;
        }
    }

    // ── 면 상태 (렌치) ──────────────────────────────────────────
    //
    // 네 면을 PlaceableRecord.faceModes 1바이트에 2비트씩 담는다.
    // 시프트 규칙을 아는 곳은 이 세 함수뿐이어야 한다 — 흩어지면 반드시 한 군데가 어긋난다.

    /// <summary>반대쪽 방향 번호. <see cref="Directions"/> 가 N,E,S,W 순서라 2 를 더하면 된다.</summary>
    public static int Opposite(int dir) => (dir + 2) & 3;

    /// <summary>이 배치물의 <paramref name="dir"/> 면 상태. 레코드가 없으면 기본으로 본다.</summary>
    public static PipeFaceMode FaceOf(PlaceableRecord record, int dir)
    {
        if (record == null || dir < 0 || dir > 3) return PipeFaceMode.Default;
        return (PipeFaceMode)((record.faceModes >> (dir * 2)) & 0x3);
    }

    /// <summary>셀 좌표로 바로 묻는다(레코드를 꺼내 오는 수고를 줄인다). 청크를 새로 만들지 않는다.</summary>
    public static PipeFaceMode FaceAt(Vector2Int cell, int dir)
        => FaceOf(WorldMap.Instance != null ? WorldMap.Instance.GetPlaceableAt(cell) : null, dir);

    /// <summary>이 배치물의 <paramref name="dir"/> 면 상태를 바꾼다.</summary>
    public static void SetFace(PlaceableRecord record, int dir, PipeFaceMode mode)
    {
        if (record == null || dir < 0 || dir > 3) return;
        int shift = dir * 2;
        record.faceModes = (byte)((record.faceModes & ~(0x3 << shift)) | (((int)mode & 0x3) << shift));
    }

    /// <summary>이 셀에 놓인 파이프(파이프가 아니면 null). 청크를 새로 만들지 않는다.</summary>
    public static PipeBlock PipeAt(Vector2Int cell)
    {
        PlaceableRecord record = WorldMap.Instance != null ? WorldMap.Instance.GetPlaceableAt(cell) : null;
        if (record == null || ItemDictionary.Instance == null) return null;
        return ItemDictionary.Instance.GetPipeInfo(record.blockId);
    }

    /// <summary>이 셀에 기계가 놓여 있는가(로드 여부와 무관하게 레코드 기준).</summary>
    public static bool MachineAt(Vector2Int cell)
    {
        PlaceableRecord record = WorldMap.Instance != null ? WorldMap.Instance.GetPlaceableAt(cell) : null;
        if (record == null || ItemDictionary.Instance == null) return false;
        return ItemDictionary.Instance.GetMachineInfo(record.blockId) != null;
    }

    /// <summary>
    /// 이 셀이 <paramref name="kind"/> 파이프와 이어지는가.
    /// 같은 종류의 파이프끼리 이어지고(등급은 상관없다), 기계는 어느 면으로든 붙는다.
    /// </summary>
    public static bool Connects(Vector2Int cell, PipeKind kind)
    {
        PlaceableRecord record = WorldMap.Instance != null ? WorldMap.Instance.GetPlaceableAt(cell) : null;
        if (record == null || ItemDictionary.Instance == null) return false;

        PipeBlock pipe = ItemDictionary.Instance.GetPipeInfo(record.blockId);
        if (pipe != null) return pipe.kind == kind;

        // 기계는 방향을 고를 UI 가 없으므로 어느 면으로든 연결된다.
        return ItemDictionary.Instance.GetMachineInfo(record.blockId) != null;
    }

    /// <summary>
    /// <paramref name="from"/> 의 <paramref name="dir"/> 면이 렌치로 끊겨 있는가.
    ///
    /// <b>양쪽 레코드를 모두 본다.</b> 렌치는 끊을 때 두 칸에 같은 값을 써 두지만,
    /// 한쪽만 캤다가 다시 깔린 경우에도 남은 쪽 표시가 그대로 먹히게 하기 위해서다.
    /// </summary>
    public static bool IsCut(Vector2Int from, int dir)
    {
        if (dir < 0 || dir > 3) return false;
        if (FaceAt(from, dir) == PipeFaceMode.Cut) return true;
        return FaceAt(from + Directions[dir], Opposite(dir)) == PipeFaceMode.Cut;
    }

    /// <summary>이 파이프가 <paramref name="dir"/> 쪽 기계에 <b>넣어도</b> 되는가(꺼내기 전용 면이면 안 된다).</summary>
    public static bool CanInsert(PipeFaceMode mode)
        => mode == PipeFaceMode.Default || mode == PipeFaceMode.Insert;

    /// <summary>이 파이프가 <paramref name="dir"/> 쪽 기계에서 <b>꺼내도</b> 되는가(넣기 전용 면이면 안 된다).</summary>
    public static bool CanExtract(PipeFaceMode mode)
        => mode == PipeFaceMode.Default || mode == PipeFaceMode.Extract;

    /// <summary>
    /// 저장 기계(상자·아이템 저장소)는 <b>렌치로 방향을 지정해야만</b> 파이프가 손댈 수 있다.
    /// <c>Default</c> 면으로는 넣지도 빼지도 않는다.
    ///
    /// 일반 기계는 입력칸과 출력칸이 나뉘어 있어 방향이 저절로 정해지지만, 저장소는 한 칸이 둘을 겸한다.
    /// 그래서 Default 를 양방향으로 두면 <b>상자 두 개를 파이프로 이으면 아이템이 영원히 왕복한다.</b>
    /// 방향을 강제하면 그 고리가 구조적으로 생기지 않는다 —
    /// <c>Extract</c> 면인 저장소는 <see cref="CanInsertInto"/> 가 false 라 <see cref="FindSinks"/> 의 도착지에 오르지도 않는다.
    ///
    /// <b>넣기와 빼기 판정을 반드시 이 짝으로 함께 쓴다.</b> 한쪽만 고치면 방향이 어긋난다.
    /// 경로 탐색은 레코드만 보고(<see cref="StorageAt"/>) 배달·추출은 살아 있는 인스턴스를 보므로
    /// 규칙은 <c>bool isStorage</c> 를 받는 이 두 함수에 두고 나머지는 넘겨 주기만 한다.
    /// </summary>
    public static bool CanInsertInto(bool isStorage, PipeFaceMode mode)
        => isStorage ? mode == PipeFaceMode.Insert : CanInsert(mode);

    /// <inheritdoc cref="CanInsertInto(bool, PipeFaceMode)"/>
    public static bool CanExtractFrom(bool isStorage, PipeFaceMode mode)
        => isStorage ? mode == PipeFaceMode.Extract : CanExtract(mode);

    /// <inheritdoc cref="CanInsertInto(bool, PipeFaceMode)"/>
    public static bool CanInsertInto(MachineInstance machine, PipeFaceMode mode)
        => CanInsertInto(machine != null && machine.IsStorage, mode);

    /// <inheritdoc cref="CanInsertInto(bool, PipeFaceMode)"/>
    public static bool CanExtractFrom(MachineInstance machine, PipeFaceMode mode)
        => CanExtractFrom(machine != null && machine.IsStorage, mode);

    /// <summary>
    /// 이 셀의 배치물이 저장 블록인가. <b><see cref="MachineAt"/> 과 같은 규약으로 레코드만 본다</b> —
    /// 경로 탐색은 청크가 안 불려 있어도 같은 답을 내야 한다.
    /// </summary>
    public static bool StorageAt(Vector2Int cell)
    {
        PlaceableRecord record = WorldMap.Instance != null ? WorldMap.Instance.GetPlaceableAt(cell) : null;
        if (record == null || ItemDictionary.Instance == null) return false;
        return ItemDictionary.Instance.GetMachineInfo(record.blockId) is StorageBlock;
    }

    /// <summary>
    /// 파이프가 <b>꺼내 갈</b> 수 있는 칸. 일반 기계는 출력칸이고, 저장소는 저장칸(= 입력 구간) 전부다.
    /// 방향은 <see cref="CanExtractFrom"/> 이 이미 걸렀으므로 여기서는 어디를 볼지만 정한다.
    /// </summary>
    public static IList<ItemStack> SourceSlots(MachineInstance machine)
    {
        if (machine == null || machine.inventory == null) return null;
        return machine.IsStorage ? machine.inventory.inputSlots : machine.inventory.outputSlots;
    }

    /// <summary>
    /// 4방향 연결 마스크(N=1, E=2, S=4, W=8).
    /// 저장하지 않고 필요할 때마다 계산한다 — 파생 상태를 저장하면 이웃이 바뀔 때 어긋난다.
    ///
    /// 렌치로 끊은 면은 마스크에서 빠지므로, 스프라이트가 저절로 막힌 끝 모양이 된다.
    /// </summary>
    public static byte ConnectionMask(Vector2Int cell, PipeKind kind)
    {
        int mask = 0;
        for (int i = 0; i < Directions.Length; i++)
        {
            if (IsCut(cell, i)) continue;
            if (Connects(cell + Directions[i], kind)) mask |= 1 << i;
        }
        return (byte)mask;
    }

    // ── 경로 탐색 ───────────────────────────────────────────────

    /// <summary>한 번 탐색할 때 훑을 파이프 칸의 상한. 거대한 망에서 프레임이 튀지 않게 막는다.</summary>
    public const int MaxVisited = 512;

    // 매번 새로 할당하지 않도록 재사용하는 작업용 자료구조(탐색은 한 번에 하나만 돈다).
    private static readonly Dictionary<Vector2Int, float> best = new Dictionary<Vector2Int, float>();
    private static readonly List<Vector2Int> frontier = new List<Vector2Int>();

    /// <summary>
    /// 출발 파이프에서 같은 종류의 망을 따라 닿을 수 있는 기계를 <b>이동 시간 순</b>으로 모은다.
    ///
    /// 칸마다 <see cref="PipeBlock.secondsPerCell"/> 이 다르므로 BFS 가 아니라 다익스트라다
    /// (등급이 균일하면 결과가 BFS 와 같다). 비용에는 지나는 파이프 칸이 모두 들어가고,
    /// 기계로 나가는 마지막 한 걸음은 0 이다.
    ///
    /// <b>결과는 출발 파이프와 위상에만 의존한다</b> — 호출자에 따라 달라지는 조건(예: 짐을 꺼낸 기계 제외)을
    /// 여기에 넣으면 안 된다. 호출자들이 이 목록을 <c>TopologyVersion</c> 하나를 키로 캐시해 함께 쓰기 때문에,
    /// 한쪽 사정으로 걸러진 목록을 다른 쪽이 그대로 재사용해 <b>있는 도착지를 없다고 판단</b>하게 된다
    /// (실제로 기계 A↔파이프↔기계 B 왕복에서 한쪽 방향이 영영 죽었다).
    /// 자기 산출물 회수 방지 같은 <b>호출자 사정은 목록을 쓰는 자리에서</b> 거른다.
    /// </summary>
    public static void FindSinks(Vector2Int start, PipeKind kind, List<Sink> results)
    {
        results.Clear();
        best.Clear();
        frontier.Clear();

        PipeBlock startPipe = PipeAt(start);
        if (startPipe == null || startPipe.kind != kind) return;

        best[start] = startPipe.secondsPerCell;
        frontier.Add(start);

        int visited = 0;
        while (frontier.Count > 0 && visited < MaxVisited)
        {
            // 가장 싼 칸을 꺼낸다. 망이 작아 선형 탐색이 힙보다 빠르다.
            int pick = 0;
            for (int i = 1; i < frontier.Count; i++)
                if (best[frontier[i]] < best[frontier[pick]]) pick = i;

            Vector2Int cell = frontier[pick];
            frontier.RemoveAt(pick);
            float cost = best[cell];
            visited++;

            // 이 칸의 면 상태는 네 방향에서 공유하므로 레코드를 한 번만 꺼낸다.
            PlaceableRecord record = WorldMap.Instance != null ? WorldMap.Instance.GetPlaceableAt(cell) : null;

            for (int d = 0; d < Directions.Length; d++)
            {
                Vector2Int next = cell + Directions[d];
                PipeFaceMode face = FaceOf(record, d);
                if (face == PipeFaceMode.Cut || FaceAt(next, Opposite(d)) == PipeFaceMode.Cut) continue;

                PipeBlock pipe = PipeAt(next);
                if (pipe != null)
                {
                    if (pipe.kind != kind) continue;

                    float nextCost = cost + pipe.secondsPerCell;
                    if (best.TryGetValue(next, out float known) && known <= nextCost) continue;

                    best[next] = nextCost;
                    if (!frontier.Contains(next)) frontier.Add(next);
                    continue;
                }

                // 파이프가 아니면 기계인지 본다. 기계는 종점이라 더 뻗지 않는다.
                // 꺼내기 전용(빨강) 면으로는 넣을 수 없으므로 도착 후보에서 뺀다.
                // 저장 기계는 넣기 면(파랑)일 때만 도착지가 된다. "이웃이 저장소인가 + 그 면이 무엇인가" 는
                // 둘 다 위상이라 여기서 걸러도 캐시 계약을 깨지 않는다(면을 바꾸면 MarkTopologyDirty 가 돈다).
                if (MachineAt(next) && CanInsertInto(StorageAt(next), face)) AddSink(results, next, cost);
            }
        }

        results.Sort((a, b) => a.seconds.CompareTo(b.seconds));
    }

    /// <summary>
    /// 같은 기계에 여러 경로로 닿으면 가장 빠른 것만 남긴다.
    ///
    /// ⚠ <b>반드시 기준점 칸으로 정규화한다.</b> 여러 칸을 차지하는 기계는 파이프가 두 면에서 닿을 수 있는데,
    /// 칸을 그대로 두면 <b>한 기계가 서로 다른 도착지 둘</b>로 세어진다 — 라운드로빈 몫이 두 배가 되고,
    /// <c>PipeNetworkManager</c> 의 <c>sink.machineCell == sourceCell</c> 자기 급전 가드가 뚫려
    /// 기계가 자기 산출물을 자기 입력칸으로 되먹는다.
    /// </summary>
    private static void AddSink(List<Sink> results, Vector2Int machineCell, float seconds)
    {
        if (WorldMap.Instance != null) machineCell = WorldMap.Instance.OriginAt(machineCell);

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].machineCell != machineCell) continue;
            if (results[i].seconds <= seconds) return;

            results[i] = new Sink(machineCell, seconds);
            return;
        }
        results.Add(new Sink(machineCell, seconds));
    }

    /// <summary>
    /// 이 기계가 이 아이템을 받아 줄 슬롯(못 받으면 null).
    ///
    /// <b>레시피를 근거로 거르는 것이 핵심이다</b> — 이게 없으면 화로가 자기 산출물을
    /// 도로 입력칸에 받아 무한 고리가 생긴다.
    /// </summary>
    public static IList<ItemStack> TargetSlots(MachineInstance machine, Items item)
    {
        if (machine == null || item == null || machine.inventory == null) return null;

        // 저장소는 레시피가 없어 아래 필터를 타면 언제나 null 이다. 무엇이든 받는 것이 정체성이므로
        // 여기서 갈라 준다 — 방향(넣기만/빼기만)은 이미 렌치 면이 정했다(CanInsertInto).
        if (machine.IsStorage) return machine.inventory.inputSlots;

        // 연료는 연료 칸으로 (발전기·화로에 석탄을 자동 공급할 수 있게)
        if (machine.UsesFuel && item.IsFuel) return machine.inventory.fuelSlots;

        RecipeDictionary dictionary = RecipeDictionary.Instance;
        if (dictionary == null) return null;

        IReadOnlyList<Recipe> recipes = dictionary.GetRecipesFor(machine.RecipeKey);
        for (int i = 0; i < recipes.Count; i++)
        {
            Recipe recipe = recipes[i];
            // ⚠ 티어로 거르지 않는다 — 기계가 티어와 무관하게 전부 처리하므로
            //    (MachineInstance.SelectRecipe 주석 참고) 여기서 거르면
            //    "기계는 돌릴 수 있는데 파이프가 재료를 안 넣는" 상태가 된다.
            if (recipe == null || recipe.inputs == null) continue;

            for (int j = 0; j < recipe.inputs.Count; j++)
            {
                ItemStack need = recipe.inputs[j];
                if (need != null && need.item == item && need.count > 0) return machine.inventory.inputSlots;
            }
        }
        return null;
    }

    /// <summary>
    /// 이 기계가 이 유체를 받아 줄 탱크(못 받으면 null). <see cref="TargetSlots"/> 의 유체판이고
    /// <b>레시피를 근거로 거르는 것도 똑같다</b> — 이게 없으면 전기 분해기가 자기가 뽑은 수소를 도로 먹는다.
    /// </summary>
    public static IList<FluidStack> TargetTanks(MachineInstance machine, FluidDefine fluid)
    {
        if (machine == null || fluid == null || machine.InputTanks == null || machine.InputTanks.Count == 0) return null;

        RecipeDictionary dictionary = RecipeDictionary.Instance;
        if (dictionary == null) return null;

        IReadOnlyList<Recipe> recipes = dictionary.GetRecipesFor(machine.RecipeKey);
        for (int i = 0; i < recipes.Count; i++)
        {
            Recipe recipe = recipes[i];
            // 티어를 안 보는 이유는 TargetSlots 와 같다.
            if (recipe == null || recipe.fluidInputs == null) continue;

            for (int j = 0; j < recipe.fluidInputs.Count; j++)
            {
                FluidStack need = recipe.fluidInputs[j];
                if (need != null && need.fluid == fluid && need.amount > 0) return machine.InputTanks;
            }
        }
        return null;
    }
}
