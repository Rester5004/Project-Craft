using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기계 하나가 처리하는 가공/조합 레시피. 아이템을 <see cref="Items"/> 로 직접 참조하므로
/// 이름 매칭이 필요 없다(StreamingAssets 의 JSON 은 한글명이라 별도 임포터가 있어야 한다).
/// </summary>
[CreateAssetMenu(fileName = "Recipe", menuName = "Recipes/Recipe")]
public class Recipe : ScriptableObject
{
    [Tooltip("이 레시피를 처리하는 기계. 지정하지 않으면 어떤 기계도 사용하지 않는다.")]
    public MachineBlock machine;

    [Tooltip("조합대 목록에서 이 레시피가 속할 탭. 일반 기계 레시피는 비워도 된다.")]
    public RecipeCategory category;

    [Tooltip("필요 티어. 조합대 티어가 이 값 이상일 때만 목록에 나타난다.")]
    [Min(0)] public int tier;

    [Tooltip("소모할 재료. 일반 기계는 입력 슬롯에서, 조합대는 플레이어 인벤토리에서 가져간다.")]
    public List<ItemStack> inputs = new();

    [Tooltip("생산할 아이템. 첫 항목이 조합대 목록에 표시되는 대표 산출물이다.")]
    public List<ItemStack> outputs = new();

    [Tooltip("가공 완료까지 걸리는 시간(초). 0 이면 즉시 완성.")]
    [Min(0f)] public float craftTime = 1f;

    /// <summary>이 레시피가 속한 기계의 blockId(= <see cref="BlockBase.blockName"/>).</summary>
    public string MachineBlockId => machine != null ? machine.blockName : "";

    /// <summary>조합대 목록에 아이콘으로 표시할 대표 산출물.</summary>
    public Items PrimaryOutput => outputs != null && outputs.Count > 0 ? outputs[0].item : null;

    /// <summary>대표 산출물의 개수.</summary>
    public int PrimaryOutputAmount => outputs != null && outputs.Count > 0 ? outputs[0].count : 0;
}
