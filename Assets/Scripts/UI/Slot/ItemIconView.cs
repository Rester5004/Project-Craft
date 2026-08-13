using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 슬롯 아이콘 하나를 그리는 공용 루틴. 평범한 아이템은 스프라이트 한 장이지만
/// 커스텀 도구·채워진 양동이처럼 여러 장을 겹쳐야 하는 아이템도 있어서, 부족한 만큼 겹침 Image 를
/// <b>런타임에 만들어</b> 쓴다(프리팹을 고치지 않기 위해 — BarTooltip 과 같은 규약).
///
/// <b>여기는 "어떻게 그리는가" 만 안다.</b> "무엇을 몇 장으로 그리는가" 는
/// <see cref="ItemIconLayers"/> 가 정한다 — 필드 드랍(<see cref="DroppedItem"/>)과 규칙을 공유하려면
/// 그 판단이 한 곳에 있어야 한다.
///
/// 겹침 Image 는 반드시 기준 Image 의 <b>자식</b>이어야 한다.
/// <see cref="ItemSlot.OnBeginDrag"/> 가 기준 Image 의 transform 째로 캔버스에 옮기기 때문이다.
/// </summary>
public static class ItemIconView
{
    private const string LayerPrefix = "IconLayer";
    private static readonly List<IconLayer> buffer = new();

    /// <summary>스택 하나를 그린다. 비어 있으면 아이콘을 감춘다.</summary>
    public static void Apply(Image baseImage, ItemStack stack, float alpha = 1f)
    {
        bool has = stack != null && stack.item != null && stack.count > 0;
        Apply(baseImage, has ? stack.item : null, has ? stack.instance : null, alpha);
    }

    /// <summary>아이템 + 개체 데이터를 그린다(레시피 슬롯처럼 스택이 없는 곳에서 쓴다).</summary>
    public static void Apply(Image baseImage, Items item, ItemInstance instance, float alpha = 1f)
    {
        if (baseImage == null) return;

        if (item == null)
        {
            baseImage.enabled = false;
            HideFrom(baseImage, 1);
            return;
        }

        // 무엇을 몇 장으로 그릴지는 ItemIconLayers 한 곳이 정한다(필드 드랍도 같은 것을 본다).
        ItemIconLayers.Collect(item, instance, buffer);
        if (buffer.Count == 0)
        {
            baseImage.enabled = false;
            HideFrom(baseImage, 1);
            return;
        }

        baseImage.enabled = true;
        baseImage.sprite = buffer[0].sprite;
        baseImage.color = WithAlpha(buffer[0].color, alpha);

        for (int i = 1; i < buffer.Count; i++)
        {
            Image overlay = GetOverlay(baseImage, i);
            overlay.enabled = true;
            overlay.sprite = buffer[i].sprite;
            overlay.color = WithAlpha(buffer[i].color, alpha);
        }

        HideFrom(baseImage, buffer.Count);
    }

    private static Color WithAlpha(Color color, float alpha)
        => new Color(color.r, color.g, color.b, color.a * alpha);

    /// <summary><paramref name="index"/> 번째 겹침 Image 를 찾거나 만든다(기준 Image 의 자식).</summary>
    private static Image GetOverlay(Image baseImage, int index)
    {
        string name = LayerPrefix + index;
        Transform existing = baseImage.transform.Find(name);
        if (existing != null) return existing.GetComponent<Image>();

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = baseImage.gameObject.layer;

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(baseImage.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        Image overlay = go.GetComponent<Image>();
        overlay.raycastTarget = false;             // 아래 슬롯의 호버·드래그를 가리지 않게
        overlay.preserveAspect = baseImage.preserveAspect;
        return overlay;
    }

    /// <summary><paramref name="from"/> 번째부터의 겹침 Image 를 끈다(파괴하지 않고 재사용).</summary>
    private static void HideFrom(Image baseImage, int from)
    {
        for (int i = Mathf.Max(1, from); ; i++)
        {
            Transform layer = baseImage.transform.Find(LayerPrefix + i);
            if (layer == null) break;

            Image overlay = layer.GetComponent<Image>();
            if (overlay != null) overlay.enabled = false;
        }
    }
}
