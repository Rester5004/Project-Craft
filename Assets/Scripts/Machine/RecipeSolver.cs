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
    private static readonly List<bool> simPlain = new();   // 개체 데이터가 붙은 칸은 병합 대상이 아니다

    /// <summary>maxStack 이 0 이하(미설정)면 무제한으로 본다(ItemSlot 의 드롭 병합과 같은 규약).</summary>
    public static int MaxStackOf(Items item)
        => item != null && item.maxStack > 0 ? item.maxStack : int.MaxValue;

    /// <summary>
    /// 슬롯 전체에 들어 있는 해당 아이템의 총 개수.
    /// 개체 데이터가 붙은 스택(커스텀 도구 등)은 <b>세지 않는다</b> — 재료로 소모되면 안 되기 때문.
    /// </summary>
    public static int CountItem(IList<ItemStack> slots, Items item)
    {
        if (slots == null || item == null) return 0;

        int total = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            ItemStack stack = slots[i];
            if (stack != null && stack.item == item && stack.IsPlain && stack.count > 0) total += stack.count;
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

    /// <summary>재료와 도구가 모두 갖춰졌는가. 실제 제작 가능 여부는 이 함수로 판단한다.</summary>
    public static bool CanCraft(IList<ItemStack> slots, Recipe recipe)
        => HasInputs(slots, recipe) && HasTools(slots, recipe);

    // ── 도구 요구 (소모가 아니라 내구도 차감) ────────────────────

    /// <summary>레시피가 요구하는 도구를 모두 가지고 있고 내구도가 남아 있는가.</summary>
    public static bool HasTools(IList<ItemStack> slots, Recipe recipe)
    {
        if (recipe == null || recipe.requiredTools == null || recipe.requiredTools.Count == 0) return true;
        if (slots == null) return false;

        for (int i = 0; i < recipe.requiredTools.Count; i++)
            if (FindTool(slots, recipe.requiredTools[i]) < 0) return false;
        return true;
    }

    /// <summary>요구 하나만 따로 확인한다(어떤 도구가 없는지 줄 단위로 보여 줄 때 쓴다).</summary>
    public static bool HasTool(IList<ItemStack> slots, ToolRequirement requirement)
        => FindTool(slots, requirement) >= 0;

    /// <summary>요구된 도구의 내구도를 깎는다. 0 이 되면 도구가 사라진다.</summary>
    public static void ConsumeTools(IList<ItemStack> slots, Recipe recipe)
    {
        if (recipe == null || recipe.requiredTools == null || slots == null) return;

        for (int i = 0; i < recipe.requiredTools.Count; i++)
        {
            ToolRequirement requirement = recipe.requiredTools[i];
            int index = FindTool(slots, requirement);
            if (index < 0) continue;

            ItemStack stack = slots[index];
            ToolInstance tool = (ToolInstance)stack.instance;
            tool.durability -= requirement.durabilityCost;
            if (tool.durability <= 0) stack.Clear();   // 다 닳은 도구는 소멸
        }
    }

    /// <summary>요구를 만족하는 도구가 든 슬롯 번호(없으면 -1). 내구도가 적게 남은 것부터 쓴다.</summary>
    private static int FindTool(IList<ItemStack> slots, ToolRequirement requirement)
    {
        if (slots == null || requirement == null || requirement.tool == null) return -1;

        int best = -1;
        int bestDurability = int.MaxValue;

        for (int i = 0; i < slots.Count; i++)
        {
            ItemStack stack = slots[i];
            if (stack == null || stack.count <= 0) continue;
            if (stack.item is not ToolItem item || item.definition != requirement.tool) continue;
            if (stack.instance is not ToolInstance tool) continue;
            if (tool.durability < requirement.durabilityCost) continue;

            if (tool.durability >= bestDurability) continue;
            bestDurability = tool.durability;
            best = i;
        }
        return best;
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
                if (stack == null || stack.item != need.item || !stack.IsPlain || stack.count <= 0) continue;

                int taken = Mathf.Min(stack.count, remaining);
                stack.count -= taken;
                remaining -= taken;
                if (stack.count <= 0) stack.Clear();
            }
        }
        return true;
    }

    /// <summary>
    /// 산출물을 전부 넣을 자리가 있는가(슬롯을 변경하지 않고 시뮬레이션).
    ///
    /// <paramref name="blockId"/> 를 주면 <b>확률 부산물까지 자리를 잡아 둔다</b> —
    /// 무엇이 당첨될지 모르므로 <b>나올 수 있는 것 전부</b>가 들어갈 자리가 있을 때만 진행한다.
    /// 그래야 굴린 뒤 자리가 없어 버리는 일도, 자리가 날 때까지 매 프레임 다시 굴리는 편법도 없다.
    /// 추출기 출력이 9칸인 이유가 이것이다(한 레시피의 후보가 최대 7종).
    /// </summary>
    public static bool CanStoreOutputs(IList<ItemStack> slots, Recipe recipe, string blockId = null)
    {
        if (recipe == null) return true;

        bool hasFixed = recipe.outputs != null && recipe.outputs.Count > 0;
        bool hasChance = recipe.chanceOutputs != null && recipe.chanceOutputs.Count > 0;
        if (!hasFixed && !hasChance) return true;
        if (slots == null) return false;

        simItems.Clear();
        simCounts.Clear();
        simPlain.Clear();
        for (int i = 0; i < slots.Count; i++)
        {
            ItemStack stack = slots[i];
            bool has = stack != null && stack.item != null && stack.count > 0;
            simItems.Add(has ? stack.item : null);
            simCounts.Add(has ? stack.count : 0);
            simPlain.Add(has && stack.IsPlain);   // 도구가 든 칸은 "차 있지만 합칠 수 없는" 칸
        }

        if (hasFixed)
            for (int o = 0; o < recipe.outputs.Count; o++)
            {
                ItemStack produce = recipe.outputs[o];
                if (produce == null || produce.item == null || produce.count <= 0) continue;
                if (!SimulateAdd(produce.item, produce.count)) return false;
            }

        if (hasChance)
            for (int o = 0; o < recipe.chanceOutputs.Count; o++)
            {
                ChanceOutput produce = recipe.chanceOutputs[o];
                if (produce == null || produce.item == null || produce.count <= 0) continue;
                if (ExtractionTable.Multiplier(blockId, produce.item) <= 0f) continue;  // 이 기계는 못 얻는다
                if (!SimulateAdd(produce.item, produce.count)) return false;
            }

        return true;
    }

    /// <summary>시뮬레이션 슬롯에 넣어 본다(실제 슬롯은 건드리지 않는다). 다 넣었으면 true.</summary>
    private static bool SimulateAdd(Items item, int count)
    {
        int remaining = count;
        int max = MaxStackOf(item);

        // 같은 아이템이 있는 칸부터 채우고, 남으면 빈 칸을 쓴다(TryAdd 와 동일한 순서).
        for (int i = 0; i < simItems.Count && remaining > 0; i++)
        {
            if (!simPlain[i] || simItems[i] != item || simCounts[i] >= max) continue;
            int moved = Mathf.Min(max - simCounts[i], remaining);
            simCounts[i] += moved;
            remaining -= moved;
        }
        for (int i = 0; i < simItems.Count && remaining > 0; i++)
        {
            if (simItems[i] != null) continue;
            int moved = Mathf.Min(max, remaining);
            simItems[i] = item;
            simCounts[i] = moved;
            simPlain[i] = true;
            remaining -= moved;
        }
        return remaining <= 0;
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
        => AddItems(slots, item, amount, null) == amount;

    /// <summary>
    /// 개체 데이터를 가진 아이템(커스텀 도구 등)을 넣는다.
    /// 개체마다 내용이 달라 기존 스택과 합칠 수 없으므로 <b>빈 칸만</b> 찾는다.
    /// </summary>
    public static bool TryAdd(IList<ItemStack> slots, Items item, int amount, ItemInstance instance)
        => AddItems(slots, item, amount, instance) == amount;

    /// <summary>
    /// 지금 이 슬롯들이 이 아이템을 몇 개까지 받을 수 있는지 <b>넣어 보지 않고</b> 센다.
    /// 파이프가 "보내 봐야 못 받는 곳"으로 짐을 실어 보내지 않도록 미리 확인하는 데 쓴다.
    /// </summary>
    public static int CountFreeSpace(IList<ItemStack> slots, Items item, bool hasInstance = false)
    {
        if (slots == null || item == null) return 0;

        int max = MaxStackOf(item);
        // <b>long 으로 센다.</b> maxStack 이 0(미설정)이면 MaxStackOf 가 int.MaxValue 를 돌려주는데,
        // int 로 더하면 빈 칸 두 개만에 음수로 넘쳐(2147483647 + 2147483647 = -2) 호출자가
        // "자리 없음" 으로 읽는다 — 빈 칸이 짝수인 동안만 운송이 멈추는 형태로 나타났다.
        long room = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            ItemStack stack = slots[i];
            if (stack == null) continue;

            if (stack.item == null) { room += max; continue; }

            // 개체 데이터가 붙은 짐은 기존 스택에 합칠 수 없어 빈 칸만 센다.
            if (hasInstance) continue;
            if (stack.item == item && stack.IsPlain && stack.count < max) room += max - stack.count;
        }
        return room > int.MaxValue ? int.MaxValue : (int)room;
    }

    /// <summary>
    /// 넣을 수 있는 만큼만 넣고 <b>실제로 넣은 개수</b>를 돌려준다.
    /// 다 못 넣어도 나머지를 호출자가 알 수 있으므로, 필드 드랍 줍기처럼
    /// "일부만 주워지는" 상황에서 남은 것이 증발하지 않는다.
    /// </summary>
    public static int AddItems(IList<ItemStack> slots, Items item, int amount, ItemInstance instance = null)
    {
        if (slots == null || item == null || amount <= 0) return 0;

        int remaining = amount;
        int max = MaxStackOf(item);

        if (instance == null)
        {
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                ItemStack stack = slots[i];
                if (stack == null || stack.item != item || !stack.IsPlain || stack.count >= max) continue;
                int moved = Mathf.Min(max - stack.count, remaining);
                stack.count += moved;
                remaining -= moved;
            }
        }

        // 개체 데이터가 붙은 짐은 <b>칸당 하나</b>다. 개체마다 내용(내구도)이 다른데 한 칸에 여러 개를 넣으면
        // 인스턴스는 하나뿐이라, 그 하나가 닳아 없어질 때 stack.Clear() 로 N 개가 통째로 사라진다.
        // 지금은 개체를 다는 아이템(ToolItem)이 전부 maxStack 1 이라 실제로는 걸리지 않지만,
        // 그 규약은 에셋 값이라 언제든 어긋날 수 있다 — 여기서 구조적으로 막는다.
        int perSlot = instance == null ? max : 1;

        bool first = true;
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            ItemStack stack = slots[i];
            if (stack == null || stack.item != null) continue;
            int moved = Mathf.Min(perSlot, remaining);
            stack.item = item;
            stack.count = moved;
            // 여러 칸에 나뉘어 들어가면 칸마다 별개의 개체여야 한다.
            stack.instance = instance == null ? null : (first ? instance : instance.Clone());
            first = false;
            remaining -= moved;
        }
        return amount - remaining;
    }
}
