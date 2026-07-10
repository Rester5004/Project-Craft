using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerForTest : MonoBehaviour
{
    [SerializeField] float speed = 5f;

    private Rigidbody2D rb;
    private Vector2 pendingInput;

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
