using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 파이프 망 전체를 한 곳에서 굴린다.
///
/// 파이프 칸마다 MonoBehaviour 를 붙이면 수백 개의 Update 가 돌게 되므로,
/// 매니저 하나가 로드된 파이프를 대신 그리고 대신 돌본다.
///
/// 씬에 배선하지 않는다 — <see cref="MapGenerator"/> 가 런타임에 만든다
/// (PowerLinkMode 가 오버레이 타일맵을 런타임 생성한 것과 같은 규약). 덕분에 씬 파일을 건드리지 않는다.
/// </summary>
public class PipeNetworkManager : MonoBehaviour
{
    /// <summary>
    /// 파이프 타일맵의 정렬 순서. 기계와 같은 2 인데,
    /// 파이프와 기계는 셀당 배치물 1개 규칙 때문에 <b>같은 칸에 있을 수 없어</b> 겹치지 않는다.
    /// 벽 앞면(2)도 벽 칸에만 그려지고 파이프는 바닥 칸에만 놓이므로 마찬가지다.
    /// </summary>
    private const int PipeSortingOrder = 120;

    public static PipeNetworkManager Active { get; private set; }

    /// <summary>파이프를 그리는 전용 타일맵.</summary>
    public Tilemap PipeTilemap { get; private set; }

    /// <summary>배치·제거로 망이 바뀔 때마다 오른다. 캐시된 경로가 이 값과 다르면 다시 계산한다.</summary>
    public int TopologyVersion { get; private set; }

    private readonly Dictionary<Vector2Int, PipeCell> cells = new Dictionary<Vector2Int, PipeCell>();

    // 블록별 마스크 16칸 타일 캐시. 셀 수와 무관하게 (블록 종류 x 16)개만 만든다.
    private readonly Dictionary<PipeBlock, Tile[]> tileCache = new Dictionary<PipeBlock, Tile[]>();

    /// <summary>로드된 파이프 전부(월드 셀 → 상태).</summary>
    public IEnumerable<KeyValuePair<Vector2Int, PipeCell>> Cells => cells;

    /// <summary>씬을 건드리지 않고 매니저와 타일맵을 만든다. 이미 있으면 그대로 쓴다.</summary>
    public static void EnsureCreated(Transform gridParent, Sprite whitePixel, Material overlayMaterial)
    {
        if (Active != null) return;

        GameObject host = new GameObject("PipeNetwork");
        PipeNetworkManager manager = host.AddComponent<PipeNetworkManager>();

        GameObject tilemapGO = new GameObject("Pipes", typeof(Tilemap), typeof(TilemapRenderer));
        if (gridParent != null) tilemapGO.transform.SetParent(gridParent, false);

        manager.PipeTilemap = tilemapGO.GetComponent<Tilemap>();
        tilemapGO.GetComponent<TilemapRenderer>().sortingOrder = PipeSortingOrder;

        manager.overlay = host.AddComponent<PipeFaceOverlay>();
        manager.overlay.Reference = manager.PipeTilemap;
        // 면 막대는 안내 표시라 조명을 받지 않는다. 그림·머티리얼은 에셋에서 받아 넘긴다.
        manager.overlay.BarSprite = whitePixel;
        manager.overlay.OverlayMaterial = overlayMaterial;
    }

    private void Awake()
    {
        if (Active == null) Active = this;
    }

    private void OnDestroy()
    {
        if (Active == this) Active = null;
    }

    // ── 셀 등록 / 해제 ───────────────────────────────────────────

    /// <summary>청크가 로드되며 파이프가 나타났다.</summary>
    public void OnPipeLoaded(PipeCell cell)
    {
        if (cell == null) return;

        cells[cell.cell] = cell;
        TopologyVersion++;
        RefreshAround(cell.cell);
        overlayDirty = true;
    }

    /// <summary>청크가 언로드돼 파이프를 화면에서 내린다(레코드는 남는다).</summary>
    public void OnPipeUnloaded(Vector2Int cell)
    {
        if (cells.TryGetValue(cell, out PipeCell pipe))
        {
            pipe.WriteBack();
            cells.Remove(cell);
        }

        if (PipeTilemap != null) PipeTilemap.SetTile((Vector3Int)cell, null);
        TopologyVersion++;
        RefreshNeighbours(cell);
        overlayDirty = true;
    }

