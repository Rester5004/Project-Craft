using UnityEngine;

/// <summary>
/// 저장 네트워크의 장치 한 종류(컨트롤러 · 드라이브 · 터미널).
///
/// <b><see cref="MachineBlock"/> 을 물려받는 것은 순전히 공짜로 얻는 것 때문이다</b> —
/// 프리팹 스폰 · 발자국 · 콜라이더 · 채굴 · 세이브 · 청크 수명주기 · 전력 링크가 전부 따라온다
/// (<see cref="LightBlock"/> 이 같은 이유로 그렇게 돼 있다). 새 배치물 *종류*를 만들면
/// 분기를 여섯 곳에 더해야 한다.
///
/// ⚠ 종류를 가르는 것은 <see cref="role"/> 이다. <b>blockId 문자열로 판정하지 말 것</b> —
/// 이름을 바꾸는 순간 조용히 네트워크에서 빠진다.
/// </summary>
[CreateAssetMenu(fileName = "StorageNetworkBlock", menuName = "Blocks/StorageNetworkBlock")]
public class StorageNetworkBlock : MachineBlock
{
    [Header("저장 네트워크")]
    [Tooltip("이 장치가 네트워크에서 맡는 역할.")]
    public StorageNetworkRole role = StorageNetworkRole.Controller;

    /// <summary>
    /// 슬롯이 0개여도 "미설정" 으로 보지 않는다 — 컨트롤러·터미널은 자기 칸이 없다.
    /// 빼면 <see cref="MachineInstance.ApplyConfig"/> 의 폴백이 3/6 칸을 붙인다.
    /// </summary>
    public override bool AllowsZeroSlots => true;

    /// <summary>
    /// 레시피를 돌리지 않는다. 드라이브의 칸은 <b>셀을 꽂는 자리</b>지 재료 자리가 아니다 —
    /// 켜 두면 <c>Tick</c> 이 그 칸을 재료로 보려 든다.
    /// </summary>
    public override bool AutoProcess => false;

    /// <summary>
    /// <b>상시 전력 소비.</b> 조명과 같은 경로를 탄다(<c>MachineInstance.Update</c> 의
    /// AutoProcess 조기 return 앞 분기). 이것이 노션 설계의 <b>"채널 제한 대신 전력이 곧 한계"</b> 다 —
    /// 새 전력 개념을 만들지 않고 <see cref="MachineBlock.energyUseRate"/> 하나로 표현한다.
    ///
    /// ⚠ 그래서 <c>IsRunning</c> 이 곧 <b>"이 장치에 전기가 들어와 있는가"</b> 이고,
    /// <see cref="StorageNetwork"/> 가 그 값 하나로 네트워크 생사를 판단한다.
    /// </summary>
    public override bool IsAlwaysOn => true;

    /// <summary>컨트롤러는 칸이 하나도 없어 <b>빈 패널이 뜨면 안 된다</b>.</summary>
    public override bool OpensUI => role != StorageNetworkRole.Controller;
}
