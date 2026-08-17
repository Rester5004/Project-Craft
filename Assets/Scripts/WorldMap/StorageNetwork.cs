using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 데이터 케이블로 이어진 저장 네트워크 하나.
///
/// <b>저장하지 않는다.</b> 구성·용량은 전부 파생 상태라 <see cref="PipeNetworkManager.TopologyVersion"/>
/// 이 바뀔 때만 다시 계산한다(경로 캐시가 이미 쓰는 규약). 그래서 <b>세이브 포맷이 v12 그대로</b>고,
/// 케이블을 캐거나 렌치로 면을 바꿔도 다음 조회에서 저절로 맞는 답이 나온다.
///
/// 새로 만든 개념이 거의 없다 — 연결은 <see cref="PipeKind.Data"/>, 서브넷 분리는 렌치의
/// <see cref="PipeFaceMode.Cut"/>, 입출력 버스는 <see cref="PipeFaceMode.Insert"/>/<see cref="PipeFaceMode.Extract"/>,
/// 한계는 전력이다.
/// </summary>
public class StorageNetwork
{
    /// <summary>네트워크가 살아 있는가, 아니면 무엇 때문에 죽었는가.</summary>
    public enum State
    {
        Ok = 0,
        NoController = 1,          // 컨트롤러가 없다
        MultipleControllers = 2,   // 둘 이상 — 노션 설계상 그 네트워크는 통째로 멈춘다
        NoPower = 3,               // 컨트롤러에 전기가 안 들어온다
    }

    /// <summary>
    /// 케이블의 한 면에 붙은 입출력 버스. <b>별도의 버스 기계를 만들지 않는다</b> —
    /// 렌치로 지정한 <b>케이블 쪽 면</b>이 곧 버스다(노션 §3).
    /// </summary>
    public struct Bus
    {
        public Vector2Int cable;     // 버스가 달린 케이블 칸
        public int dir;              // 케이블에서 기계를 보는 방향
        public Vector2Int machine;   // 상대 기계의 <b>기준점</b>(발자국 정규화됨)
        public bool insert;          // true = 네트워크 → 기계 / false = 기계 → 네트워크
    }

    /// <summary>
    /// 위상만으로 정해지는 상태(컨트롤러 개수). <b>전력은 여기 넣지 않는다</b> — 아래 <see cref="Status"/> 참고.
    /// </summary>
    private State topology = State.NoController;

    /// <summary>
    /// ⚠ <b>전력은 캐시하지 않고 물어볼 때마다 다시 본다.</b>
    /// 위상(<see cref="PipeNetworkManager.TopologyVersion"/>)은 케이블을 놓거나 렌치를 쓸 때만 바뀌는데
    /// <b>전원은 매 프레임 바뀐다</b> — Build 시점에 굳혀 두면 발전기가 돌아와도 네트워크가 죽은 채로 남는다
    /// (실측으로 이 함정에 걸렸다: 컨트롤러가 먼저 켜지며 망을 캐시해 드라이브가 영영 0/1 이었다).
    /// </summary>
    public State Status => topology != State.Ok ? topology
                         : IsPowered(Controller) ? State.Ok : State.NoPower;

    public bool IsOnline => Status == State.Ok;

    /// <summary>컨트롤러 기준점. <see cref="Status"/> 가 <see cref="State.NoController"/> 면 뜻이 없다.</summary>
    public Vector2Int Controller { get; private set; }

    public readonly List<Vector2Int> Cables = new();
    public readonly List<Vector2Int> Drives = new();
    public readonly List<Vector2Int> Terminals = new();
    public readonly List<Bus> Buses = new();

