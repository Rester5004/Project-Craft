using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <b>게임을 처음 시작할 때 주는 것</b>의 정본. 세이브가 없을 때 <see cref="PlayerSave.Load"/> 가 한 번 부른다.
///
/// 지금은 돌 곡괭이 하나뿐이다. 곡괭이가 없으면 <b>벽을 한 칸도 캘 수 없어</b> 게임이 시작되지 않는다 —
/// 채굴 판정이 <see cref="ToolDefinition.canMineBlocks"/> 이고 그것이 참인 도구가 곡괭이뿐이기 때문이다.
/// 돌인 이유는 <see cref="ToolMaterial.sourceItem"/> 가 채굴로 얻는 <c>stone</c> 이라
/// 부러져도 스스로 다시 만들 수 있는 유일한 재질이라서다.
///
/// <b>규칙을 여기 모아 둔다</b> — <see cref="PlayerSave"/> 는 "세이브가 없다" 만 알고,
/// 무엇을 주는지는 몰라야 한다.
/// </summary>
public static class StartingInventory
{
    private const string ToolId = "pickaxe";
    private const string MaterialId = "stone";

    /// <summary>
    /// 시작 아이템을 넣는다. 넣을 수 없으면(딕셔너리 미등록·부품 없음) <b>경고만 남기고 넘어간다</b> —
    /// 여기서 예외를 던지면 새 게임이 아예 시작되지 않는다.
    /// </summary>
    public static void Grant(Inventory inventory)
    {
        if (inventory == null || inventory.slots == null) return;

        ToolDictionary tools = ToolDictionary.Instance;
        if (tools == null) { Debug.LogWarning("[StartingInventory] ToolDictionary 가 없어 시작 도구를 못 줬습니다."); return; }

        ToolItem output = FindTool(tools, ToolId);
        ToolDefinition definition = output != null ? output.definition : null;
        ToolMaterial material = tools.GetMaterial(MaterialId);
        if (output == null || definition == null || material == null)
        {
            Debug.LogWarning($"[StartingInventory] '{ToolId}'({MaterialId}) 를 찾지 못했습니다 — 시작 도구를 못 줬습니다.");
            return;
        }

        // 부품 칸 순서 그대로 같은 재질의 부품을 채운다. 재질 필터(Curated 등)는 ToolFactory 가 검사한다.
        List<ItemStack> parts = new(definition.SlotCount);
        for (int i = 0; i < definition.SlotCount; i++)
        {
            ToolPartSlot slot = definition.GetSlot(i);
            ToolPartItem part = slot != null ? tools.GetPart(slot.kind, material) : null;
            if (part == null)
            {
                Debug.LogWarning($"[StartingInventory] '{ToolId}' 의 {i}번 칸에 맞는 {MaterialId} 부품이 없습니다.");
                return;
            }
            parts.Add(new ItemStack { item = part, count = 1 });
        }

        ToolInstance made = ToolFactory.Create(definition, parts);
        if (made == null) { Debug.LogWarning($"[StartingInventory] '{ToolId}' 조립에 실패했습니다."); return; }

        if (!PlaceInHand(inventory, output, made) && !RecipeSolver.TryAdd(inventory.slots, output, 1, made))
        {
            Debug.LogWarning("[StartingInventory] 인벤토리에 자리가 없어 시작 도구를 못 줬습니다.");
            return;
        }
        inventory.NotifyChanged();
    }

    /// <summary>
    /// 지금 손에 든 칸(핫바의 선택 칸)이 비어 있으면 거기에 넣는다.
    ///
    /// 핫바 시작 인덱스를 여기 적지 않고 <see cref="InventoryHotBarUI.SelectedInventoryIndex"/> 에 묻는 이유는
    /// 그 숫자(30)가 핫바의 직렬화 필드라서다 — 여기 베껴 두면 둘이 갈린다.
    /// </summary>
    private static bool PlaceInHand(Inventory inventory, ToolItem tool, ToolInstance instance)
    {
        InventoryHotBarUI hotbar = Object.FindFirstObjectByType<InventoryHotBarUI>();
        if (hotbar == null) return false;

        int index = hotbar.SelectedInventoryIndex;
        if (index < 0 || index >= inventory.slots.Count) return false;

        ItemStack slot = inventory.slots[index];
        if (slot.item != null && slot.count > 0) return false;

        slot.item = tool;
        slot.count = 1;
        slot.instance = instance;
        return true;
    }

    /// <summary>
    /// <c>toolId</c> 로 완성 도구를 찾는다. <see cref="ToolDictionary"/> 에 ID 색인이 없어 목록을 훑는다 —
    /// 도구가 넷뿐이고 시작할 때 한 번만 부르므로 색인을 새로 만들 이유가 없다
    /// (색인을 늘리면 <c>Rebuild</c>·<c>EnsureIndex</c> 두 곳을 함께 고쳐야 한다).
    /// </summary>
    private static ToolItem FindTool(ToolDictionary tools, string toolId)
    {
        IReadOnlyList<ToolItem> all = tools.Tools;
        for (int i = 0; all != null && i < all.Count; i++)
        {
            ToolItem tool = all[i];
            if (tool != null && tool.definition != null && tool.definition.toolId == toolId) return tool;
        }
        return null;
    }
}
