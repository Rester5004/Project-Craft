using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems; // UI 클릭 방지를 위해 추가

public class PlayerMining : MonoBehaviour
{
    // 외부에서 함부로 못 바꾸게 private으로 보호하고 인스펙터엔 노출
    [SerializeField] private int flag = 0; 

    [Header("Mining Settings")]
    [SerializeField] private LayerMask blockLayer;         
    [SerializeField] private float mineRange = 1.5f; 

    void Update()
    {
        // 1. 인벤토리나 기계 UI 등이 열려있으면 광질 시도 자체를 차단!
        if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen)
            return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            // 2. 허공에 떠 있는 UI(버튼 등)를 클릭한 경우 광질 안 함!
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            TrySelectBlock();
        }
    }

    private void TrySelectBlock()
    {
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue(); 
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

        Collider2D hitCollider = Physics2D.OverlapPoint(mousePos, blockLayer); 

        if (hitCollider != null)
        {
            Vector2 playerPos = transform.position;
            Vector2 blockPos = hitCollider.transform.position; 

            float diffX = Mathf.Abs(playerPos.x - blockPos.x);
            float diffY = Mathf.Abs(playerPos.y - blockPos.y);

            // 근방인지 확인용 (상하좌우 십자 형태)
            bool isAdjacent = (diffX <= mineRange && diffY < 0.5f) || (diffY <= mineRange && diffX < 0.5f);

            if (isAdjacent)
            {
                flag = 1;
                Debug.Log($"채굴 가능 범위! flag = {flag}. 선택된 블록: {hitCollider.name}");
                
                // TODO: 실제 블록 파괴/아이템 획득 코드 넣기
                
                // flag = 0; // 채굴 처리 후 다시 0으로 초기화
            }
            else
            { 
                Debug.Log("블록이 너무 멀거나 대각선 위치에 있습니다.");
            }
        }
    }
}