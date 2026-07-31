/// <summary>
/// 파이프가 무엇을 나르는가. <b>종류가 같아야 서로 이어진다</b> —
/// 아이템 파이프와 액체 파이프는 맞붙어 있어도 남남이다.
/// 값은 직렬화 호환을 위해 고정한다.
/// </summary>
public enum PipeKind
{
    Item = 0,
    Liquid = 1,
    Gas = 2,
}
