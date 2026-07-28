using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레시피의 재료 확인 · 소모 · 산출물 적재를 담당하는 순수 헬퍼.
/// 기계 인벤토리(<see cref="MachineInventory.inputSlots"/>/<see cref="MachineInventory.outputSlots"/>)와
/// 플레이어 인벤토리(<see cref="Inventory.slots"/>)가 모두 <c>List&lt;ItemStack&gt;</c> 이므로
/// 같은 함수로 처리한다.
/// </summary>
public static class RecipeSolver
{
    // 적재 가능 여부 시뮬레이션용 재사용 버퍼. 기계마다 매 프레임 호출될 수 있어 GC 할당을 피한다.
    private static readonly List<Items> simItems = new();
    private static readonly List<int> simCounts = new();

    /// <summary>maxStack 이 0 이하(미설정)면 무제한으로 본다(ItemSlot 의 드롭 병합과 같은 규약).</summary>
    public static int MaxStackOf(Items item)
        => item != null && item.maxStack > 0 ? item.maxStack : int.MaxValue;

    /// <summary>슬롯 전체에 들어 있는 해당 아이템의 총 개수.</summary>
    public static int CountItem(IList<ItemStack> slots, Items item)
    {
        if (slots == null || item == null) return 0;

        int total = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            ItemStack stack = slots[i];
            if (stack != null && stack.item == item && stack.count > 0) total += stack.count;
        }
        return total;
    }

    /// <summary>레시피의 모든 재료가 슬롯에 충분히 있는가. 재료가 없는 레시피는 false.</summary>
    public static bool HasInputs(IList<ItemStack> slots, Recipe recipe)
    {
        if (slots == null || recipe == null || recipe.inputs == null || recipe.inputs.Count == 0) return false;

        for (int i = 0; i < recipe.inputs.Count; i++)
        {
            ItemStack need = recipe.inputs[i];
            if (need == null || need.item == null || need.count <= 0) continue;
            if (CountItem(slots, need.item) < need.count) return false;
        }
        return true;
    }

    /// <summary>재료를 실제로 차감한다. 재료가 부족하면 아무것도 건드리지 않고 false.</summary>
    public static bool ConsumeInputs(IList<ItemStack> slots, Recipe recipe)
    {
        if (!HasInputs(slots, recipe)) return false;

        for (int i = 0; i < recipe.inputs.Count; i++)
        {
            ItemStack need = recipe.inputs[i];
            if (need == null || need.item == null || need.count <= 0) continue;

            int remaining = need.count;
            for (int s = 0; s < slots.Count && remaining > 0; s++)
            {
                ItemStack stack = slots[s];
                if (stack == null || stack.item != need.item || stack.count <= 0) continue;

                int taken = Mathf.Min(stack.count, remaining);
                stack.count -= taken;
                remaining -= taken;
                if (stack.count <= 0) { stack.item = null; stack.count = 0; }
            }
        }
        return true;
    }

    /// <summary>산출물을 전부 넣을 자리가 있는가(슬롯을 변경하지 않고 시뮬레이션).</summary>
    public static bool CanStoreOutputs(IList<ItemStack> slots, Recipe recipe)
    {
        if (recipe == null || recipe.outputs == null || recipe.outputs.Count == 0) return true;
        if (slots == null) return false;

        simItems.Clear();
        simCounts.Clear();
        for (int i = 0; i < slots.Count; i++)
        {
            ItemStack stack = slots[i];
            bool has = stack != null && stack.item != null && stack.count > 0;
            simItems.Add(has ? stack.item : null);
            simCounts.Add(has ? stack.count : 0);
        }

        for (int o = 0; o < recipe.outputs.Count; o++)
        {
            ItemStack produce = recipe.outputs[o];
            if (produce == null || produce.item == null || produce.count <= 0) continue;

            int remaining = produce.count;
            int max = MaxStackOf(produce.item);

            // 같은 아이템이 있는 칸부터 채우고, 남으면 빈 칸을 쓴다(TryAdd 와 동일한 순서).
            for (int i = 0; i < simItems.Count && remaining > 0; i++)
            {
                if (simItems[i] != produce.item || simCounts[i] >= max) continue;
                int moved = Mathf.Min(max - simCounts[i], remaining);
                simCounts[i] += moved;
                remaining -= moved;
            }
            for (int i = 0; i < simItems.Count && remaining > 0; i++)
            {
                if (simItems[i] != null) continue;
                int moved = Mathf.Min(max, remaining);
                simItems[i] = produce.item;
                simCounts[i] = moved;
                remaining -= moved;
            }

            if (remaining > 0) return false;
        }
        return true;
    }

    /// <summary>산출물을 슬롯에 적재한다. 전부 넣었으면 true(부분 적재 시 false).</summary>
    public static bool StoreOutputs(IList<ItemStack> slots, Recipe recipe)
    {
        if (recipe == null || recipe.outputs == null) return true;

        bool all = true;
        for (int o = 0; o < recipe.outputs.Count; o++)
        {
            ItemStack produce = recipe.outputs[o];
            if (produce == null || produce.item == null || produce.count <= 0) continue;
            if (!TryAdd(slots, produce.item, produce.count)) all = false;
        }
        return all;
    }

    /// <summary>아이템을 슬롯에 넣는다(기존 스택 우선, 그 다음 빈 칸). 전부 넣었으면 true.</summary>
    public static bool TryAdd(IList<ItemStack> slots, Items item, int amount)
    {
        if (slots == null || item == null || amount <= 0) return false;

        int remaining = amount;
        int max = MaxStackOf(item);

        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            ItemStack stack = slots[i];
            if (stack == null || stack.item != item || stack.count >= max) continue;
            int moved = Mathf.Min(max - stack.count, remaining);
            stack.count += moved;
            remaining -= moved;
        }
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            ItemStack stack = slots[i];
            if (stack == null || stack.item != null) continue;
            int moved = Mathf.Min(max, remaining);
            stack.item = item;
            stack.count = moved;
            remaining -= moved;
        }
        return remaining == 0;
    }
}
