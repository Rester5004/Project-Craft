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

    /// <summary>플레이어가 항상 두르고 있는 빛. 어디서도 완전히 실명하지 않게 하는 최소한이다.</summary>
    public static readonly Color PlayerLight = new Color(1f, 0.949f, 0.816f);          // #FFF2D0
    // ⚠ 너무 넓거나 밝으면 횃불이 할 일이 없어진다. 카메라가 세로 8유닛을 보여 주므로
    //    반경 3.5 면 화면의 한가운데만 밝고 가장자리는 어둠으로 남는다.
    public const float PlayerInnerRadius = 0.6f;
    public const float PlayerOuterRadius = 3.5f;
    public const float PlayerIntensity = 0.55f;

    /// <summary>지금 월드의 환경광 색.</summary>
    public static Color AmbientColor => UndergroundSession.IsActive ? UndergroundAmbient : SurfaceAmbient;

    /// <summary>지금 월드의 환경광 세기.</summary>
    public static float AmbientIntensity => UndergroundSession.IsActive ? UndergroundIntensity : SurfaceIntensity;
}
