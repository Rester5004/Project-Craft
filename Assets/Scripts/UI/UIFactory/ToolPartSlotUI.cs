using UnityEngine;

/// <summary>
/// 조합대의 도구 부품 칸. 지정된 부품 종류 · 허용 재질에 맞는 아이템만 받아 준다.
/// 저장소는 조합대 기계의 <b>입력 슬롯</b>이라, UI를 닫아도 부품이 그 조합대에 남고 월드 세이브에 함께 저장된다.
/// </summary>
[DisallowMultipleComponent]
public class ToolPartSlotUI : ItemSlot
{
    private ToolDefinition definition;
    private int partIndex = -1;

    /// <summary>이 칸이 어떤 도구의 몇 번째 부품인지 지정한다. definition 이 null 이면 넣기 금지(빼기만 가능).</summary>
    public void SetRequirement(ToolDefinition definition, int partIndex)
    {
        this.definition = definition;
        this.partIndex = definition != null ? partIndex : -1;
    }

    /// <summary>이 칸이 요구하는 부품 종류(요구가 없으면 null).</summary>
    public ToolPartKind RequiredKind
    {
        get
        {
            ToolPartSlot slot = definition != null ? definition.GetSlot(partIndex) : null;
            return slot != null ? slot.kind : null;
        }
    }

    protected override bool Accepts(ItemStack source)
        => definition != null && ToolFactory.IsValidPart(definition, partIndex, source);

    /// <summary>비어 있으면 무엇을 넣어야 하는지 알려 준다.</summary>
    protected override string TooltipText()
    {
        string text = base.TooltipText();
        if (!string.IsNullOrEmpty(text)) return text;

        ToolPartKind kind = RequiredKind;
        return kind != null ? kind.DisplayName + " 을(를) 넣으세요" : "";
    }
}
