using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부품에서 도구를 만들고, 만들어진 도구의 수치와 그림을 풀어 주는 순수 헬퍼.
/// 조합대 UI · 세이브 복원 · 아이콘 렌더가 모두 여기를 거치므로 규칙이 한 곳에만 있다.
/// </summary>
public static class ToolFactory
{
    /// <summary>부품 칸 <paramref name="slotIndex"/> 에 이 스택을 넣을 수 있는가.</summary>
    public static bool IsValidPart(ToolDefinition definition, int slotIndex, ItemStack stack)
    {
        ToolPartSlot slot = definition != null ? definition.GetSlot(slotIndex) : null;
        if (slot == null) return false;
        if (stack == null || stack.count <= 0 || !stack.IsPlain) return false;

        return stack.item is ToolPartItem part
            && part.kind == slot.kind
            && slot.Allows(part.material);
    }

    /// <summary>모든 부품 칸이 올바르게 채워졌는가.</summary>
    public static bool CanAssemble(ToolDefinition definition, IList<ItemStack> partStacks)
    {
        if (definition == null || definition.SlotCount == 0) return false;
        if (partStacks == null || partStacks.Count < definition.SlotCount) return false;

        for (int i = 0; i < definition.SlotCount; i++)
            if (!IsValidPart(definition, i, partStacks[i])) return false;
        return true;
    }

    /// <summary>
    /// 부품으로 도구 개체를 만든다. 부품을 소모하지는 않는다(호출자가 한다).
    /// 조건이 맞지 않으면 null.
    /// </summary>
    public static ToolInstance Create(ToolDefinition definition, IList<ItemStack> partStacks)
    {
        if (!CanAssemble(definition, partStacks)) return null;

        int count = definition.SlotCount;
        string[] materialIds = new string[count];
        ToolMaterial[] materials = new ToolMaterial[count];

        for (int i = 0; i < count; i++)
        {
            ToolPartItem part = (ToolPartItem)partStacks[i].item;
            materials[i] = part.material;
            materialIds[i] = part.MaterialId;
        }

        int durability = ComputeDurability(definition, materials);
        return new ToolInstance(materialIds, durability, durability);
    }

    /// <summary>
    /// 내구도 = 기준값 × 머리(마지막 칸) 재질의 durabilityFactor × 나머지 칸 재질의 handleFactor.
    /// 칸이 하나뿐인 도구(드라이버)는 그 칸이 곧 머리다.
    /// </summary>
    public static int ComputeDurability(ToolDefinition definition, IList<ToolMaterial> materials)
    {
        if (definition == null || materials == null || materials.Count == 0) return 1;

        float value = definition.baseDurability;
        int headIndex = materials.Count - 1;

        for (int i = 0; i < materials.Count; i++)
        {
            ToolMaterial material = materials[i];
            if (material == null) continue;
            value *= i == headIndex ? material.durabilityFactor : material.handleFactor;
        }

        return Mathf.Max(1, Mathf.RoundToInt(value));
    }

    /// <summary>완성 도구의 그림 레이어를 부품 칸 순서대로 모은다(뒤가 위). 한 장도 못 찾으면 false.</summary>
    public static bool CollectLayers(ToolDefinition definition, ToolInstance instance, List<IconLayer> results)
    {
        if (definition == null || instance == null || results == null) return false;

        ToolDictionary dictionary = ToolDictionary.Instance;
        if (dictionary == null) return false;

        int added = 0;
        for (int i = 0; i < definition.SlotCount; i++)
        {
            ToolPartSlot slot = definition.GetSlot(i);
            if (slot == null) continue;

            string materialId = instance.MaterialAt(i);
            Sprite sprite = dictionary.GetSprite(slot.SpriteNameFor(materialId));
            if (sprite == null) continue;

            ToolMaterial material = dictionary.GetMaterial(materialId);
            Color color = slot.tintByMaterial && material != null ? material.tint : Color.white;

            results.Add(new IconLayer(sprite, color));
            added++;
        }
        return added > 0;
    }
}
