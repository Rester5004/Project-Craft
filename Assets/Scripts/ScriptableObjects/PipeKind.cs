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

    /// <summary>
    /// 저장 네트워크의 <b>데이터 케이블</b>. 짐을 싣지 않고 <b>연결만</b> 한다.
    ///
    /// 운반 경로가 저절로 비켜 간다 — <see cref="PipeBlock.CarriesItems"/> 는 <c>Item</c> 만 참이고
    /// <see cref="FluidDefine.CarriedBy"/> 는 Liquid/Gas 만 돌려주므로, <b>새 분기를 하나도 안 넣어도</b>
    /// 아이템·유체가 케이블에 실리지 않는다. 반대로 연결 마스크·오토타일·렌치 면은 <c>kind</c> 로만
    /// 갈리므로 전부 공짜로 따라온다.
    /// </summary>
    Data = 3,
}
