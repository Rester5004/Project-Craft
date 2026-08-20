using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 저장 네트워크 전체를 <b>한 목록</b>으로 보여 주는 가상 컨테이너.
/// <see cref="IItemContainer"/> 를 구현하므로 <see cref="ItemSlot"/> 드래그앤드롭이 그대로 붙는다 —
/// 새 입출력 UI 를 만들지 않아도 된다.
///
/// <b>칸이 진짜 저장소가 아니다.</b> 여기 있는 <see cref="ItemStack"/> 은 셀 내용을 비추는
/// <b>사본(view)</b> 이고, 정본은 드라이브에 꽂힌 <see cref="StorageCellInstance"/> 들이다.
/// 그래서 흐름이 이렇게 된다:
///
/// <list type="number">
/// <item><see cref="Rebuild"/> 가 셀에서 view 를 만들고 <b>스냅샷</b>을 남긴다</item>
/// <item><see cref="ItemSlot.OnDrop"/> 이 view 의 스택을 평소처럼 직접 고친다</item>
/// <item>OnDrop 이 끝나며 부르는 <see cref="NotifyChanged"/> 에서 <b>스냅샷과 비교해 셀에 반영</b>한다</item>
/// </list>
///
/// ⚠ 3번이 없으면 플레이어가 옮긴 것이 화면에서만 움직이고 셀은 그대로다.
/// <see cref="ItemSlot.OnDrop"/> 이 <b>양쪽 컨테이너 모두</b> NotifyChanged 를 부르는 것에 기대고 있다.
/// </summary>
public class NetworkContainer : IItemContainer
{
    private readonly List<ItemStack> view = new List<ItemStack>();

    // 스냅샷 — view 를 만든 직후의 내용. 이것과의 차이가 곧 "플레이어가 한 일" 이다.
    private readonly List<string> snapNames = new List<string>();
    private readonly List<int> snapCounts = new List<int>();

    // 네트워크 전체 집계(종류 → 개수). Rebuild 마다 다시 만든다 — 파생 상태를 캐시하지 않는다.
    private readonly List<string> totalNames = new List<string>();
    private readonly List<int> totalCounts = new List<int>();

    private readonly Vector2Int anchor;

    public int PageSize { get; private set; }
    public int Page { get; private set; }

    /// <summary>지금 네트워크에 있는 아이템 종류 수(페이지 계산용).</summary>
    public int TypeCount => totalNames.Count;

    public int PageCount => Mathf.Max(1, Mathf.CeilToInt(TypeCount / (float)PageSize));

    /// <summary>이 컨테이너가 붙은 네트워크. 죽었으면 null 이거나 <c>IsOnline == false</c>.</summary>
    public StorageNetwork Network => StorageNetwork.Of(anchor);

    public System.Action OnChanged;

    public NetworkContainer(Vector2Int terminalCell, int pageSize)
    {
        anchor = terminalCell;
        PageSize = Mathf.Max(1, pageSize);
        Rebuild();
    }

    // ── IItemContainer ──────────────────────────────────────

    public int Capacity => PageSize;

    public ItemStack GetStack(int index)
    {
        while (view.Count < PageSize) view.Add(new ItemStack());
        return view[index];
    }

    /// <summary>
    /// 이 칸이 이 아이템을 <b>몇 개까지</b> 받을 수 있는가 = 지금 담긴 것 + 네트워크의 남은 자리.
    ///
    /// ⚠ 정확해야 한다. <see cref="ItemSlot.OnDrop"/> 이 이 값으로 잘라 주기 때문에,
    /// 실제보다 크게 답하면 "넣었다고 했는데 셀에 안 들어간" 아이템이 생긴다.
    /// </summary>
    public int SlotCapacity(int index, Items item)
    {
        if (item == null) return 0;

        ItemStack current = GetStack(index);
        int here = current.item == item ? current.count : 0;
        return here + FreeFor(item.itemName);
    }

    public void NotifyChanged()
    {
        Reconcile();
        Rebuild();
        OnChanged?.Invoke();
    }

    // ── 페이지 ──────────────────────────────────────────────

    public void SetPage(int page)
    {
        Page = Mathf.Clamp(page, 0, PageCount - 1);
        Rebuild();
        OnChanged?.Invoke();
    }