    public bool TryGet(Vector2Int cell, out PipeCell pipe) => cells.TryGetValue(cell, out pipe);

    /// <summary>
    /// 이 칸 주변의 연결이 바뀌었다(기계를 놓거나 캤을 때도 부른다 — 옆 파이프의 모양이 바뀐다).
    /// </summary>
    public void MarkTopologyDirty(Vector2Int cell)
    {
        TopologyVersion++;
        RefreshAround(cell);
        overlayDirty = true;
    }

    // ── 렌치: 연결면 설정 ───────────────────────────────────────

    /// <summary>
    /// 파이프 한 면의 상태를 다음 단계로 돌린다. 무엇으로 바뀌는지는 <b>이웃이 무엇인가</b>로 갈린다.
    ///
    ///   이웃이 같은 종류 파이프 → 끊김 ↔ 기본 (두 칸에 같은 값을 써 둔다)
    ///   이웃이 기계             → 기본 → 넣기(파랑) → 꺼내기(빨강) → 기본
    ///   그 밖(빈 칸·다른 종류)  → 아무 일도 하지 않는다
    ///
    /// 어느 면을 눌렀는지 고르는 것은 <see cref="PlayerInteraction"/> 의 몫이다 —
    /// 플레이어 입력 판별은 거기 한 곳에 모아 둔다.
    /// </summary>
    /// <returns>실제로 바뀌었으면 true.</returns>
    public bool CycleFace(Vector2Int cell, int dir)
    {
        if (dir < 0 || dir > 3) return false;
        if (!cells.TryGetValue(cell, out PipeCell pipe) || pipe.block == null) return false;
        if (pipe.record == null) return false;

        Vector2Int neighbourCell = cell + PipeRouter.Directions[dir];
        PipeBlock neighbourPipe = PipeRouter.PipeAt(neighbourCell);

        if (neighbourPipe != null && neighbourPipe.kind == pipe.block.kind)
        {
            PipeFaceMode next = PipeRouter.FaceOf(pipe.record, dir) == PipeFaceMode.Cut
                ? PipeFaceMode.Default
                : PipeFaceMode.Cut;

            PipeRouter.SetFace(pipe.record, dir, next);

            // 이웃에도 같은 값을 남긴다. 판정은 양쪽을 다 보므로 없어도 동작은 하지만,
            // 있어야 이 파이프를 캤을 때 이웃 쪽 표시를 지울 대상을 찾을 수 있다.
            PlaceableRecord other = WorldMap.Instance != null ? WorldMap.Instance.GetPlaceableAt(neighbourCell) : null;
            if (other != null) PipeRouter.SetFace(other, PipeRouter.Opposite(dir), next);

            MarkTopologyDirty(cell);
            return true;
        }

        if (PipeRouter.MachineAt(neighbourCell))
        {
            PipeFaceMode current = PipeRouter.FaceOf(pipe.record, dir);
            PipeFaceMode next = current == PipeFaceMode.Default ? PipeFaceMode.Insert
                : current == PipeFaceMode.Insert ? PipeFaceMode.Extract
                : PipeFaceMode.Default;

            PipeRouter.SetFace(pipe.record, dir, next);
            MarkTopologyDirty(cell);
            return true;
        }

        return false;
    }

    /// <summary>저장 직전에 실은 짐을 레코드로 동기화한다.</summary>
    public void FlushAll()
    {
        foreach (KeyValuePair<Vector2Int, PipeCell> pair in cells) pair.Value.WriteBack();
    }

    // ── 운송 ────────────────────────────────────────────────────

    /// <summary>한 프레임에 추출을 시도할 파이프 수. 전부 훑으면 파이프가 많을 때 프레임이 튄다.</summary>
    private const int SourcesPerFrame = 8;

