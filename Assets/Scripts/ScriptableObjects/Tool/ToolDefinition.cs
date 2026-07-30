using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도구 한 종류의 설계도(곡괭이 · 망치 · 드라이버 …).
/// 필요한 부품 칸과 완성 그림의 레이어를 <see cref="slots"/> 하나로 함께 기술한다.
///
/// 도구를 늘리려면 이 에셋을 하나 만들고 <see cref="ToolItem"/> · <see cref="ToolRecipe"/> 를 붙이면 된다.
/// </summary>
[CreateAssetMenu(fileName = "ToolDefinition", menuName = "Tools/Tool Definition")]
public class ToolDefinition : ScriptableObject
{
    [Tooltip("내부 ID(영문 소문자). 예: pickaxe")]
    public string toolId;

    [Tooltip("화면에 표시할 이름(한글). 예: 곡괭이")]
    public string displayName;

    [Tooltip("레시피 목록에 보일 대표 아이콘(재질이 정해지기 전이라 고정 그림을 쓴다).")]
    public Sprite listIcon;

    [Tooltip("기준 내구도. 실제 내구도 = 기준 × 머리 재질 durabilityFactor × 나머지 부품 handleFactor.")]
    [Min(1)] public int baseDurability = 100;

    [Tooltip("부품 칸이자 그림 레이어. 순서가 곧 겹치는 순서(뒤가 위)이고, 마지막 칸을 '머리'로 본다.")]
    public List<ToolPartSlot> slots = new();

    /// <summary>표시에 쓸 이름(displayName 이 비면 toolId, 그것도 비면 에셋 이름).</summary>
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(displayName)) return displayName;
            return string.IsNullOrEmpty(toolId) ? name : toolId;
        }
    }

    /// <summary>조합에 필요한 부품 칸 수.</summary>
    public int SlotCount => slots != null ? slots.Count : 0;

    /// <summary>범위를 벗어나면 null.</summary>
    public ToolPartSlot GetSlot(int index)
        => slots != null && index >= 0 && index < slots.Count ? slots[index] : null;
}