    // ── 저장은 네트워크가 한다 ──────────────────────────────
    // ⚠ 넣고 빼는 규칙을 여기 복사해 두면 안 된다 — 버스(StorageNetwork.Pump)와 갈라져
    //    "터미널로는 들어가는데 파이프로는 안 들어가는" 상태가 된다. 정본은 StorageNetwork 하나다.

    private int FreeFor(string itemName)
    {
        StorageNetwork net = Network;
        return net != null ? net.FreeFor(itemName) : 0;
    }

    public int Insert(string itemName, int amount)
    {
        StorageNetwork net = Network;
        return net != null ? net.Insert(itemName, amount) : 0;
    }

    public int Remove(string itemName, int amount)
    {
        StorageNetwork net = Network;
        return net != null ? net.Remove(itemName, amount) : 0;
    }

    public int CountOf(string itemName)
    {
        StorageNetwork net = Network;
        return net != null ? net.CountOf(itemName) : 0;
    }

    // ── view 만들기 · 되돌려 쓰기 ───────────────────────────

    /// <summary>셀에서 화면에 보일 목록을 다시 만든다. 스냅샷도 여기서 갱신한다.</summary>
    public void Rebuild()
    {
        StorageNetwork net = Network;
        if (net != null) net.Snapshot(totalNames, totalCounts);
        else { totalNames.Clear(); totalCounts.Clear(); }

        if (Page >= PageCount) Page = PageCount - 1;

        while (view.Count < PageSize) view.Add(new ItemStack());
        snapNames.Clear();
        snapCounts.Clear();

        int start = Page * PageSize;
        ItemDictionary dict = ItemDictionary.Instance;
        for (int i = 0; i < PageSize; i++)
        {
            int at = start + i;
            if (at >= totalNames.Count || dict == null)
            {
                view[i].Clear();
                snapNames.Add(null);
                snapCounts.Add(0);
                continue;
            }

            view[i].item = dict.GetItem(totalNames[at]);
            view[i].count = view[i].item != null ? totalCounts[at] : 0;
            view[i].instance = null;                     // 네트워크는 개체 데이터를 담지 않는다(아래 주석 참고)
            snapNames.Add(view[i].item != null ? totalNames[at] : null);
            snapCounts.Add(view[i].count);
        }
    }

    /// <summary>
    /// 스냅샷과 지금 view 의 차이를 셀에 반영한다.
    ///
    /// ⚠ <b>칸의 아이템이 바뀐 경우(교환)도 다뤄야 한다</b> — 옛것은 플레이어 손으로 갔으니 셀에서 빼고,
    /// 새것은 셀에 넣는다. 개수만 비교하면 교환에서 아이템이 복제되거나 사라진다.
    /// </summary>
    private void Reconcile()
    {
        for (int i = 0; i < PageSize && i < view.Count; i++)
        {
            string was = snapNames[i];
            int had = snapCounts[i];
            string now = view[i].item != null && view[i].count > 0 ? view[i].item.itemName : null;
            int has = now != null ? view[i].count : 0;

            if (was == now)
            {
                if (has > had) Fill(now, has - had, i, had);
                else if (has < had) Remove(was, had - has);
                continue;
            }

            if (was != null) Remove(was, had);          // 옛것은 밖으로 나갔다
            if (now != null) Fill(now, has, i, 0);      // 새것이 들어왔다
        }
    }

    /// <summary>
    /// 셀에 넣되 <b>못 넣은 만큼은 화면에 되돌려 둔다</b> — 그냥 버리면 아이템이 증발한다.
    /// <see cref="SlotCapacity"/> 가 정확하면 여기 걸릴 일이 없으므로, 걸리면 그쪽이 틀린 것이다.
    /// </summary>
    private void Fill(string itemName, int amount, int slot, int keep)
    {
        int put = Insert(itemName, amount);
        if (put == amount) return;

        Debug.LogError($"[NetworkContainer] '{itemName}' {amount}개 중 {put}개만 들어갔습니다 — "
                     + "SlotCapacity 가 실제 여유보다 크게 답하고 있습니다.");
        view[slot].count = keep + (amount - put);
    }

    /// <summary>저장 터미널은 종류를 가리지 않는다 — 못 넣는 것은 개체 데이터 쪽에서 걸린다.</summary>
    public bool AcceptsItem(int index, Items item) => true;
}