    /// <summary>받아 줄 곳이 없을 때 다시 시도하기까지 기다리는 시간(초).</summary>
    private const float RetryDelay = 1f;

    private readonly List<Vector2Int> order = new List<Vector2Int>();   // 추출 순회용(재사용)
    private int sourceCursor;

    /// <summary>
    /// Tick 이 직접 굴리는 시계(초). Time.time 을 쓰지 않는 이유는 짐의 남은 시간과 <b>같은 시간축</b>이어야
    /// 하기 때문이다 — 그래야 프레임을 돌리지 않고 Tick(dt) 만으로도 동작이 재현된다.
    /// </summary>
    private float clock;

    private void Update() => Tick(Time.deltaTime);

    // 색 막대는 청크를 불러오는 동안 기계가 수십 개 스폰되며 계속 더러워진다.
    // 표시만 남겨 두고 프레임 끝에 한 번만 다시 만든다.
    private PipeFaceOverlay overlay;
    private bool overlayDirty;

    private void LateUpdate()
    {
        if (!overlayDirty || overlay == null) return;
        overlayDirty = false;
        overlay.Rebuild(cells);
    }

    /// <summary>프레임을 돌리지 않는 검증용. 게임에서는 <see cref="LateUpdate"/> 가 알아서 부른다.</summary>
    public void RebuildOverlayNow()
    {
        overlayDirty = false;
        if (overlay != null) overlay.Rebuild(cells);
    }

    /// <summary>
    /// 한 프레임 분의 운송.
    ///
    /// deltaTime 을 인자로 받는 공개 메서드로 둔 이유는 <b>프레임을 돌리지 않고도 검증</b>할 수 있게
    /// 하기 위해서다(백그라운드 에디터는 프레임이 진행되지 않는다).
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (cells.Count == 0) return;

