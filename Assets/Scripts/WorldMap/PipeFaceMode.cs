/// <summary>
/// 파이프 한 면(N/E/S/W 중 하나)에 플레이어가 렌치로 지정해 둔 상태.
///
/// 값이 <b>2비트에 들어가야</b> 한다 — 네 면을 <see cref="PlaceableRecord.faceModes"/> 1바이트에 담기 때문.
/// 그래서 항목을 늘리려면 저장 포맷을 함께 바꿔야 한다.
///
/// <see cref="Cut"/> 은 파이프-파이프 면에만, <see cref="Insert"/>/<see cref="Extract"/> 는
/// 기계 면에만 의미가 있다. 어긋난 조합이 남아 있어도(기계를 캐서 옆이 빈 칸이 된 경우 등)
/// 판정하는 쪽이 이웃을 먼저 보므로 그냥 무시된다.
/// </summary>
public enum PipeFaceMode : byte
{
    /// <summary>손대지 않은 상태. 지금까지와 같이 양방향으로 동작한다.</summary>
    Default = 0,

    /// <summary>기계에 <b>넣기만</b> 한다. 이 면으로는 기계에서 꺼내지 않는다.</summary>
    Insert = 1,

    /// <summary>기계에서 <b>꺼내기만</b> 한다. 이 면으로는 기계에 넣지 않는다.</summary>
    Extract = 2,

    /// <summary>두 파이프 사이를 끊는다. 모양도 막힌 끝으로 바뀐다.</summary>
    Cut = 3,
}
