using UnityEngine;

/// <summary>
/// 조합대 레시피 목록의 분류(블록 / 도구 / 기계 …). 탭 하나에 대응한다.
/// 종류를 늘리려면 이 SO 에셋을 추가로 만들고 <see cref="Recipe.category"/> 에 지정하면 된다.
/// </summary>
[CreateAssetMenu(fileName = "RecipeCategory", menuName = "Recipes/Recipe Category")]
public class RecipeCategory : ScriptableObject
{
    [Tooltip("탭에 표시할 이름. 현재 UI 폰트가 한글을 지원하지 않으므로 영어를 권장한다.")]
    public string displayName;

    [Tooltip("탭 아이콘. 지정하면 아이콘 탭, 비우면 이름 텍스트만 표시한다.")]
    public Sprite icon;

    [Tooltip("탭 정렬 순서(작을수록 왼쪽).")]
    public int sortOrder;

    /// <summary>표시에 쓸 이름(displayName 이 비면 에셋 이름).</summary>
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
}
