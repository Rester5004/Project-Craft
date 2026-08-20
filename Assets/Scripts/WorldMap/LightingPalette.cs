using UnityEngine;

/// <summary>
/// 어느 월드가 얼마나 어두운지를 정하는 <b>정본 표</b>.
/// <see cref="ExtractionTable"/> · <see cref="FluidColors"/> 와 같은 꼴로 static 이다.
///
/// 값을 코드 여기저기 박지 않으려고 한 곳에 모았다 — 밝기는 반드시 여러 번 바뀐다.
///
/// <b>메인 맵도 설정상 지하다.</b> 그래서 낮/밤 주기가 없고, 지상·지하를 밝기로 가르지 않는다.
/// 다만 탐험용 인스턴스 방(<see cref="UndergroundSession"/>)은 조금 더 깊은 곳이라 한 단계 더 어둡다.
/// </summary>
public static class LightingPalette
{
    /// <summary>메인 맵(지상 씬)의 환경광. 차가운 푸른빛이라 횃불의 주황과 대비가 선다.</summary>
    public static readonly Color SurfaceAmbient = new Color(0.227f, 0.290f, 0.420f);   // #3A4A6B
    public const float SurfaceIntensity = 0.18f;

    /// <summary>탐험용 지하 방. 등급이 올라가도 지금은 같은 값을 쓴다(등급별로 나눌 자리만 열어 둔다).</summary>
    public static readonly Color UndergroundAmbient = new Color(0.180f, 0.200f, 0.290f);
    public const float UndergroundIntensity = 0.12f;

    /// <summary>플레이어를 따라다니는 두 빛의 색. 빔·오라가 같은 색이라야 경계가 안 보인다.</summary>
    public static readonly Color PlayerLight = new Color(1f, 0.949f, 0.816f);          // #FFF2D0

    // ── 빔: 보는 쪽으로 나가는 원뿔 ─────────────────────────────
    // ⚠ 콘의 중심은 광원의 <b>로컬 +Y</b> 다(URP Light2D 규약). 어느 쪽을 보는가는 이 표가 아니라
    //    <see cref="PlayerLightAim"/> 이 정하고, 여기 있는 것은 "얼마나 넓고 밝은가" 뿐이다.
    // ⚠ 너무 넓거나 밝으면 횃불이 할 일이 없어진다. 카메라가 세로 8유닛을 보여 주므로
    //    반경 5 면 앞쪽 한 화면 남짓만 닿고 옆·뒤는 어둠으로 남는다.
    public const float BeamInnerRadius = 0.3f;
    public const float BeamOuterRadius = 5f;
    public const float BeamIntensity = 1f;
    public const float BeamInnerAngle = 0f;      // 0 = 중심부터 바로 감쇠가 시작돼 가장자리가 부드럽다
    public const float BeamOuterAngle = 128f;

    // ── 오라: 발밑이 통째로 검어지지 않게 하는 최소한 ───────────
    // 빔만 두면 옆·뒤가 환경광(0.18)만 남아 방향 감각을 잃는다. 반경을 한 칸 남짓으로 좁게 잡아
    // 밝히는 것이 아니라 "자기 발밑은 보인다" 만 보장한다.
    public const float AuraInnerRadius = 0.2f;
    public const float AuraOuterRadius = 1.6f;
    public const float AuraIntensity = 0.35f;

    /// <summary>지금 월드의 환경광 색.</summary>
    public static Color AmbientColor => UndergroundSession.IsActive ? UndergroundAmbient : SurfaceAmbient;

    /// <summary>지금 월드의 환경광 세기.</summary>
    public static float AmbientIntensity => UndergroundSession.IsActive ? UndergroundIntensity : SurfaceIntensity;
}
