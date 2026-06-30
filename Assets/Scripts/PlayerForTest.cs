using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerForTest : MonoBehaviour
{
    [SerializeField] float speed = 5f;

    void Update()
    {
        Vector2 input = Keyboard.current != null
            ? new Vector2(
                (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f),
                (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f))
            : Vector2.zero;

        transform.position += (Vector3)(input * (speed * Time.deltaTime));
    }
}
