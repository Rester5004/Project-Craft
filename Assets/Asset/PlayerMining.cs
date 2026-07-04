using UnityEngine;
using UnityEngine.InputSystem; // 신규 input시스템 사용....

public class PlayerMining : MonoBehaviour
{
    public int flag = 0; // 채굴하면 flag=1 인데 나중에 블록 구현하면 마저 하겠습니다.

    [Header("Mining Settings")]
    public LayerMask blockLayer;         //원하는 블록을 태그달아서 여기에 설정하면됨
    public float mineRange = 1.5f; 

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TrySelectBlock();
        }
    }

    void TrySelectBlock()
    {
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue(); //절대위치
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

        Collider2D hitCollider = Physics2D.OverlapPoint(mousePos, blockLayer); //클릭한 위치에 블록있는지 확인


        if (hitCollider != null)
        {

            Vector2 playerPos = transform.position;
            Vector2 blockPos = hitCollider.transform.position; 

            float diffX = Mathf.Abs(playerPos.x - blockPos.x);
            float diffY = Mathf.Abs(playerPos.y - blockPos.y);

            bool isAdjacent = (diffX <= mineRange && diffY < 0.5f) ||  (diffY <= mineRange && diffX < 0.5f);
            // 근방인지 확인용

            if (isAdjacent)
            {
                flag = 1;
                Debug.Log($"채굴 가능 범위! flag = {flag}. 선택된 블록: {hitCollider.name}");
                //채굴코드 넣기 그뒤에 flag=0으로 다시
            }
            else{ 
                Debug.Log("블록이 너무 멀거나 대각선 위치에 있습니다.");
            }
        }
    }
}