        clock += deltaTime;
        DeliverAll(deltaTime);
        ExtractSome();
    }

    /// <summary>실린 짐의 남은 시간을 깎고, 다 되면 배달한다.</summary>
    private void DeliverAll(float deltaTime)
    {
        foreach (KeyValuePair<Vector2Int, PipeCell> pair in cells)
        {
            PipeCell pipe = pair.Value;
            if (pipe.parcel == null) continue;

            if (pipe.parcel.remaining > 0f)
            {
                pipe.parcel.remaining -= deltaTime;
                if (pipe.parcel.remaining > 0f) continue;
                pipe.parcel.remaining = 0f;
            }

            TryDeliver(pipe);
        }
    }

    /// <summary>도착할 시간이 된 짐을 대상 기계에 넣는다. 못 넣으면 그대로 기다린다.</summary>
    private void TryDeliver(PipeCell pipe)
    {
        MapGenerator map = MapGenerator.Active;
        if (map == null) return;

        Vector2Int dest = pipe.parcel.Destination;

        // 청크가 안 불려 있으면 "기계가 없다"고 단정할 수 없다 — 그냥 기다린다.
        if (!map.IsCellLoaded(dest)) return;

        // 짐이 날아가는 사이에 렌치로 길을 끊거나 뒤집었을 수 있다. 위상이 바뀌었을 때만 다시 확인한다.
        if (pipe.routeVersion != TopologyVersion)
        {
            PipeRouter.FindSinks(pipe.cell, pipe.block.kind, pipe.sinks);
            pipe.routeVersion = TopologyVersion;

            bool stillReachable = false;
            for (int i = 0; i < pipe.sinks.Count && !stillReachable; i++)
                stillReachable = pipe.sinks[i].machineCell == dest;

            if (!stillReachable) { Retarget(pipe); return; }
        }

        if (!map.TryGetMachineAt(dest, out MachineInstance target) || target == null)
        {
            Retarget(pipe);
            return;
        }

        if (pipe.parcel.IsFluid) { DeliverFluid(pipe, target); return; }

        Items item = ItemDictionary.Instance != null ? ItemDictionary.Instance.GetItem(pipe.parcel.itemName) : null;
        IList<ItemStack> slots = PipeRouter.TargetSlots(target, item);
        if (slots == null)
        {
            Retarget(pipe);
            return;
        }

        // 개체 데이터가 붙은 아이템(도구)은 고유 최대치를 쓰는 저장소에 넣지 않는다 —
        // 한 칸에 수천 개인데 인스턴스는 하나뿐이라 그 하나가 사라질 때 전부가 사라진다.
        if (pipe.parcel.instance != null && !target.AcceptsInstanceItems) { Retarget(pipe); return; }

        // 칸 상한은 대상 기계에 묻는다(저장소는 maxStack 이 아니다). CountFreeSpace 와 반드시 같은 값이어야 한다.
        int moved = RecipeSolver.AddItems(slots, item, pipe.parcel.count, pipe.parcel.instance,
                                          target.InputSlotCapacity(item));
        if (moved <= 0) return;   // 가득 찼다 — 여기서 기다린다(필드에 쏟지 않는다)

        // AddItems 는 알려 주지 않는다. 직접 불러야 UI 가 갱신되고 기계가 새 레시피를 다시 찾는다.
        target.inventory.NotifyChanged();
        target.Flush();

        pipe.parcel.count -= moved;
        if (pipe.parcel.count <= 0) pipe.parcel = null;
        else pipe.parcel.instance = null;   // 개체 데이터는 들어간 쪽이 가져갔다

        pipe.WriteBack();
    }

    /// <summary>유체 짐을 대상 기계의 입력 탱크에 붓는다. 아이템 쪽과 계약이 같다(못 넣으면 기다린다).</summary>
    private void DeliverFluid(PipeCell pipe, MachineInstance target)
    {
        FluidDefine fluid = ItemDictionary.Instance != null ? ItemDictionary.Instance.GetFluid(pipe.parcel.fluidId) : null;
        IList<FluidStack> tanks = PipeRouter.TargetTanks(target, fluid);
        if (tanks == null)
        {
            Retarget(pipe);
            return;
        }

        int moved = RecipeSolver.AddFluid(tanks, fluid, pipe.parcel.amount, target.MaxFluid);
        if (moved <= 0) return;   // 탱크가 찼다 — 짐을 든 채 기다린다

        // AddFluid 도 통지하지 않는다. NotifyFluidChanged 가 UI 갱신 + 레시피 재탐색 + Flush 를 한다.
        target.NotifyFluidChanged();

        pipe.parcel.amount -= moved;
        if (pipe.parcel.amount <= 0) pipe.parcel = null;

        pipe.WriteBack();
    }

    /// <summary>도착지가 사라졌을 때 같은 짐으로 다른 기계를 한 번 다시 찾는다.</summary>
    private void Retarget(PipeCell pipe)
    {
        ItemDictionary dictionary = ItemDictionary.Instance;
        Items item = !pipe.parcel.IsFluid && dictionary != null ? dictionary.GetItem(pipe.parcel.itemName) : null;
        FluidDefine fluid = pipe.parcel.IsFluid && dictionary != null ? dictionary.GetFluid(pipe.parcel.fluidId) : null;
        if (item == null && fluid == null) return;

        PipeRouter.FindSinks(pipe.cell, pipe.block.kind, pipe.sinks);
        pipe.routeVersion = TopologyVersion;

        MapGenerator map = MapGenerator.Active;
        for (int i = 0; i < pipe.sinks.Count; i++)
        {
            Vector2Int cell = pipe.sinks[i].machineCell;
            if (!map.IsCellLoaded(cell)) continue;
            if (!map.TryGetMachineAt(cell, out MachineInstance machine) || machine == null) continue;
            if (fluid != null ? PipeRouter.TargetTanks(machine, fluid) == null
                              : PipeRouter.TargetSlots(machine, item) == null) continue;
            // 개체 데이터가 붙은 짐은 고유 최대치 저장소로 다시 보내지 않는다(TryDeliver 와 같은 규약).
            if (pipe.parcel.instance != null && !machine.AcceptsInstanceItems) continue;

            pipe.parcel.destX = cell.x;
            pipe.parcel.destY = cell.y;
            pipe.WriteBack();
            return;
        }
        // 갈 곳이 없으면 짐을 든 채로 기다린다. 회수는 파이프를 캘 때만 한다.
    }

    /// <summary>이번 프레임 몫의 파이프에서 추출을 시도한다.</summary>
    private void ExtractSome()
    {
        order.Clear();
        foreach (KeyValuePair<Vector2Int, PipeCell> pair in cells) order.Add(pair.Key);
        if (order.Count == 0) return;

        int tries = Mathf.Min(SourcesPerFrame, order.Count);
        for (int i = 0; i < tries; i++)
        {
            sourceCursor = (sourceCursor + 1) % order.Count;
            if (cells.TryGetValue(order[sourceCursor], out PipeCell pipe)) TryExtract(pipe);
        }
    }

    /// <summary>
    /// 옆 기계의 출력칸(또는 출력 탱크)에서 짐을 하나 싣는다.
    ///
    /// 아이템과 유체는 <b>싣는 대상만</b> 다르고 경로 탐색·라운드로빈·면 규칙은 완전히 같다.
    /// 그래서 <see cref="ParcelRecord"/> 한 종류로 두고 여기서만 갈라진다.
    /// </summary>
    private void TryExtract(PipeCell pipe)
    {
        if (pipe.parcel != null) return;                       // 한 칸에 짐 하나
        if (pipe.block == null) return;
        if (clock < pipe.nextAttemptTime) return;

        MapGenerator map = MapGenerator.Active;
        if (map == null) return;

        if (!pipe.block.CarriesItems) { TryExtractFluid(pipe, map); return; }

        // 출발 기계 찾기 — N, E, S, W 고정 순서라 결정적이다.
        MachineInstance source = null;
        Vector2Int sourceCell = default;
        ItemStack stack = null;
        for (int d = 0; d < PipeRouter.Directions.Length && source == null; d++)
        {
            Vector2Int cell = pipe.cell + PipeRouter.Directions[d];
            if (!map.TryGetMachineAt(cell, out MachineInstance machine) || machine == null) continue;
            if (machine.inventory == null) continue;

            // 넣기 전용(파랑) 면으로는 꺼내지 않는다. 끊긴 면도 마찬가지다.
            // <b>저장소는 꺼내기 면(빨강)일 때만</b> 꺼낼 수 있어서 기계를 찾은 뒤에 판정한다.
            if (!PipeRouter.CanExtractFrom(machine, PipeRouter.FaceOf(pipe.record, d))) continue;

            // 일반 기계는 출력칸, 저장소는 저장칸 전부.
            IList<ItemStack> from = PipeRouter.SourceSlots(machine);
            if (from == null) continue;

            for (int s = 0; s < from.Count; s++)
            {
                ItemStack candidate = from[s];
                if (candidate == null || candidate.item == null || candidate.count <= 0) continue;
                source = machine;
                // ⚠ 이웃 칸(cell)이 아니라 <b>기계의 기준점</b>을 쓴다. 여러 칸 기계는 파이프가 닿은
                // 칸과 기준점이 다른데, FindSinks 의 도착지는 기준점으로 정규화돼 있어
                // 이웃 칸을 그대로 두면 아래 자기 급전 가드가 뚫린다.
                sourceCell = machine.worldCell;
                stack = candidate;
                break;
            }
        }
        if (source == null) return;

        if (pipe.routeVersion != TopologyVersion)
        {
            // 출발 기계를 여기서 빼면 안 된다 — 이 목록은 TopologyVersion 하나를 키로 캐시돼
            // 배달 쪽과 함께 쓰이므로, 걸러 두면 다른 방향의 배달이 도착지를 잃는다.
            // 자기 산출물 회수 방지는 아래 루프의 sourceCell 검사가 맡는다.
            PipeRouter.FindSinks(pipe.cell, pipe.block.kind, pipe.sinks);
            pipe.routeVersion = TopologyVersion;
        }
        if (pipe.sinks.Count == 0) { pipe.nextAttemptTime = clock + RetryDelay; return; }

        // 커서에서 한 바퀴 — 받아 줄 수 있는 첫 기계로 보낸다.
        // 짐은 쪼갤 수 없으므로 "이번 짐은 이 기계, 다음 짐은 다음 기계" 가 곧 균등 분배다.
        int cursor = pipe.record != null ? pipe.record.roundRobinCursor : 0;
        if (cursor < 0 || cursor >= pipe.sinks.Count) cursor = 0;

        for (int i = 0; i < pipe.sinks.Count; i++)
        {
            int index = (cursor + i) % pipe.sinks.Count;
            PipeRouter.Sink sink = pipe.sinks[index];
            if (sink.machineCell == sourceCell) continue;
            if (!map.IsCellLoaded(sink.machineCell)) continue;
            if (!map.TryGetMachineAt(sink.machineCell, out MachineInstance target) || target == null) continue;

            IList<ItemStack> slots = PipeRouter.TargetSlots(target, stack.item);
            if (slots == null) continue;
            if (stack.instance != null && !target.AcceptsInstanceItems) continue;
            if (RecipeSolver.CountFreeSpace(slots, stack.item, stack.instance != null,
                                            target.InputSlotCapacity(stack.item)) <= 0) continue;

            int take = Mathf.Min(pipe.block.throughput, stack.count);
            if (stack.instance != null) take = 1;   // 개체 데이터가 붙은 짐(도구)은 하나씩

            pipe.parcel = new ParcelRecord
            {
                itemName = stack.item.itemName,
                count = take,
                instance = stack.instance,
                destX = sink.machineCell.x,
                destY = sink.machineCell.y,
                remaining = sink.seconds,
            };

            stack.count -= take;
            if (stack.count <= 0) stack.Clear();
            // 개체는 짐에 실려 갔다. 남은 스택이 같은 ToolInstance 객체를 계속 붙들고 있으면
            // 한쪽 내구도를 깎을 때 다른 쪽도 같이 닳는다. 배달 쪽(TryDeliver)과 같은 규약이다.
            else if (pipe.parcel.instance != null) stack.instance = null;
            source.inventory.NotifyChanged();
            source.Flush();

            if (pipe.record != null) pipe.record.roundRobinCursor = (index + 1) % pipe.sinks.Count;
            pipe.WriteBack();
            return;
        }

        pipe.nextAttemptTime = clock + RetryDelay;   // 아무도 안 받는다 — 잠시 쉬었다 다시 본다
    }

    /// <summary>
    /// 옆 기계의 <b>출력 탱크</b>에서 유체를 싣는다. 위 <see cref="TryExtract"/> 의 아이템 경로와
    /// 면 규칙·경로 캐시·라운드로빈이 완전히 같고, 다루는 자료형만 다르다.
    ///
    /// 파이프 종류(<see cref="PipeKind"/>)와 유체의 상(<see cref="FluidDefine.phase"/>)이 맞아야 싣는다 —
    /// 액체 파이프가 수소를 나르면 기체 파이프를 놓을 이유가 없어진다.
    /// </summary>
    private void TryExtractFluid(PipeCell pipe, MapGenerator map)
    {
        MachineInstance source = null;
        Vector2Int sourceCell = default;
        FluidStack tank = null;

        for (int d = 0; d < PipeRouter.Directions.Length && source == null; d++)
        {
            if (!PipeRouter.CanExtract(PipeRouter.FaceOf(pipe.record, d))) continue;

            Vector2Int cell = pipe.cell + PipeRouter.Directions[d];
            if (!map.TryGetMachineAt(cell, out MachineInstance machine) || machine == null) continue;
            if (machine.OutputTanks == null) continue;

            for (int t = 0; t < machine.OutputTanks.Count; t++)
            {
                FluidStack candidate = machine.OutputTanks[t];
                if (candidate == null || candidate.IsEmpty) continue;
                if (candidate.fluid.CarriedBy != pipe.block.kind) continue;   // 액체/기체 파이프를 가른다
                source = machine;
                sourceCell = machine.worldCell;   // 아이템 쪽과 같은 이유로 기준점을 쓴다
                tank = candidate;
                break;
            }
        }
        if (source == null) return;

        if (pipe.routeVersion != TopologyVersion)
        {
            PipeRouter.FindSinks(pipe.cell, pipe.block.kind, pipe.sinks);
            pipe.routeVersion = TopologyVersion;
        }
        if (pipe.sinks.Count == 0) { pipe.nextAttemptTime = clock + RetryDelay; return; }

        int cursor = pipe.record != null ? pipe.record.roundRobinCursor : 0;
        if (cursor < 0 || cursor >= pipe.sinks.Count) cursor = 0;

        for (int i = 0; i < pipe.sinks.Count; i++)
        {
            int index = (cursor + i) % pipe.sinks.Count;
            PipeRouter.Sink sink = pipe.sinks[index];
            if (sink.machineCell == sourceCell) continue;
            if (!map.IsCellLoaded(sink.machineCell)) continue;
            if (!map.TryGetMachineAt(sink.machineCell, out MachineInstance target) || target == null) continue;

            IList<FluidStack> tanks = PipeRouter.TargetTanks(target, tank.fluid);
            if (tanks == null) continue;
            if (RecipeSolver.CountFreeFluidSpace(tanks, tank.fluid, target.MaxFluid) <= 0) continue;

            int take = Mathf.Min(pipe.block.throughput, tank.amount);
            if (take <= 0) continue;

            pipe.parcel = new ParcelRecord
            {
                itemName = "",
                count = 0,
                fluidId = tank.fluid.fluidId,
                amount = take,
                destX = sink.machineCell.x,
                destY = sink.machineCell.y,
                remaining = sink.seconds,
            };

            tank.amount -= take;
            if (tank.amount <= 0) tank.Clear();
            source.NotifyFluidChanged();

            if (pipe.record != null) pipe.record.roundRobinCursor = (index + 1) % pipe.sinks.Count;
            pipe.WriteBack();
            return;
        }

        pipe.nextAttemptTime = clock + RetryDelay;
    }

    // ── 렌더 ────────────────────────────────────────────────────

    private void RefreshAround(Vector2Int cell)
    {
        RefreshTile(cell);
        RefreshNeighbours(cell);
    }

    private void RefreshNeighbours(Vector2Int cell)
    {
        for (int i = 0; i < PipeRouter.Directions.Length; i++)
            RefreshTile(cell + PipeRouter.Directions[i]);
    }

    /// <summary>한 칸의 연결 모양을 다시 계산해 그린다.</summary>
    public void RefreshTile(Vector2Int cell)
    {
        if (PipeTilemap == null) return;
        if (!cells.TryGetValue(cell, out PipeCell pipe) || pipe.block == null) return;

        pipe.mask = PipeRouter.ConnectionMask(cell, pipe.block.kind);
        PipeTilemap.SetTile((Vector3Int)cell, TileFor(pipe.block, pipe.mask));
    }

    /// <summary>
    /// (블록, 마스크) 조합의 타일. 회전이 필요한 마스크가 있어
    /// TilemapTextureLoader.CreateRuntimeTile 을 쓰지 못하고 여기서 따로 캐시한다.
    /// </summary>
    private Tile TileFor(PipeBlock block, int mask)
    {
        if (block == null || block.atlas == null) return null;

        if (!tileCache.TryGetValue(block, out Tile[] tiles))
        {
            tiles = new Tile[16];
            tileCache[block] = tiles;
        }

        if (mask < 0 || mask >= tiles.Length) return null;
        if (tiles[mask] != null) return tiles[mask];

        Sprite sprite = block.atlas.SpriteFor(mask);
        if (sprite == null) return null;

        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.color = block.tint;
        tile.transform = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, block.atlas.RotationFor(mask)));
        // LockTransform 이 없으면 리프레시 타이밍에 따라 회전이 사라진다(기본 flags 는 LockColor 뿐).
        tile.flags = TileFlags.LockTransform | TileFlags.LockColor;
        tile.colliderType = Tile.ColliderType.None;   // 파이프는 길을 막지 않는다

        tiles[mask] = tile;
        return tile;
    }
}
