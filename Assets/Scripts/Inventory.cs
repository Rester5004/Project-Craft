using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ItemStack   // 슬롯 하나의 내용물
{
    public Items item;
    public int count;
}

public class Inventory : MonoBehaviour
{
    public int size = 20;
    public List<ItemStack> slots = new List<ItemStack>();

    // UI가 "바뀌었으니 다시 그려!"를 알 수 있게 알림
    public System.Action OnChanged;

    void Awake()
    {
        for (int i = 0; i < size; i++)
            slots.Add(new ItemStack());
    }

    // 아이템 넣기 (성공하면 true)
    public bool AddItem(Items item, int amount)
    {
        // 같은 아이템 칸에 먼저 쌓기
        foreach (var s in slots)
        {
            if (s.item == item && s.count < item.maxStack)
            {
                s.count += amount;
                OnChanged?.Invoke();
                return true;
            }
        }
        // 빈 칸에 새로 넣기
        foreach (var s in slots)
        {
            if (s.item == null)
            {
                s.item = item;
                s.count = amount;
                OnChanged?.Invoke();
                return true;
            }
        }
        return false; // 인벤토리 가득 참
    }
}