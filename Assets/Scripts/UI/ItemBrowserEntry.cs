using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 아이템 목록의 줄 하나. 좌/우 클릭을 구분해야 해서 <see cref="Button"/> 대신 직접 받는다
/// (Button 의 onClick 은 어느 버튼으로 눌렀는지 알려 주지 않는다).
///
/// 자기 아이템이 무엇인지만 알고, 실제 지급은 <see cref="ItemBrowser"/> 에 맡긴다.
/// </summary>
public class ItemBrowserEntry : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private static readonly Color IdleColor = new Color(1f, 1f, 1f, 0.05f);
    private static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.18f);

    private ItemBrowser owner;
    private Items item;
    private Image background;

    public void Bind(ItemBrowser browser, Items boundItem, Image rowBackground)
    {
        owner = browser;
        item = boundItem;
        background = rowBackground;
        if (background != null) background.color = IdleColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null || item == null) return;
        owner.Give(item, eventData.button == PointerEventData.InputButton.Right);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (background != null) background.color = HoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (background != null) background.color = IdleColor;
    }
}
