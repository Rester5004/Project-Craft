using UnityEngine;

/// <summary>
/// 이동 입력을 애니메이터 파라미터로 옮기고, 멈춰 있을 때 눈 깜빡임을 띄운다.
///
/// <b>파라미터 이름은 여기와 Female.controller 양쪽이 정본이다.</b> 한쪽만 바꾸면
/// <see cref="Animator"/> 가 경고 한 줄 없이 조용히 아무 일도 하지 않는다.
/// </summary>
public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("눈 깜빡임 (멈춰 있을 때만)")]
    [SerializeField, Min(0.1f)] private float blinkIntervalMin = 2f;
    [SerializeField, Min(0.1f)] private float blinkIntervalMax = 6f;

    private float blinkTimer;

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
        animator.SetFloat(MoveX, move.x);
        animator.SetFloat(MoveY, -move.y);   // 부호 규약: S(아래) = 정면, W(위) = 뒷모습
        animator.SetBool(Moving, moving);

        UpdateBlink(moving);
    }

    /// <summary>
    /// 멈춰 있는 동안에만 시간을 재다가 깜빡임을 한 번 띄운다.
    /// 걷는 중에는 타이머를 되돌린다 — 멈추자마자 눈을 깜빡이면 반사처럼 보인다.
    /// 실제로 언제 Idle 로 돌아오는지는 컨트롤러가 정한다(Blink 는 Exit Time 1.0 이라 끝까지 재생된다).
    /// </summary>
    private void UpdateBlink(bool moving)
    {
        if (moving) { ResetBlinkTimer(); return; }

        blinkTimer -= Time.deltaTime;
        if (blinkTimer > 0f) return;

        animator.SetTrigger(Blink);
        ResetBlinkTimer();
    }

    private void ResetBlinkTimer()
        => blinkTimer = Random.Range(blinkIntervalMin, Mathf.Max(blinkIntervalMin, blinkIntervalMax));
}
