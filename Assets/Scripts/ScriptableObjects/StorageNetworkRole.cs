/// <summary>
/// 저장 네트워크 장치가 무엇인가. <b>문자열(blockId)로 판정하지 않으려고</b> 만든 값이다
/// (<see cref="WrenchItem"/> 을 타입으로 판정하는 것과 같은 규약).
///
/// 값은 직렬화 호환을 위해 고정한다.
/// </summary>
public enum StorageNetworkRole
{
    /// <summary>네트워크의 심장. <b>한 네트워크에 하나뿐</b>이고 둘 이상이면 그 네트워크가 통째로 죽는다.</summary>
    Controller = 0,

    /// <summary>저장 셀을 꽂는 곳. 칸 수만큼 셀을 받는다.</summary>
    Drive = 1,

    /// <summary>네트워크 전체를 한 화면으로 보여 주고 넣고 빼는 창구.</summary>
    Terminal = 2,
}
