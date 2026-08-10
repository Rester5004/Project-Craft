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

    [Tooltip("소모할 유체. 기계의 입력 탱크에서 가져간다. 양은 단위 없는 정수이고 1 양동이 = 1000 이 규약이다.")]
    public List<FluidStack> fluidInputs = new();

    [Tooltip("생산할 유체. 기계의 출력 탱크에 쌓인다. 자리가 없으면 재료를 먹지 않고 멈춘다.")]
    public List<FluidStack> fluidOutputs = new();

    [Tooltip("확률로 나오는 부산물(추출기). 여기 적는 확률은 '가장 낮은 값' 이고,\n" +
             "어느 기계가 얼마나 얻는지는 ExtractionTable 이 정한다.")]
    public List<ChanceOutput> chanceOutputs = new();

    [Tooltip("필요한 도구. 소모되지 않고 내구도만 닳는다(내구도가 0 이 되면 도구가 사라진다).")]
    public List<ToolRequirement> requiredTools = new();

    [Tooltip("가공 완료까지 걸리는 시간(초). 0 이면 즉시 완성.")]
    [Min(0f)] public float craftTime = 1f;

    [Tooltip("JSON 임포트 시 Recipe 로 옮기지 못한 원본 정보(확률 부산물·유체·전력·도구·장소·비고와 원문).")]
    [TextArea(3, 12)] public string importNote;

    /// <summary>
    /// 이 레시피를 찾을 때 쓰는 색인 키. 기본은 기계의 blockName 이지만,
    /// 0/1/2티어 화로처럼 업그레이드 관계인 기계들은 <see cref="MachineBlock.recipeGroupId"/> 를 공유해
    /// 같은 목록을 보게 된다(실제로 구울 수 있는지는 티어가 가른다).
    /// </summary>
    public string MachineBlockId => machine != null ? machine.RecipeGroupId : "";

    /// <summary>
    /// 조합대 목록에 아이콘으로 표시할 대표 산출물.
    /// 확정 산출이 없으면 <b>첫 확률 부산물</b>로 폴백한다 — 추출 레시피처럼 확률 산출만 있는 것도
    /// 목록·중복 검사에서 대표를 가져야 하기 때문이다(없으면 null 참조로 터진다).
    /// </summary>
    public Items PrimaryOutput => outputs != null && outputs.Count > 0
        ? outputs[0].item
        : (chanceOutputs != null && chanceOutputs.Count > 0 ? chanceOutputs[0].item : null);

    /// <summary>대표 산출물의 개수.</summary>
    public int PrimaryOutputAmount => outputs != null && outputs.Count > 0
        ? outputs[0].count
        : (chanceOutputs != null && chanceOutputs.Count > 0 ? chanceOutputs[0].count : 0);
}
