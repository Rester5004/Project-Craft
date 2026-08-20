using UnityEngine;

/// <summary>
/// 이동 입력을 애니메이터 파라미터로 옮기고, 멈춰 있을 때 눈 깜빡임을 띄운다.
/// <b>플레이어가 향한 방향(<see cref="Facing"/>)의 정본</b>이기도 하다 — 애니메이션과 빛이
/// 같은 값을 봐야 "스프라이트는 왼쪽인데 빛은 오른쪽" 이 생기지 않는다.
///
/// <b>파라미터 이름은 여기와 Female.controller 양쪽이 정본이다.</b> 한쪽만 바꾸면
/// <see cref="Animator"/> 가 경고 한 줄 없이 조용히 아무 일도 하지 않는다.
/// </summary>
public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("눈 깜빡임 (정면으로 멈춰 있을 때만)")]
    [SerializeField, Min(0.1f)] private float blinkIntervalMin = 2f;
    [SerializeField, Min(0.1f)] private float blinkIntervalMax = 6f;

    private float blinkTimer;

    // 마지막으로 향한 방향. <b>월드 방향이다</b>(입력 그대로) — 애니메이터에 넣을 때만 y 를 뒤집는다.
    // 둘을 섞으면 빛이 위아래 반대로 간다. 시작값은 정면(아래)이라 예전과 첫 포즈가 같다.
    private Vector2 facing = Vector2.down;

    /// <summary>마지막으로 향한 방향(월드). 플레이어 빛이 이 하나를 본다.</summary>
    public Vector2 Facing => facing;

    // 애니메이터 파라미터 이름을 매 프레임 문자열로 넘기지 않는다(해시가 더 싸다).
    private static readonly int MoveX = Animator.StringToHash("moveX");
    private static readonly int MoveY = Animator.StringToHash("moveY");
    private static readonly int Moving = Animator.StringToHash("moving");
    private static readonly int Blink = Animator.StringToHash("blink");

    private void Start() => ResetBlinkTimer();

    private void Update()
    {
        if (animator == null) return;

        Vector2 move = InputActionManager.Instance != null ? InputActionManager.Instance.MoveValue : Vector2.zero;

        // <b>(int) 로 자르지 않는다.</b> 2DVector 컴포짓이 대각선을 정규화해 (0.707, 0.707) 을 주는데
        // 예전처럼 int 로 자르면 0 이 되어, 대각선으로 걷는 내내 "가만히 있음" 으로 잡혔다.
        // 블렌드 트리도 Float 만 받는다.
        bool moving = move.sqrMagnitude > 0.01f;

        // <b>손을 떼도 0 을 쓰지 않는다.</b> Idle 도 블렌드 트리라 이 값이 곧 서 있는 방향이고,
        // 0 을 쓰면 네 클립이 같은 무게로 섞여 어느 쪽도 아닌 포즈가 된다.
        if (moving) facing = move.normalized;

        animator.SetFloat(MoveX, facing.x);
        animator.SetFloat(MoveY, -facing.y);   // 부호 규약: S(아래) = 정면(Forward), W(위) = 뒷모습(Backward)
        animator.SetBool(Moving, moving);

        UpdateBlink(moving);
    }

    /// <summary>
    /// 멈춰 있는 동안에만 시간을 재다가 깜빡임을 한 번 띄운다.
    /// 걷는 중에는 타이머를 되돌린다 — 멈추자마자 눈을 깜빡이면 반사처럼 보인다.
    /// 실제로 언제 Idle 로 돌아오는지는 컨트롤러가 정한다(Blink 는 Exit Time 1.0 이라 끝까지 재생된다).
    ///
    /// ⚠ <b>정면일 때만 쏜다.</b> 깜빡임 그림은 정면 것뿐이고 FemaleBlink.anim 은 빈 클립인데
    /// 상태가 Write Defaults 라, 옆·뒤를 보고 서 있다가 Blink 로 넘어가면 스프라이트가
    /// 프리팹 기본 그림으로 튄다. 정면 Idle 은 클립 자체가 깜빡이므로 손해가 없다.
    /// </summary>
    private void UpdateBlink(bool moving)
    {
        if (moving || facing.y > -0.5f) { ResetBlinkTimer(); return; }

        blinkTimer -= Time.deltaTime;
        if (blinkTimer > 0f) return;

        animator.SetTrigger(Blink);
        ResetBlinkTimer();
    }

    private void ResetBlinkTimer()
        => blinkTimer = Random.Range(blinkIntervalMin, Mathf.Max(blinkIntervalMin, blinkIntervalMax));
}
