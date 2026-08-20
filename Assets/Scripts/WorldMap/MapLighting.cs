using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 맵 전체의 밝기를 맡는다. <see cref="LightingPalette"/> 의 값을 씬의 Global Light 2D 와
/// 플레이어 빛에 밀어 넣는다.
///
/// <b>오브젝트는 GameRig 프리팹에 저작돼 있다</b>(이 컴포넌트도, 플레이어 빛도). 코드로 만들면
/// 씬에서 위치·세기를 못 만지고 인스펙터에도 안 보인다. 두 씬이 같은 GameRig 를 쓰고
/// PrefabInstance override 가 transform·이름뿐이라 <b>프리팹 한 장이 두 씬에 그대로 전파된다.</b>
///
/// ⚠ <b>빛 <i>값</i>의 정본은 여전히 <see cref="LightingPalette"/> 다.</b> 프리팹은 오브젝트만 갖고
/// 값은 여기서 채운다 — <see cref="LightBlock"/>(SO 가 값 / 프리팹은 빈 Light2D)과 같은 규약이라
/// 밝기가 두 곳으로 갈라지지 않는다.
/// </summary>
public class MapLighting : MonoBehaviour
{
    public static MapLighting Active { get; private set; }

    [Tooltip("씬의 Global Light 2D. 환경광(어둠의 바닥)을 여기에 넣는다.")]
    [SerializeField] private Light2D globalLight;

    [Tooltip("플레이어가 보는 쪽으로 나가는 원뿔 빔(TestPlayer 자식). 방향은 PlayerLightAim 이 돌린다.")]
    [SerializeField] private Light2D playerLight;

    [Tooltip("발밑을 덮는 작은 원형. 빔 하나만 두면 옆·뒤가 환경광만 남아 방향 감각을 잃는다.")]
    [SerializeField] private Light2D playerAura;

    private void Awake()
    {
        if (Active == null) Active = this;
        if (globalLight == null)
            Debug.LogError("[MapLighting] globalLight 이 비어 있습니다. GameRig 의 Global Light 2D 를 꽂아야 합니다.", this);
    }

    private void Start() => Apply();

    private void OnDestroy()
    {
        if (Active == this) Active = null;
    }

    /// <summary>
    /// 지금 월드(<see cref="UndergroundSession"/>)에 맞는 밝기를 적용한다.
    /// 지하로 내려가거나 올라온 뒤에 다시 부른다.
    /// </summary>
    public void Apply()
    {
        if (globalLight != null)
        {
            globalLight.color = LightingPalette.AmbientColor;
            globalLight.intensity = LightingPalette.AmbientIntensity;
        }

        // 벽이 빛을 막는 것이 이 프로젝트의 규칙이라 플레이어 빛도 예외를 두지 않는다.
        // 다만 완전한 검정은 방향 감각을 잃게 하므로 그림자를 조금 남긴다.
        if (playerLight != null)
        {
            playerLight.color = LightingPalette.PlayerLight;
            playerLight.intensity = LightingPalette.BeamIntensity;
            playerLight.pointLightInnerRadius = LightingPalette.BeamInnerRadius;
            playerLight.pointLightOuterRadius = LightingPalette.BeamOuterRadius;
            // ⚠ 각도까지 여기서 넣는다. 씬·프리팹에 두면 두 씬이 갈라진다 —
            //    실제로 MapTest 에만 콘 설정이 있어 지하에서는 원형이던 적이 있다.
            playerLight.pointLightInnerAngle = LightingPalette.BeamInnerAngle;
            playerLight.pointLightOuterAngle = LightingPalette.BeamOuterAngle;
            playerLight.shadowsEnabled = true;
            playerLight.shadowIntensity = 0.75f;
        }

        if (playerAura == null) return;

        playerAura.color = LightingPalette.PlayerLight;
        playerAura.intensity = LightingPalette.AuraIntensity;
        playerAura.pointLightInnerRadius = LightingPalette.AuraInnerRadius;
        playerAura.pointLightOuterRadius = LightingPalette.AuraOuterRadius;
        playerAura.shadowsEnabled = true;
        playerAura.shadowIntensity = 0.75f;
    }
}
