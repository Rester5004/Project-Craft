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
    /// <summary>
    /// 개체 데이터(커스텀 도구 등)를 가진 아이템을 지급한다.
    /// 개체마다 내용이 달라 병합할 수 없으므로 빈 칸에만 들어간다.
    /// </summary>
    public bool AddItem(Items item, int amount, ItemInstance instance)
        => AddPartial(item, amount, instance) == amount;

    /// <summary>
    /// 넣을 수 있는 만큼만 넣고 실제로 넣은 개수를 돌려준다.
    /// 필드 드랍을 주울 때처럼 다 못 담아도 나머지를 필드에 남겨야 하는 경우에 쓴다.
    /// </summary>
    public int AddPartial(Items item, int amount, ItemInstance instance = null)
    {
        if (item == null || amount <= 0) return 0;

        int added = RecipeSolver.AddItems(slots, item, amount, instance);
        if (added > 0) OnChanged?.Invoke();
        return added;
    }

    public bool AddItem(Items item, int amount)
    {
        foreach (ItemStack stack in slots)
        {
            // 개체 데이터가 붙은 칸(도구)에는 절대 합치지 않는다.
            if (stack.item == item && stack.IsPlain && stack.count < item.maxStack)
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
            stack.Clear();
            selectedSlotIndex = -1;
        }

        OnChanged?.Invoke();
    }
}
