using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬을 추가하지 않고 지상과 지하 프로토타입 공간을 연결하는 임시 게이트다.
/// E 키를 누르면 가까운 게이트를 사용한다.
/// </summary>
public class PrototypeMapTransitions : MonoBehaviour
{
    private const float UseDistance = 1.5f; //근처 게이트 사용가능 거리
    private static readonly List<PrototypeMapTransitions> Gates = new();
    private static bool initialized; //게이트 생성됐는지 여부

    [SerializeField] private Vector2 destination; //게이트 쓰면 갈 목적지

    private void Awake()
    {
        Gates.Add(this);
    }

    private void OnDestroy()
    {
        Gates.Remove(this);
    }

    public static bool TryUseNearest(Transform player)
    {
        EnsurePrototypeGates();
        PrototypeMapTransitions nearest = null;
        float nearestDistance = UseDistance * UseDistance; 
        foreach (PrototypeMapTransitions gate in Gates) //가장 가까운 게이트 탐색
        {
            if (gate == null) continue;
            float distance = ((Vector2)gate.transform.position - (Vector2)player.position).sqrMagnitude;
            if (distance <= nearestDistance)
            {
                nearest = gate; //가장 가까우면 갱신
                nearestDistance = distance;
            }
        }

        if (nearest == null)
            return false;

        player.position = nearest.destination;
        return true;
    }

    public static void Initialize() => EnsurePrototypeGates();

    private static void EnsurePrototypeGates()
    {
        if (initialized) return;
        initialized = true;
        if (WorldMap.Instance != null)
            WorldMap.Instance.EnsurePrototypeUndergroundRoom();
        CreateGate("Surface To Underground Gate", new Vector2(2f, 0f), new Vector2(0f, -64f), new Color(0.2f, 0.85f, 1f)); //지상
        CreateGate("Underground Return Gate", new Vector2(0f, -64f), new Vector2(1f, 0f), new Color(1f, 0.75f, 0.2f)); //지하
    }

    private static void CreateGate(string gateName, Vector2 position, Vector2 destination, Color color)
    {
        GameObject gateObject = new(gateName);
        gateObject.transform.position = position;
        PrototypeMapTransitions gate = gateObject.AddComponent<PrototypeMapTransitions>();
        gate.destination = destination; //목적지 설정용 

        SpriteRenderer renderer = gateObject.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        renderer.color = color;
        renderer.sortingOrder = 10;
        gateObject.transform.localScale = Vector3.one * 0.65f;
    }
}
