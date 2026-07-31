using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 가스/에너지/진행도 바에 호버 툴팁을 붙인다.
/// 바 자체(<see cref="FillingSlot"/>)는 자기가 무슨 역할인지 모르므로,
/// 같은 오브젝트의 <see cref="MachineUIElement"/> 에서 역할·순번을 읽고
/// 실제 문구는 기계 인스턴스를 아는 <see cref="DefaultMachineUI"/> 에게 맡긴다.
///
/// 이 컴포넌트는 DefaultMachineUI 가 초기화 때 AddComponent 로 붙이므로
/// 기존 기계 UI 프리팹을 수정할 필요가 없다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MachineUIElement))]
public class BarTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private DefaultMachineUI owner;
    private MachineUIElement element;

    // 호버할 때마다 델리게이트를 새로 만들지 않도록 한 번만 만들어 둔다.
    private System.Func<string> textProvider;

    /// <summary>이 바가 속한 패널을 지정한다.</summary>
    public void Bind(DefaultMachineUI panel)
    {
        owner = panel;
        if (element == null) element = GetComponent<MachineUIElement>();
        if (textProvider == null) textProvider = GetText;
    }

    /// <summary>바가 가리키는 값은 계속 변하므로 호버 시점에 한 번 읽지 않고 매 프레임 다시 만든다.</summary>
    private string GetText()
        => owner != null && element != null ? owner.GetBarTooltip(element.role, element.index) : "";

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipUI.Instance == null) return;
        if (owner == null || element == null) return;

        TooltipUI.Instance.Show(textProvider);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipUI.Instance != null) TooltipUI.Instance.Hide();
    }
}
