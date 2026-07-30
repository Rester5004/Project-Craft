using UnityEngine;

/// <summary>
/// 커스텀 도구를 조립하는 레시피. 재료가 <b>구체적인 아이템이 아니라 부품 종류</b>라서
/// 일반 <see cref="Recipe.inputs"/> 로는 표현할 수 없고, 조합대가 부품 칸을 따로 띄운다.
///
/// <see cref="Recipe.outputs"/> 에는 결과 <see cref="ToolItem"/> 을 1개 넣어 둔다.
/// 그래야 목록 아이콘 · 검색 · 적재 공간 검사가 일반 레시피와 똑같이 동작한다.
/// </summary>
[CreateAssetMenu(fileName = "ToolRecipe", menuName = "Recipes/Tool Recipe")]
public class ToolRecipe : Recipe
{
    [Header("도구 조립")]
    [Tooltip("만들 도구의 설계도. 필요한 부품 칸이 여기서 나온다.")]
    public ToolDefinition tool;

    /// <summary>결과 도구 아이템(outputs 첫 항목). 없으면 null.</summary>
    public ToolItem ToolOutput => PrimaryOutput as ToolItem;
}
