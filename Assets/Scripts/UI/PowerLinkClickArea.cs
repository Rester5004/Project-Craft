using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 전력 전송 모드의 전체화면 클릭 영역. 좌/우클릭을 그대로 <see cref="PowerLinkMode"/> 에 넘긴다.
///
/// 마우스를 직접 폴링하지 않고 EventSystem 을 거치는 이유:
/// 이 경로라야 "돌아가기 버튼 위 클릭"과 "월드 셀 클릭"을 uGUI 가 알아서 갈라 준다.
/// (버튼이 이 영역보다 위에 있고 스스로 클릭을 소비하므로 여기까지 내려오지 않는다.)
/// </summary>
[DisallowMultipleComponent]
public class PowerLinkClickArea : MonoBehaviour, IPointerClickHandler
{
    /// <summary>클릭이 들어왔을 때 호출된다. 어느 버튼인지는 PointerEventData.button 으로 판별한다.</summary>
    public System.Action<PointerEventData> OnClicked;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (OnClicked != null) OnClicked(eventData);
    }
}
