using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬에 등록된 <see cref="Recipe"/> 를 기계(blockId)별로 색인한다.
/// <see cref="ItemDictionary"/> 와 같은 방식으로 인스펙터 리스트를 Awake 에서 Dictionary 로 만든다.
/// </summary>
public class RecipeDictionary : Singleton<RecipeDictionary>
{
    [Header("Recipes")]
    [SerializeField] private List<Recipe> recipesList = new();

    private readonly Dictionary<string, List<Recipe>> byMachine = new();
    private static readonly List<Recipe> Empty = new();

    protected override void Awake()
    {
        base.Awake();
        Rebuild();
    }

    /// <summary>인스펙터 리스트로 색인을 다시 만든다.</summary>
    public void Rebuild()
    {
        byMachine.Clear();
        foreach (Recipe recipe in recipesList)
        {
            if (recipe == null) continue;

            string blockId = recipe.MachineBlockId;
            if (string.IsNullOrEmpty(blockId))
            {
                Debug.LogWarning($"[RecipeDictionary] '{recipe.name}' 에 기계가 지정되지 않아 건너뜁니다.", recipe);
                continue;
            }

            if (!byMachine.TryGetValue(blockId, out List<Recipe> list))
            {
                list = new List<Recipe>();
                byMachine[blockId] = list;
            }
            list.Add(recipe);
        }
    }

    /// <summary>해당 기계가 쓸 수 있는 모든 레시피(없으면 빈 목록).</summary>
    public IReadOnlyList<Recipe> GetRecipesFor(string blockId)
    {
        // 플레이 중 스크립트 재컴파일(도메인 리로드)로 색인만 비면 Awake 가 다시 불리지 않으므로 복구한다.
        if (byMachine.Count == 0 && recipesList.Count > 0) Rebuild();

        if (!string.IsNullOrEmpty(blockId) && byMachine.TryGetValue(blockId, out List<Recipe> list))
            return list;
        return Empty;
    }

    private static bool Contains(string source, string query)
        => !string.IsNullOrEmpty(source)
           && ItemDictionary.NormalizeName(source).IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>등록된 전체 레시피 수(진단용).</summary>
    public int RecipeCount => recipesList.Count;

    // ── 조합대용 조회 (카테고리 · 티어 · 검색) ──────────────────
    // 호출자가 넘긴 리스트를 재사용해 목록을 다시 그릴 때마다 할당하지 않는다.

    /// <summary>maxTier 안에 레시피가 하나라도 있는 카테고리를 sortOrder 순으로 모은다(탭 구성용).</summary>
    public void CollectCategories(string blockId, int maxTier, List<RecipeCategory> results)
    {
        if (results == null) return;
        results.Clear();

        int uncategorized = 0;

        IReadOnlyList<Recipe> all = GetRecipesFor(blockId);
        for (int i = 0; i < all.Count; i++)
        {
            Recipe recipe = all[i];
            if (recipe == null || recipe.tier > maxTier) continue;
            if (recipe.category == null) { uncategorized++; continue; }
            if (!results.Contains(recipe.category)) results.Add(recipe.category);
        }

        // 카테고리가 없으면 어느 탭에도 못 들어가 목록에서 통째로 사라진다.
        // 조용히 없어지면 원인을 찾기 어려우므로 알려 준다(Assign Recipe Categories 로 채울 수 있다).
        if (uncategorized > 0)
            Debug.LogWarning($"[RecipeDictionary] '{blockId}' 의 레시피 {uncategorized}개가 카테고리가 없어 탭에 나타나지 않습니다. "
                + "Tools/Project Craft/Recipes/Assign Recipe Categories 를 실행하세요.");

        results.Sort(CompareCategories);
    }

    private static int CompareCategories(RecipeCategory a, RecipeCategory b)
    {
        int byOrder = a.sortOrder.CompareTo(b.sortOrder);
        return byOrder != 0 ? byOrder : string.CompareOrdinal(a.DisplayName, b.DisplayName);
    }

    /// <summary>카테고리 · 티어 · 검색어로 거른 레시피를 모은다(그리드 구성용).</summary>
    /// <param name="category">null 이면 카테고리를 가리지 않는다.</param>
    /// <param name="search">비어 있지 않으면 산출물 이름의 대소문자 무시 부분 일치로 거른다.</param>
    public void CollectRecipes(string blockId, RecipeCategory category, int maxTier, string search, List<Recipe> results)
    {
        if (results == null) return;
        results.Clear();

        bool hasSearch = !string.IsNullOrWhiteSpace(search);
        string query = hasSearch ? ItemDictionary.NormalizeName(search.Trim()) : null;

        IReadOnlyList<Recipe> all = GetRecipesFor(blockId);
        for (int i = 0; i < all.Count; i++)
        {
            Recipe recipe = all[i];
            if (recipe == null || recipe.tier > maxTier) continue;
            if (category != null && recipe.category != category) continue;

            if (hasSearch)
            {
                Items output = recipe.PrimaryOutput;
                if (output == null) continue;
                // 한글 표시 이름과 영어 ID 양쪽으로 검색되게 한다.
                if (!Contains(output.DisplayName, query) && !Contains(output.itemName, query)) continue;
            }

            results.Add(recipe);
        }
    }
}