    /// <summary>전기가 들어온 드라이브 수. <b>셀 때마다 다시 센다</b>(위 <see cref="Status"/> 와 같은 이유).</summary>
    public int PoweredDriveCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < Drives.Count; i++) if (IsPowered(Drives[i])) n++;
            return n;
        }
    }

    // ── 캐시 ────────────────────────────────────────────────
    // 네트워크에 속한 <b>모든</b> 칸(케이블·장치)이 같은 객체를 가리킨다. 어느 칸에서 물어도 답이 같다.
    private static readonly Dictionary<Vector2Int, StorageNetwork> cache = new();
    private static int cachedVersion = -1;

    /// <summary>
    /// 이 칸이 속한 네트워크. 케이블 칸도, 케이블에 붙은 장치 칸도 받는다.
    /// 어디에도 안 닿으면 null.
    /// </summary>
    public static StorageNetwork Of(Vector2Int cell)
    {
        EnsureFresh();
        if (cache.TryGetValue(cell, out StorageNetwork cached)) return cached;

        // 시작 케이블을 정한다. 장치 칸에서 물어보면 붙어 있는 케이블에서 출발한다.
        Vector2Int start;
        if (IsCable(cell)) start = cell;
        else if (!TryFindAdjacentCable(cell, out start)) return null;

        StorageNetwork network = Build(start);
        for (int i = 0; i < network.Cables.Count; i++) cache[network.Cables[i]] = network;
        for (int i = 0; i < network.Drives.Count; i++) cache[network.Drives[i]] = network;
        for (int i = 0; i < network.Terminals.Count; i++) cache[network.Terminals[i]] = network;
        if (network.topology != State.NoController) cache[network.Controller] = network;
        return network;
    }

    /// <summary>위상이 바뀌었으면 캐시를 통째로 버린다.</summary>
    private static void EnsureFresh()
    {
        int version = PipeNetworkManager.Active != null ? PipeNetworkManager.Active.TopologyVersion : 0;
        if (version == cachedVersion) return;
        cache.Clear();
        cachedVersion = version;
    }

    // ── 탐색 ────────────────────────────────────────────────

    private static readonly Queue<Vector2Int> frontier = new();
    private static readonly HashSet<Vector2Int> seen = new();

    private static StorageNetwork Build(Vector2Int start)
    {
        StorageNetwork net = new StorageNetwork();
        WorldMap map = WorldMap.Instance;
        if (map == null) { net.topology = State.NoController; return net; }

        int controllers = 0;
        frontier.Clear();
        seen.Clear();
        frontier.Enqueue(start);
        seen.Add(start);

        while (frontier.Count > 0)
        {
            Vector2Int cell = frontier.Dequeue();
            net.Cables.Add(cell);

            PlaceableRecord record = map.GetPlaceableAt(cell);
            for (int dir = 0; dir < 4; dir++)
            {
                // Cut 면은 <b>서브넷 경계</b>다. 케이블끼리든 장치든 여기서 끊긴다.
                PipeFaceMode mode = PipeRouter.FaceOf(record, dir);
                if (mode == PipeFaceMode.Cut) continue;

                Vector2Int next = cell + PipeRouter.Directions[dir];

                if (IsCable(next))
                {
                    // 끊김은 양쪽 레코드에 미러돼 있지만, 한쪽만 남는 경우에 대비해 반대편도 본다.
                    if (PipeRouter.FaceAt(next, PipeRouter.Opposite(dir)) == PipeFaceMode.Cut) continue;
                    if (seen.Add(next)) frontier.Enqueue(next);
                    continue;
                }

                Collect(net, map, cell, dir, next, mode, ref controllers);
            }
        }

        // 위상만 여기서 굳힌다. 전력은 Status 프로퍼티가 물어볼 때마다 다시 본다.
        net.topology = controllers == 0 ? State.NoController
                     : controllers > 1 ? State.MultipleControllers
                     : State.Ok;
        return net;
    }

    /// <summary>케이블 옆에 붙은 것 하나를 장치 또는 버스로 분류한다.</summary>
    private static void Collect(StorageNetwork net, WorldMap map, Vector2Int cable, int dir,
                                Vector2Int neighbor, PipeFaceMode mode, ref int controllers)
    {
        PlaceableRecord record = map.GetPlaceableAt(neighbor);
        if (record == null || string.IsNullOrEmpty(record.blockId)) return;

        // ⚠ <b>반드시 기준점으로 정규화한다.</b> 안 하면 여러 칸을 차지하는 기계가
        //    칸 수만큼 서로 다른 대상으로 세어진다(PipeRouter.AddSink · PowerLinkMode.AddLink 가
        //    걸렸던 것과 같은 함정 — 드라이브가 2×2 가 되는 순간 셀이 네 배로 보인다).
        Vector2Int origin = map.OriginAt(neighbor);

        BlockBase block = ItemDictionary.Instance != null ? ItemDictionary.Instance.GetBlock(record.blockId) : null;
        if (block is StorageNetworkBlock device)
        {
            switch (device.role)
            {
                case StorageNetworkRole.Controller:
                    controllers++;
                    net.Controller = origin;
                    return;
                case StorageNetworkRole.Drive:
                    if (!net.Drives.Contains(origin)) net.Drives.Add(origin);
                    return;
                default:
                    if (!net.Terminals.Contains(origin)) net.Terminals.Add(origin);
                    return;
            }
        }

        // 장치가 아닌 배치물은 <b>케이블 면이 Insert/Extract 일 때만</b> 버스가 된다.
        // Default 를 양방향으로 두면 상자 두 개를 이었을 때 아이템이 영원히 왕복한다
        // (저장 블록에서 이미 겪은 함정이라 같은 규칙을 쓴다).
        if (mode != PipeFaceMode.Insert && mode != PipeFaceMode.Extract) return;

        Bus bus;
        bus.cable = cable;
        bus.dir = dir;
        bus.machine = origin;
        bus.insert = mode == PipeFaceMode.Insert;
        net.Buses.Add(bus);
    }

    // ── 조회 도우미 ─────────────────────────────────────────

    /// <summary>이 칸이 데이터 케이블인가. 청크를 새로 만들지 않는 레코드 조회다.</summary>
    public static bool IsCable(Vector2Int cell)
    {
        WorldMap map = WorldMap.Instance;
        if (map == null) return false;

        PlaceableRecord record = map.GetPlaceableAt(cell);
        if (record == null || string.IsNullOrEmpty(record.blockId)) return false;
        if (ItemDictionary.Instance == null) return false;

        return ItemDictionary.Instance.GetBlock(record.blockId) is PipeBlock pipe && pipe.kind == PipeKind.Data;
    }

    private static bool TryFindAdjacentCable(Vector2Int cell, out Vector2Int cable)
    {
        for (int dir = 0; dir < 4; dir++)
        {
            Vector2Int next = cell + PipeRouter.Directions[dir];
            if (!IsCable(next)) continue;
            if (PipeRouter.FaceAt(next, PipeRouter.Opposite(dir)) == PipeFaceMode.Cut) continue;
            cable = next;
            return true;
        }
        cable = default;
        return false;
    }

    /// <summary>
    /// 이 장치에 전기가 들어와 있는가.
    ///
    /// <b>살아 있는 인스턴스만 본다</b> — 청크가 안 불려 있으면 인스턴스가 없고, 그때는 꺼진 것으로 친다.
    /// 네트워크를 쓰는 것은 터미널을 연 플레이어뿐이고 그 근방은 반드시 로드돼 있으므로 문제가 되지 않는다.
    /// </summary>
    private static bool IsPowered(Vector2Int cell)
    {
        MapGenerator map = MapGenerator.Active;
        if (map == null) return false;
        return map.TryGetMachineAt(cell, out MachineInstance machine) && machine != null && machine.IsRunning;
    }

    // ── 저장 (셀이 정본) ────────────────────────────────────
    // ⚠ <b>넣고 빼는 규칙은 여기 하나뿐이다.</b> 터미널(NetworkContainer)도 버스도 이 함수들을 부른다 —
    //    두 벌로 갈라 두면 "터미널로는 들어가는데 파이프로는 안 들어가는" 상태가 반드시 생긴다.

    /// <summary>
    /// 전기가 들어온 드라이브에 꽂힌 셀을 차례로 돌려준다.
    /// <b>꺼진 드라이브의 셀은 아예 보이지 않는다</b> — 전력이 곧 한계라는 규칙의 실제 모습이다.
    /// </summary>
    public IEnumerable<KeyValuePair<StorageCellItem, ItemStack>> Cells()
    {
        if (!IsOnline) yield break;

        MapGenerator map = MapGenerator.Active;
        if (map == null) yield break;

        for (int i = 0; i < Drives.Count; i++)
        {
            // ⚠ 전력은 여기서 <b>매번</b> 본다 — 캐시하면 발전기가 돌아와도 셀이 안 보인다.
            if (!IsPowered(Drives[i])) continue;
            if (!map.TryGetMachineAt(Drives[i], out MachineInstance drive) || drive == null) continue;
            if (drive.inventory == null) continue;

            List<ItemStack> slots = drive.inventory.inputSlots;
            for (int s = 0; s < slots.Count; s++)
            {
                ItemStack stack = slots[s];
                if (stack == null || stack.count <= 0) continue;
                if (!(stack.item is StorageCellItem cell)) continue;

                // 내용은 개체 데이터에 산다. 갓 만든 셀은 비어 있으므로 그때 만들어 붙인다.
                if (stack.instance == null) stack.instance = new StorageCellInstance();
                yield return new KeyValuePair<StorageCellItem, ItemStack>(cell, stack);
            }
        }
    }

    public int FreeFor(string itemName)
    {
        int free = 0;
        foreach (KeyValuePair<StorageCellItem, ItemStack> pair in Cells())
            free += ((StorageCellInstance)pair.Value.instance).FreeFor(itemName, pair.Key.typeLimit, pair.Key.totalLimit);
        return free;
    }

    public int CountOf(string itemName)
    {
        int sum = 0;
        foreach (KeyValuePair<StorageCellItem, ItemStack> pair in Cells())
            sum += ((StorageCellInstance)pair.Value.instance).CountOf(itemName);
        return sum;
    }

    /// <summary>
    /// 네트워크에 넣는다. <b>이미 그 종류를 담고 있는 셀부터</b> 채운다 —
    /// 빈 셀을 먼저 쓰면 종류 자리가 쓸데없이 쪼개진다.
    /// </summary>
    public int Insert(string itemName, int amount)
    {
        if (string.IsNullOrEmpty(itemName) || amount <= 0) return 0;

        int left = amount;
        for (int pass = 0; pass < 2 && left > 0; pass++)
        {
            foreach (KeyValuePair<StorageCellItem, ItemStack> pair in Cells())
            {
                if (left <= 0) break;

                StorageCellInstance content = (StorageCellInstance)pair.Value.instance;
                bool known = content.CountOf(itemName) > 0;
                if (pass == 0 != known) continue;   // 0회차는 이미 담긴 셀만, 1회차는 나머지

                left -= content.Insert(itemName, left, pair.Key.typeLimit, pair.Key.totalLimit);
            }
        }
        return amount - left;
    }

    /// <summary>네트워크에서 뺀다. 실제로 뺀 개수를 돌려준다.</summary>
    public int Remove(string itemName, int amount)
    {
        if (string.IsNullOrEmpty(itemName) || amount <= 0) return 0;

        int left = amount;
        foreach (KeyValuePair<StorageCellItem, ItemStack> pair in Cells())
        {
            if (left <= 0) break;
            left -= ((StorageCellInstance)pair.Value.instance).Remove(itemName, left);
        }
        return amount - left;
    }

    /// <summary>종류별 합계를 <b>넣은 순서대로</b> 모은다(터미널 목록이 프레임마다 뒤바뀌면 못 쓴다).</summary>
    public void Snapshot(List<string> names, List<int> counts)
    {
        names.Clear();
        counts.Clear();
        foreach (KeyValuePair<StorageCellItem, ItemStack> pair in Cells())
        {
            StorageCellInstance content = (StorageCellInstance)pair.Value.instance;
            for (int i = 0; i < content.Names.Count; i++)
            {
                int at = names.IndexOf(content.Names[i]);
                if (at < 0) { names.Add(content.Names[i]); counts.Add(content.Counts[i]); }
                else counts[at] += content.Counts[i];
            }
        }
    }

    // ── 입출력 버스 구동 ────────────────────────────────────

    /// <summary>버스 하나가 1초에 옮기는 개수. 파이프보다 빠르지만 무한은 아니다.</summary>
    private const float BusRate = 8f;

    private float busCredit;
    private static readonly List<string> pumpNames = new List<string>();
    private static readonly List<int> pumpCounts = new List<int>();

    /// <summary>
    /// <b>컨트롤러가 매 프레임 부른다</b>(<c>MachineInstance.Update</c> 의 상시 소비 분기).
    /// 네트워크당 컨트롤러가 하나뿐이라, 이렇게 하면 <b>한 네트워크가 정확히 한 번</b> 돈다 —
    /// 케이블마다 돌리면 칸 수만큼 빨라진다.
    /// </summary>
    public static void PumpBuses(Vector2Int controllerCell, float deltaTime)
    {
        StorageNetwork net = Of(controllerCell);
        if (net == null || !net.IsOnline || net.Buses.Count == 0) return;
        net.Pump(deltaTime);
    }

    private void Pump(float deltaTime)
    {
        busCredit += BusRate * deltaTime;
        int budget = Mathf.FloorToInt(busCredit);
        if (budget <= 0) return;
        busCredit -= budget;

        MapGenerator map = MapGenerator.Active;
        if (map == null) return;

        // ⚠ <b>예산은 버스마다 따로 준다.</b> 하나의 예산을 나눠 쓰면 목록에서 앞선 버스가 다 써 버려
        //    뒤쪽 버스가 영영 굶는다 — 실측으로 Extract 가 예산을 독차지해 Insert 가 한 개도 못 옮겼다.
        //    "버스 하나가 초당 BusRate 개" 라는 서술과도 이쪽이 맞는다.
        for (int i = 0; i < Buses.Count; i++)
        {
            Bus bus = Buses[i];
            if (!map.TryGetMachineAt(bus.machine, out MachineInstance machine) || machine == null) continue;

            if (bus.insert) PushTo(machine, budget);
            else PullFrom(machine, budget);
        }
    }

    /// <summary>네트워크 → 기계. <b>그 기계가 받을 수 있는 것만</b> 고른다(레시피 근거는 PipeRouter 가 안다).</summary>
    private int PushTo(MachineInstance machine, int budget)
    {
        Snapshot(pumpNames, pumpCounts);
        ItemDictionary dict = ItemDictionary.Instance;
        if (dict == null) return 0;

        int moved = 0;
        for (int i = 0; i < pumpNames.Count && moved < budget; i++)
        {
            Items item = dict.GetItem(pumpNames[i]);
            if (item == null) continue;

            IList<ItemStack> slots = PipeRouter.TargetSlots(machine, item);
            if (slots == null) continue;

            int want = Mathf.Min(budget - moved, pumpCounts[i]);
            int room = RecipeSolver.CountFreeSpace(slots, item);
            if (room <= 0) continue;

            int take = Remove(pumpNames[i], Mathf.Min(want, room));
            if (take <= 0) continue;

            int put = RecipeSolver.AddItems(slots, item, take);
            if (put < take) Insert(pumpNames[i], take - put);   // 못 넣은 만큼은 되돌린다 — 증발 금지
            if (put <= 0) continue;

            machine.inventory.NotifyChanged();
            machine.Flush();
            moved += put;
        }
        return moved;
    }

    /// <summary>기계 → 네트워크. 꺼낼 칸은 <see cref="PipeRouter.SourceSlots"/> 가 정한다.</summary>
    private int PullFrom(MachineInstance machine, int budget)
    {
        IList<ItemStack> slots = PipeRouter.SourceSlots(machine);
        if (slots == null) return 0;

        int moved = 0;
        for (int s = 0; s < slots.Count && moved < budget; s++)
        {
            ItemStack stack = slots[s];
            if (stack == null || stack.item == null || stack.count <= 0) continue;

            // ⚠ 개체 데이터가 붙은 것(도구·저장 셀)은 네트워크에 넣지 않는다 —
            //    셀은 itemName 만 세므로 재질·내구도·내용이 통째로 사라진다.
            if (stack.instance != null) continue;

            int take = Mathf.Min(budget - moved, stack.count);
            int put = Insert(stack.item.itemName, take);
            if (put <= 0) continue;

            stack.count -= put;
            if (stack.count <= 0) stack.Clear();
            moved += put;
        }

        if (moved > 0) { machine.inventory.NotifyChanged(); machine.Flush(); }
        return moved;
    }

    /// <summary>사람이 읽을 상태 문구(툴팁·디버그용).</summary>
    public string StatusText()
    {
        switch (Status)
        {
            case State.Ok: return $"정상 · 드라이브 {PoweredDriveCount}/{Drives.Count} · 터미널 {Terminals.Count} · 버스 {Buses.Count}";
            case State.NoController: return "저장 컨트롤러가 없다";
            case State.MultipleControllers: return "저장 컨트롤러가 둘 이상이다 — 네트워크가 멈춘다";
            default: return "전력이 끊겼다";
        }
    }
}
