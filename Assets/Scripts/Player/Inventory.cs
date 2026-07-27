using System.Collections.Generic;
using UnityEngine;

// Hotbar: inventory slots 30 through 39.
public class Inventory : Singleton<Inventory>, IItemContainer
{
    public int size;
    public List<ItemStack> slots;
    private int selectedSlotIndex=30;
    public System.Action OnChanged;

    // ── IItemContainer (슬롯 UI 바인딩용) ──────────────────────────────
    public int Capacity => slots.Count;
    public ItemStack GetStack(int index) => slots[index];
    public void NotifyChanged() => OnChanged?.Invoke();

    protected override void Awake()
    {
        base.Awake();
        slots = new List<ItemStack>(size);
        for (int i = 0; i < size; i++)
            slots.Add(new ItemStack());
    }
    /// <summary>현재 선택된 슬롯의 전체 인덱스(핫바는 30~39). 저장/복원에 사용.</summary>
    public int SelectedSlotIndex => selectedSlotIndex;

    public void SetSeclectedSlotIndex(int index)
    {
        if (index<=39 && index>=0){
            selectedSlotIndex = index+30;
        }
        else
        {
            Debug.LogError("Selected slot index out of range. Must be between 0 and 9.");
        }
    }
    public ItemStack GetSelectedItem()
    {
        if (selectedSlotIndex < 0 || selectedSlotIndex >= slots.Count)
        {
            throw new System.IndexOutOfRangeException("Selected slot index is out of range.");
        }
        return slots[selectedSlotIndex];
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
