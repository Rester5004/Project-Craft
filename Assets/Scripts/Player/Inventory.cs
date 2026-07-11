using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ItemStack
{
    public Items item;
    public int count;
}

// Hotbar: inventory slots 30 through 39.
public class Inventory : Singleton<Inventory>
{
    public int size;
    public List<ItemStack> slots;
    public int selectedSlotIndex { get; private set; } = -1;
    public System.Action OnChanged;

    protected override void Awake()
    {
        base.Awake();
        slots = new List<ItemStack>(size);
        for (int i = 0; i < size; i++)
            slots.Add(new ItemStack());
    }

    public bool AddItem(Items item, int amount)
    {
        foreach (ItemStack stack in slots)
        {
            if (stack.item == item && stack.count < item.maxStack)
            {
                stack.count += amount;
                OnChanged?.Invoke();
                return true;
            }
        }

        foreach (ItemStack stack in slots)
        {
            if (stack.item == null)
            {
                stack.item = item;
                stack.count = amount;
                OnChanged?.Invoke();
                return true;
            }
        }

        return false;
    }

    public ItemStack GetSelectedStack()
    {
        if (selectedSlotIndex < 0 || selectedSlotIndex >= slots.Count) return null;

        ItemStack stack = slots[selectedSlotIndex];
        return stack.item != null && stack.count > 0 ? stack : null;
    }

    public void SelectSlot(int index)
    {
        if (index >= 0 && index < slots.Count)
            selectedSlotIndex = index;
    }

    public void ConsumeSelectedItem()
    {
        ItemStack stack = GetSelectedStack();
        if (stack == null) return;

        stack.count--;
        if (stack.count == 0)
        {
            stack.item = null;
            selectedSlotIndex = -1;
        }

        OnChanged?.Invoke();
    }
}
