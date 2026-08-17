using UnityEngine;

/// <summary>
/// 부품 한 종류를 <b>재질과 무관하게 레시피 하나</b>로 표현한다(막대 · 망치 머리 · 곡괭이 머리).
/// 조합대 도구 탭에서 재질 칸에 금속류를 올리면 그 재질의 부품이 나온다.
///
/// <b>재질마다 레시피를 복제하지 않는 이유</b>: 종류 3 × 재질 16 = 48개가 되고,
/// 재질이 하나 늘 때마다 3개씩 또 늘어난다. 도구 조립(<see cref="ToolRecipe"/>)이 이미
/// "부품 칸 = <see cref="ToolPartSlot"/>(종류 + 재질 필터)" 로 되어 있으므로 <b>같은 구조를 한 단계 아래에 쓴다.</b>
///
/// <see cref="Recipe.inputs"/> 가 비어 있는 것이 정상이다 — 재료가 고정 아이템이 아니라
/// <b>재질</b>이라서, 조합대가 재질 칸을 따로 띄운다(<see cref="ToolRecipe"/> 와 같은 규약).
/// </summary>
[CreateAssetMenu(fileName = "ToolPartRecipe", menuName = "Recipes/Tool Part Recipe")]
public class ToolPartRecipe : Recipe
{
    [Tooltip("무엇을 만드나(rod · hammer_head · pickaxe_head).")]
    public ToolPartKind kind;

    [Tooltip("어떤 재질을 받나. 도구 조립의 부품 칸과 같은 필터 구조다.\n" +
             "kind 는 여기서 쓰지 않는다 — 받는 것은 부품이 아니라 재료 아이템이다.")]
    public ToolPartSlot materialSlot = new ToolPartSlot();

    [Tooltip("부품 1개를 만드는 데 드는 재료 개수.")]
    [Min(1)] public int materialCost = 1;

    /// <summary>
    /// 목록에는 <b>종류 이름</b>으로 뜬다(막대 · 망치 머리 · 판). <c>outputs[0]</c> 의 견본을 그대로 쓰면
    /// "돌 망치 머리" 로 보여 <b>돌 것만 만들어지는 것처럼</b> 읽힌다 — 실제로는 넣는 재질을 따라간다.
    /// </summary>
    public override string ListName => kind != null ? kind.DisplayName : base.ListName;

    /// <summary>
    /// 이 재료 아이템이 어느 재질인가(받을 수 없으면 null).
    /// <b>재질 판정의 정본은 <see cref="ToolMaterial.sourceItem"/> 하나</b>다 —
    /// 이름 규칙으로 추측하면 `iron_ingot` 과 `raw_iron_ore` 를 구별하지 못한다.
    /// </summary>
    public ToolMaterial MaterialOf(Items item)
    {
        if (item == null || ToolDictionary.Instance == null) return null;

        foreach (ToolMaterial material in ToolDictionary.Instance.Materials)
        {
            if (material == null || material.sourceItem != item) continue;
            return materialSlot != null && !materialSlot.Allows(material) ? null : material;
        }
        return null;
    }
}
