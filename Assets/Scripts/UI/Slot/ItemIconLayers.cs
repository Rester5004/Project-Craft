using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "이 아이템을 무슨 그림 몇 장으로 그리는가" 를 정하는 <b>유일한 곳</b>.
///
/// 예전에는 <see cref="ItemIconView.Apply"/>(UI 슬롯)와 <see cref="DroppedItem"/>(필드 드랍)이
/// 똑같은 폴백 네 줄을 <b>각자 복사해</b> 갖고 있었다. 규칙이 하나 늘 때마다 두 곳을 함께 고쳐야 하고,
/// 한쪽만 고치면 "슬롯에서는 유체 색이 보이는데 바닥에 떨어뜨리면 회색" 같은 어긋남이 생긴다.
///
/// 우선순위는 <b>구체적인 것부터</b>다:
///   ① 개체 데이터(커스텀 도구의 자루+머리) → ② 유체가 담긴 그릇 → ③ 평범한 아이콘 한 장.
/// </summary>
public static class ItemIconLayers
{
    /// <summary>
    /// <paramref name="results"/> 를 비우고 그릴 레이어를 채운다(앞이 아래, 뒤가 위).
    /// 아이템이 없으면 비운 채로 돌아온다 — 부르는 쪽이 "아이콘 감추기" 로 처리한다.
    /// </summary>
    public static void Collect(Items item, ItemInstance instance, List<IconLayer> results)
    {
        if (results == null) return;

        results.Clear();
        if (item == null) return;

        // ① 개체 데이터가 스스로 레이어를 낼 수 있으면 그것이 가장 구체적이다.
        if (instance != null && instance.CollectIconLayers(item, results) && results.Count > 0) return;

        // 실패한 CollectIconLayers 가 절반만 채워 두었을 수 있다.
        results.Clear();

        // ② 유체가 담긴 그릇.
        if (CollectFluidContainer(item, results)) return;

        // ③ 평범한 아이템.
        results.Add(new IconLayer(item.Icon, Color.white));
    }

    /// <summary>
    /// 채워진 그릇이면 [빈 그릇 그림, 유체 색으로 물들인 오버레이] 두 장을 채우고 true.
    ///
    /// 그림의 정본이 <b>빈 그릇 아이템</b>(<see cref="Items.fluidOverlay"/>)에 있는 이유:
    /// 오버레이 모양은 유체가 아니라 <b>그릇</b>의 성질이다. 같은 물이라도 양동이와 유리 용기는
    /// 다르게 그려져야 하는데, <see cref="FluidDefine"/> 에 두면 그릇이 하나 늘 때마다 유체 8개를
    /// 전부 고쳐야 한다. 색은 여기서도 정하지 않고 <see cref="FluidColors"/> 한 곳에 묻는다.
    ///
    /// <b>오버레이 그림이 없는 그릇은 조용히 false</b> 다 — 유리 용기처럼 아직 아트가 없는 그릇이
    /// 있어서, 여기서 경고를 내면 그 아이템을 볼 때마다 로그가 쏟아진다.
    /// </summary>
    private static bool CollectFluidContainer(Items item, List<IconLayer> results)
    {
        ItemDictionary dictionary = ItemDictionary.Instance;
        if (dictionary == null) return false;

        FluidDefine fluid = dictionary.GetFluidForItem(item);
        if (fluid == null) return false;

        Items container = fluid.emptyItem;
        if (container == null || container.Icon == null || container.fluidOverlay == null) return false;

        results.Add(new IconLayer(container.Icon, Color.white));
        results.Add(new IconLayer(container.fluidOverlay, FluidColors.Of(fluid.fluidId)));
        return true;
    }
}
