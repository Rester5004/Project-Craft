using System.Collections.Generic;
using System.IO;

/// <summary>
/// 저장 셀 하나가 <b>실제로 담고 있는 내용</b>. 스택에 붙는 개체 데이터라 셀을 캐면 내용이 그대로 딸려 온다.
///
/// <b>세이브 버전을 올리지 않는다</b> — <see cref="ItemInstanceSerializer"/> 가 어셈블리를 훑어
/// 자동 등록하고 길이 접두 방식이라, 종류가 하나 늘어도 기존 세이브가 그대로 읽힌다(v12 유지).
///
/// ⚠ <b>한도(종류·총개수)는 여기 두지 않는다.</b> 정본은 <see cref="StorageCellItem"/>(에셋)이고
/// 이 클래스의 메서드는 한도를 <b>인자로 받는다</b> — 복사해 두면 밸런스를 고쳤을 때
/// 이미 만들어진 셀만 옛 한도를 영원히 들고 다닌다.
///
/// ⚠ 내용은 아이템 <b>참조가 아니라 <c>itemName</c></b> 으로 들고 있다. 그것이 세이브 키이기 때문이고,
/// 에셋이 지워져도 <see cref="ItemAliases"/> 폴백으로 되살아날 여지가 남는다.
/// </summary>
public class StorageCellInstance : ItemInstance
{
    public override string TypeId => "storage_cell";

    /// <summary>담긴 것. <b>넣은 순서를 지킨다</b> — 터미널 목록이 프레임마다 뒤바뀌면 못 쓴다.</summary>
    private readonly List<string> names = new List<string>();
    private readonly List<int> counts = new List<int>();

    public int TypeCount => names.Count;

    public int Total
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < counts.Count; i++) sum += counts[i];
            return sum;
        }
    }

    public bool IsEmpty => names.Count == 0;

    public IReadOnlyList<string> Names => names;
    public IReadOnlyList<int> Counts => counts;

    public int CountOf(string itemName)
    {
        int i = IndexOf(itemName);
        return i < 0 ? 0 : counts[i];
    }

    private int IndexOf(string itemName)
    {
        for (int i = 0; i < names.Count; i++) if (names[i] == itemName) return i;
        return -1;
    }

    /// <summary>
    /// 이 셀이 <paramref name="itemName"/> 을 몇 개나 더 받을 수 있는가.
    /// <b>종류 한도가 개수 한도보다 먼저 걸린다</b> — 새 종류인데 자리가 없으면 0 이다.
    /// </summary>
    public int FreeFor(string itemName, int typeLimit, int totalLimit)
    {
        if (string.IsNullOrEmpty(itemName)) return 0;
        if (IndexOf(itemName) < 0 && names.Count >= typeLimit) return 0;

        int room = totalLimit - Total;
        return room > 0 ? room : 0;
    }

    /// <summary>넣을 수 있는 만큼만 넣고 <b>실제로 넣은 개수</b>를 돌려준다(부분 적재를 호출자가 알아야 한다).</summary>
    public int Insert(string itemName, int amount, int typeLimit, int totalLimit)
    {
        if (amount <= 0) return 0;

        int room = FreeFor(itemName, typeLimit, totalLimit);
        if (room <= 0) return 0;

        int put = amount < room ? amount : room;
        int i = IndexOf(itemName);
        if (i < 0) { names.Add(itemName); counts.Add(put); }
        else counts[i] += put;
        return put;
    }

    /// <summary>뺄 수 있는 만큼만 빼고 <b>실제로 뺀 개수</b>를 돌려준다. 0 이 되면 종류 자리도 비운다.</summary>
    public int Remove(string itemName, int amount)
    {
        if (amount <= 0) return 0;

        int i = IndexOf(itemName);
        if (i < 0) return 0;

        int take = amount < counts[i] ? amount : counts[i];
        counts[i] -= take;
        if (counts[i] <= 0) { names.RemoveAt(i); counts.RemoveAt(i); }
        return take;
    }

    public override ItemInstance Clone()
    {
        StorageCellInstance copy = new StorageCellInstance();
        copy.names.AddRange(names);
        copy.counts.AddRange(counts);
        return copy;
    }

    /// <summary>
    /// 내용이 완전히 같을 때만 합친다. 셀의 <c>maxStack</c> 이 1 이라 사실상 절대 합쳐지지 않는데,
    /// <b>그것이 맞다</b> — 내용이 다른 셀 둘이 한 칸이 되면 한쪽이 통째로 사라진다.
    /// </summary>
    public override bool Matches(ItemInstance other)
    {
        StorageCellInstance o = other as StorageCellInstance;
        if (o == null || o.names.Count != names.Count) return false;

        for (int i = 0; i < names.Count; i++)
            if (o.names[i] != names[i] || o.counts[i] != counts[i]) return false;

        return true;
    }

    public override void Write(BinaryWriter writer)
    {
        writer.Write(names.Count);
        for (int i = 0; i < names.Count; i++)
        {
            writer.Write(names[i]);
            writer.Write(counts[i]);
        }
    }

    public override void Read(BinaryReader reader)
    {
        names.Clear();
        counts.Clear();

        int n = reader.ReadInt32();
        for (int i = 0; i < n; i++)
        {
            string name = reader.ReadString();
            int count = reader.ReadInt32();
            if (string.IsNullOrEmpty(name) || count <= 0) continue;   // 깨진 줄은 조용히 버린다
            names.Add(name);
            counts.Add(count);
        }
    }

    /// <summary>
    /// 툴팁 한 줄. 한도는 아이템이 들고 있으므로 <b>여기서는 담긴 것만</b> 말한다 —
    /// 한도까지 보이려면 부르는 쪽이 <see cref="StorageCellItem"/> 을 알고 있어야 한다.
    /// </summary>
    public override string TooltipExtra()
    {
        if (IsEmpty) return "비어 있음";
        return string.Format("종류 {0} · {1:N0}개", names.Count, Total);
    }
}
