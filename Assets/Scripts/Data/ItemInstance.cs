using System.Collections.Generic;
using System.IO;

/// <summary>
/// 스택 하나에만 붙는 개체별 데이터의 베이스(커스텀 도구의 재질·내구도 등).
/// <see cref="ItemStack.instance"/> 가 null 이면 "평범한 아이템"이라 기존 동작과 완전히 같다.
///
/// 새 종류를 만들려면 이 클래스를 상속하고 <see cref="TypeId"/> 만 고유하게 주면 된다.
/// <see cref="ItemInstanceSerializer"/> 가 어셈블리를 훑어 자동으로 등록한다.
/// </summary>
public abstract class ItemInstance
{
    /// <summary>세이브 파일에 기록되는 종류 태그. 바꾸면 기존 세이브의 해당 아이템이 사라진다.</summary>
    public abstract string TypeId { get; }

    /// <summary>스택을 나눌 때 쓰는 깊은 복사.</summary>
    public abstract ItemInstance Clone();

    /// <summary>같은 타입끼리 병합해도 되는가(내용이 완전히 같은가).</summary>
    public abstract bool Matches(ItemInstance other);

    public abstract void Write(BinaryWriter writer);
    public abstract void Read(BinaryReader reader);

    /// <summary>화면에 보일 이름. 기본은 아이템 이름 그대로.</summary>
    public virtual string DecorateName(Items item) => item != null ? item.DisplayName : "";

    /// <summary>툴팁에 덧붙일 줄(없으면 빈 문자열).</summary>
    public virtual string TooltipExtra() => "";

    /// <summary>
    /// 아이콘을 여러 장 겹쳐 그려야 하면 <paramref name="results"/> 를 채우고 true 를 돌려준다.
    /// false 면 슬롯이 평소대로 <see cref="Items.Icon"/> 한 장만 그린다.
    /// </summary>
    public virtual bool CollectIconLayers(Items item, List<IconLayer> results) => false;

    /// <summary>둘 다 null 이면 같다고 본다(평범한 아이템끼리는 항상 병합 가능).</summary>
    public static bool Same(ItemInstance a, ItemInstance b)
    {
        if (a == null) return b == null;
        return b != null && a.GetType() == b.GetType() && a.Matches(b);
    }
}
