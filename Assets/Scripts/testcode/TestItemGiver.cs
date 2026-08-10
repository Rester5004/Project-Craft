using UnityEngine;
using System.Collections.Generic;

// PlayerSave(실행 순서 100)가 저장된 인벤토리를 복원한 다음 지급해야
// 테스트 아이템이 로드 과정에서 다시 사라지지 않는다.
[DefaultExecutionOrder(200)]
public class TestItemGiver : MonoBehaviour
{
    private Inventory inventory;   
    public List<Items> itemToGive;   
    public int amount = 10;

    void Start()
    {
        inventory = Inventory.Instance;
        if (inventory == null) return;

        foreach(Items i in itemToGive)
        {
            if (i != null)
                inventory.AddItem(i, amount);
        }
    }
}
