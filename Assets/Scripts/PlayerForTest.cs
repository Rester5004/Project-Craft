using UnityEngine;

public class PlayerForTest : MonoBehaviour
{
    [SerializeField] float speed = 5f;

    void Update()
    {
        if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen)
            return; //UI열려있으면 이동차단

        Vector2 input = InputActionManager.Instance != null ? InputActionManager.Instance.MoveValue : Vector2.zero;

        transform.position += (Vector3)(input * (speed * Time.deltaTime));
    }
}
