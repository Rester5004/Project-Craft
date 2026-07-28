using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 조합대 카테고리 탭 하나. 배경 · 아이콘 · 이름을 각각 따로 들고 있어
/// 아이콘이 배경을 덮어쓰거나 탭 크기에 맞춰 늘어나지 않는다(아이콘은 원본 비율 유지).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class CraftingTableTab : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;

    private Button button;

    /// <summary>이 탭의 버튼(클릭 구독용).</summary>
    public Button Button => button != null ? button : (button = GetComponent<Button>());

    /// <summary>이 탭이 나타내는 카테고리.</summary>
    public RecipeCategory Category { get; private set; }

    /// <summary>카테고리를 반영한다. 아이콘이 없으면 이름만 표시한다.</summary>
    public void Bind(RecipeCategory category)
    {
        Category = category;

        if (label != null) label.text = category != null ? category.DisplayName : "";

        if (icon == null) return;

        bool hasIcon = category != null && category.icon != null;
        icon.gameObject.SetActive(hasIcon);   // 레이아웃에서도 빠지도록 오브젝트째 끈다
        if (hasIcon)
        {
            icon.sprite = category.icon;
            icon.preserveAspect = true;       // 탭 크기와 무관하게 원본 비율 유지
        }
    }

    /// <summary>선택 상태를 배경 색으로 표시한다.</summary>
    public void SetSelected(bool selected, Color selectedColor, Color normalColor)
    {
        if (background != null) background.color = selected ? selectedColor : normalColor;
    }
}
