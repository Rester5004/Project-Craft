using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지하맵으로 내려가거나 지상으로 올라오는 포탈 하나. 씬 파일을 건드리지 않게 <b>런타임에 만든다</b>
/// (<c>PipeNetworkManager</c> · <c>CommandConsole</c> 과 같은 규약).
///
/// <b>어느 포탈을 쓸지 고르는 것만</b> 여기 몫이고, 실제로 무슨 일이 일어나는지는
/// <see cref="UndergroundSession"/> 이 정한다. 상호작용 판정 자체는 <c>PlayerInteraction</c> 한 곳에서
/// <see cref="TryUseNearest"/> 를 부르는 것으로 유지된다.
/// </summary>
public class UndergroundPortal : MonoBehaviour
{
    public enum Kind { ToUnderground, ToSurface }

    /// <summary>포탈 중심에서 이 거리 안이면 쓸 수 있다.</summary>
    private const float UseDistance = 1.5f;

    /// <summary>
    /// 살아 있는 포탈들. <c>FindObjectsByType</c> 로 매번 훑지 않으려는 것도 있지만,
    /// <b>씬이 바뀌면 저절로 비어야</b> 하기 때문이다 — 오브젝트가 파괴될 때 스스로 빠진다.
    /// </summary>
    private static readonly List<UndergroundPortal> active = new();

    private Kind kind;
    private int tier;

    /// <summary>포탈을 세운다. 좌표는 셀 중앙을 넘길 것(예: 셀 (0,0) 이면 (0.5, 0.5)).</summary>
    public static UndergroundPortal Create(Vector2 worldPosition, Kind kind, int tier)
    {
        GameObject go = new(kind == Kind.ToUnderground ? "Underground Portal" : "Return Portal");
        go.transform.position = worldPosition;
        go.transform.localScale = Vector3.one * 0.8f;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        renderer.color = kind == Kind.ToUnderground ? new Color(0.2f, 0.85f, 1f) : new Color(1f, 0.75f, 0.2f);
        // 기계와 같은 층(2). 플레이어(3)보다 아래여야 발밑에 깔린 것으로 보인다.
        renderer.sortingOrder = 120;

        UndergroundPortal portal = go.AddComponent<UndergroundPortal>();
        portal.kind = kind;
        portal.tier = tier;
        return portal;
    }

    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);

    /// <summary>
    /// 플레이어에게서 가장 가까운 포탈을 쓴다. 쓸 포탈이 없으면 false 라 호출자가 평소 동작으로 흘려보낸다.
    /// </summary>
    public static bool TryUseNearest(Transform player)
    {
        if (player == null) return false;

        UndergroundPortal best = null;
        float bestSqr = UseDistance * UseDistance;
        foreach (UndergroundPortal portal in active)
        {
            if (portal == null) continue;
            float sqr = ((Vector2)portal.transform.position - (Vector2)player.position).sqrMagnitude;
            if (sqr > bestSqr) continue;
            bestSqr = sqr;
            best = portal;
        }
        if (best == null) return false;

        best.Use(player);
        return true;
    }

    private void Use(Transform player)
    {
        if (kind == Kind.ToSurface) { UndergroundSession.Exit(); return; }

        // 지상 포탈은 한 번 쓰면 사라진다 — 남겨 두면 탐지기 하나로 무한히 드나들 수 있다.
        UndergroundSession.Enter(tier, player.position);
        Destroy(gameObject);
    }
}
