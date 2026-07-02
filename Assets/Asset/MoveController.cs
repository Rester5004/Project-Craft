using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MoveController : MonoBehaviour
{
    Rigidbody2D rb;
    [SerializeField] float speed = 1; //인스펙터에서 속도조절 가능

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        float x = 0;
        float y = 0;

        //현재 프로젝트 설정에 맞춰서 Input System사용
        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x = 1;
            else if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x = -1;

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y = 1;
            else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y = -1;
        }

        Move(x, y);
    }

    void Move(float x, float y)
    {
        Vector2 p = (Vector2)transform.position + new Vector2(x, y) * speed * Time.fixedDeltaTime;
        rb.MovePosition(p);
    }
}
