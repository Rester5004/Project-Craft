using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 필드에 떨어져 있는 아이템 하나의 월드 표현. <see cref="DropRecord"/> 를 화면에 보여 주고
/// 플레이어가 닿으면 인벤토리로 옮긴다.
///
/// 프리팹 없이 <see cref="Create"/> 로 코드에서 만든다(<see cref="TooltipUI"/> 와 같은 규약).
/// 씬에 배선할 것이 없어 프리팹 참조가 끊길 일이 없다.
///
/// 플레이어가 Dynamic Rigidbody2D 라 <b>이쪽에는 리지드바디가 필요 없다</b> — 트리거 콜라이더만 있으면
/// 플레이어의 리지드바디가 트리거 이벤트를 일으킨다.
/// </summary>
[DisallowMultipleComponent]
public class DroppedItem : MonoBehaviour
{
    private const float PickupRadius = 0.28f;
    private const int SortingOrder = 3;

    /// <summary>바닥에 떨어진 아이템은 인벤토리 아이콘보다 작게 보인다.</summary>
    private const float IconScale = 0.5f;

    /// <summary>제자리 회전 속도(초당 각도). 눈에 띄되 어지럽지 않은 정도.</summary>
    private const float SpinSpeed = 90f;

    /// <summary>인벤토리가 가득 차 못 주웠을 때 매 프레임 재시도하지 않도록 두는 간격(초).</summary>
    private const float RetryInterval = 0.25f;

    private static readonly List<IconLayer> LayerBuffer = new();

    private DropRecord record;
    private Chunk owner;
    private Items item;
    private float nextTryTime;

    // 스프라이트만 담는 자식. 크기·회전을 여기에만 걸어야 줍기 콜라이더가 영향을 받지 않는다.
    private Transform iconRoot;

    /// <summary>이 오브젝트가 표시 중인 레코드.</summary>
    public DropRecord Record => record;

    /// <summary>드랍을 사용할 수 없게 되면(아이템 미등록 등) null 을 돌려준다.</summary>
    public static DroppedItem Create(Transform parent, DropRecord record, Chunk owner)
    {
        if (record == null) return null;

        Items item = ItemDictionary.Instance != null ? ItemDictionary.Instance.GetItem(record.itemName) : null;
        if (item == null)
        {
            Debug.LogWarning($"[DroppedItem] 아이템 '{record.itemName}' 을 찾을 수 없어 드랍을 표시하지 못했습니다(딕셔너리 미등록).");
            return null;
        }

        GameObject go = new GameObject("Drop_" + item.itemName);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(record.x, record.y, 0f);

        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;          // 물리적으로 막지 않고 접촉만 감지한다
        collider.radius = PickupRadius;

        DroppedItem drop = go.AddComponent<DroppedItem>();
        drop.record = record;
        drop.owner = owner;
        drop.item = item;
        drop.DrawIcon();
        return drop;
    }

    /// <summary>
    /// 아이콘을 그린다. 커스텀 도구처럼 여러 장을 겹쳐야 하는 아이템은
    /// UI 와 같은 <see cref="ItemInstance.CollectIconLayers"/> 를 써서 자식 SpriteRenderer 로 포갠다.
    /// </summary>
    private void DrawIcon()
    {
        LayerBuffer.Clear();
        if (record.instance == null || !record.instance.CollectIconLayers(item, LayerBuffer))
        {
            LayerBuffer.Clear();
            LayerBuffer.Add(new IconLayer(item.Icon, Color.white));
        }

        GameObject iconGO = new GameObject("Icon");
        iconRoot = iconGO.transform;
        iconRoot.SetParent(transform, false);
        iconRoot.localScale = Vector3.one * IconScale;

        for (int i = 0; i < LayerBuffer.Count; i++)
        {
            GameObject host = new GameObject("Layer" + i);
            host.transform.SetParent(iconRoot, false);

            SpriteRenderer renderer = host.AddComponent<SpriteRenderer>();
            renderer.sprite = LayerBuffer[i].sprite;
            renderer.color = LayerBuffer[i].color;
            renderer.sortingOrder = SortingOrder + i;   // 뒤 레이어가 위로 올라온다
        }
    }

    private void Update()
    {
        if (iconRoot == null) return;
        iconRoot.Rotate(0f, 0f, SpinSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other) => TryPickup(other);

    // Enter 는 한 번만 온다. 인벤토리가 가득 차 못 주웠다면 Stay 로 다시 시도해야
    // 자리를 비운 순간 주울 수 있다.
    private void OnTriggerStay2D(Collider2D other) => TryPickup(other);

    private void TryPickup(Collider2D other)
    {
        if (record == null || Time.time < nextTryTime) return;
        if (other.GetComponentInParent<PlayerInteraction>() == null) return;   // 플레이어만 줍는다

        nextTryTime = Time.time + RetryInterval;

        Inventory inventory = Inventory.Instance;
        if (inventory == null) return;

        int added = inventory.AddPartial(item, record.count, record.instance);
        if (added <= 0) return;   // 자리가 없다 — 드랍은 그대로 남는다

        record.count -= added;
        if (record.count > 0)
        {
            // 일부만 들어갔다. 개체 데이터는 들어간 쪽이 가져갔으므로 남은 것은 평범한 스택이 된다.
            record.instance = null;
            return;
        }

        if (owner != null) owner.RemoveDrop(record);
        record = null;
        Destroy(gameObject);
    }
}
