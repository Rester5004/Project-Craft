using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    public Inventory inventory;   // 보여줄 데이터
    public InventorySlot slotPrefab;     // 만든 슬롯 프리팹
    public Transform slotParent;  // Grid Layout Group이 붙은 Panel

    InventorySlot[] slots;

    void Start()
    {
        slots = new InventorySlot[inventory.size];
        for (int i = 0; i < inventory.size; i++)
        {
            var s = Instantiate(slotPrefab, slotParent);
            s.inventory = inventory;
            s.index = i;
            slots[i] = s;
        }
        inventory.OnChanged += Refresh; // 데이터 바뀌면 자동으로 다시 그림
        Refresh();
    }

    void Refresh() { foreach (var s in slots) s.Refresh(); }

    void OnDestroy()
    {
        if (inventory != null) inventory.OnChanged -= Refresh;
    }
}