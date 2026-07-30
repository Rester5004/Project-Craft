using UnityEngine;

/// <summary>
/// 아이템 아이콘을 이룰 겹침 한 장. 커스텀 도구처럼 여러 스프라이트를 포개 그려야 하는
/// 아이템이 <see cref="ItemInstance.CollectIconLayers"/> 로 돌려준다.
/// </summary>
public struct IconLayer
{
    public Sprite sprite;
    public Color color;

    public IconLayer(Sprite sprite, Color color)
    {
        this.sprite = sprite;
        this.color = color;
    }
}
