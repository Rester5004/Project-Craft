using UnityEngine;

/// <summary>
/// 기계 UI 제작 전용 씬(MachineUIFactory)의 작업 루트 마커.
/// 제작 창이 이 컴포넌트를 찾아 레이아웃을 생성/편집한다. 런타임 로직은 없다.
/// </summary>
public class MachineUIFactoryStage : MonoBehaviour
{
    [Tooltip("제작 중인 레이아웃이 배치되는 부모(보통 Canvas 아래의 AuthoringRoot)")]
    public RectTransform authoringRoot;

    public RectTransform Root => authoringRoot != null ? authoringRoot : transform as RectTransform;
}
