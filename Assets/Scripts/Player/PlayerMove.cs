using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMove : MonoBehaviour
{
    [SerializeField] float speed = 5f;

    private Rigidbody2D rb;
    private Vector2 pendingInput;

    /// <summary>
    /// 이번 물리 스텝에 실제로 움직이려 하는가. <b>발소리가 이 하나를 본다</b>(<see cref="PlayerFootsteps"/>).
    ///
    /// <see cref="PlayerAnimation"/> 처럼 입력을 직접 읽지 않는 이유: 여기 <c>pendingInput</c> 은
    /// UI 가 열려 있으면 이미 0 이라, <b>인벤토리를 켜 둔 채 WASD 를 눌러도 발소리가 안 난다.</b>
    /// (애니메이션은 그 경우 제자리에서 걷는 것으로 보이는데, 그건 별건이다.)
    /// </summary>
    public bool IsMoving => pendingInput.sqrMagnitude > 0.01f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
    }

    void Update()
    {
        if (UIManager.Instance != null && UIManager.Instance.isAnyUIOpen && UIManager.Instance.OpenUICount > 0)
        {
            pendingInput = Vector2.zero; //UI열려있으면 이동차단
            return;
        }

        pendingInput = InputActionManager.Instance != null ? InputActionManager.Instance.MoveValue : Vector2.zero;
    }

    void FixedUpdate()
    {
        // 물리 스텝에서 이동시켜야 벽(Collider2D)과 부딪혔을 때 위치 덮어쓰기와 충돌 해결이 충돌하지 않는다.
        rb.MovePosition(rb.position + pendingInput * (speed * Time.fixedDeltaTime));
    }
}