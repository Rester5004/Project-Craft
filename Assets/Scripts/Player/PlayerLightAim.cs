using UnityEngine;

/// <summary>
/// 플레이어의 원뿔 빔을 <see cref="PlayerAnimation.Facing"/> 쪽으로 돌린다.
///
/// <b>방향은 값이 아니라 매 프레임 상태라 <see cref="LightingPalette"/> 에 두지 않는다.</b>
/// 팔레트는 "얼마나 넓고 밝은가" 만 갖고, "어디를 보는가" 는 여기 하나가 정한다.
///
/// ⚠ <b>콘의 중심은 광원의 로컬 +Y 다</b>(URP Light2D 규약 — Light2DLookupTexture 가 각을
/// <c>Vector2.down</c> 기준으로 굽고 셰이더가 그것을 뒤집어 쓴다). 그래서 방향각에서 90 을 뺀다.
/// 이 -90 을 빼먹으면 빛이 보는 쪽의 <b>왼쪽 90도</b>를 비춘다.
/// </summary>
public class PlayerLightAim : MonoBehaviour
{
    [Tooltip("방향의 정본. 같은 프리팹 안(TestPlayer)이라 직렬화 참조로 꽂는다.")]
    [SerializeField] private PlayerAnimation source;

    // 마지막으로 반영한 방향. 즉시 스냅이라 <b>바뀐 프레임에만</b> transform 을 건드린다 —
    // 매 프레임 쓰면 계층이 계속 dirty 가 되고 이득이 없다.
    private Vector2 applied = Vector2.zero;

    private void Awake()
    {
        if (source == null) source = GetComponentInParent<PlayerAnimation>();
        if (source == null)
            Debug.LogError("[PlayerLightAim] PlayerAnimation 을 못 찾았습니다. 빔이 정면에 고정됩니다.", this);
    }

    // PlayerAnimation.Update 가 방향을 정한 뒤에 읽어야 같은 프레임에 반영된다.
    private void LateUpdate()
    {
        if (source == null) return;

        Vector2 f = source.Facing;
        if (f == applied) return;
        applied = f;

        float z = Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg - 90f;
        transform.localRotation = Quaternion.Euler(0f, 0f, z);
    }
}
