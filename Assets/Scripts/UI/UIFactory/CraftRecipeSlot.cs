using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 조합대 목록의 레시피 한 칸. 결과 아이템을 보여 주고 클릭하면 제작을 요청한다.
/// 드래그/드롭이 없으므로 <see cref="ItemSlot"/> 을 상속하지 않는다.
/// 재료가 부족하면 흐리게 표시하고 클릭을 무시한다.
/// </summary>
[DisallowMultipleComponent]
public class CraftRecipeSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Image slotImage;
    [SerializeField] private Sprite selectedSlotSprite;

    [Tooltip("재료가 부족할 때의 불투명도")]
    [SerializeField, Range(0f, 1f)] private float dimmedAlpha = 0.35f;

    private Sprite defaultSlotSprite;
    private bool craftable;
    private bool selected;

    /// <summary>이 칸이 표시 중인 레시피(없으면 null).</summary>
    public Recipe Recipe { get; private set; }

    /// <summary>지금 제작 가능한가(재료 충족).</summary>
    public bool Craftable => craftable;

    /// <summary>제작 가능한 칸을 클릭했을 때 발생.</summary>
    public event System.Action<Recipe> OnClicked;

    private void Awake()
    {
        if (slotImage == null) slotImage = GetComponent<Image>();
        if (slotImage != null) defaultSlotSprite = slotImage.sprite;
    }

    /// <summary>레시피와 제작 가능 여부를 반영한다.</summary>
    public void Bind(Recipe recipe, bool isCraftable)
    {
        Recipe = recipe;
        craftable = isCraftable;

        Items output = recipe != null ? recipe.PrimaryOutput : null;
        int amount = recipe != null ? recipe.PrimaryOutputAmount : 0;
        float alpha = isCraftable ? 1f : dimmedAlpha;

        // 재료가 부족하면 흐리게. 겹침 레이어가 있는 아이템도 같은 투명도로 맞춰진다.
        ItemIconView.Apply(iconImage, output, null, alpha);

        if (countText != null)
        {
            countText.text = amount > 1 ? amount.ToString() : "";
            Color c = countText.color;
            countText.color = new Color(c.r, c.g, c.b, alpha);
        }
    }

    /// <summary>재료 상황만 바뀌었을 때 아이콘 교체 없이 흐림만 갱신한다.</summary>
    public void SetCraftable(bool isCraftable)
    {
        if (craftable == isCraftable) return;
        Bind(Recipe, isCraftable);
    }

    /// <summary>이 칸이 상세 패널에 표시되도록 선택된 상태인지.</summary>
    public void SetSelected(bool value)
    {
        selected = value;
        ApplyHighlight(false);
    }

    private void ApplyHighlight(bool hovered)
    {
        if (slotImage == null) return;
        bool lit = (hovered || selected) && selectedSlotSprite != null;
        slotImage.sprite = lit ? selectedSlotSprite : defaultSlotSprite;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 재료가 부족해도 선택은 되게 한다. 무엇이 모자란지 상세 패널에서 봐야 하기 때문.
        if (Recipe == null) return;
        OnClicked?.Invoke(Recipe);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ApplyHighlight(true);

        // 산출물이 아니라 <b>레시피가 부르는 이름</b>을 쓴다 — 도구·부품 레시피는 재질에 따라 결과가
        // 달라서 outputs[0] 이 아이콘용 견본일 뿐이다(<see cref="Recipe.ListName"/> 주석 참고).
        if (TooltipUI.Instance != null) TooltipUI.Instance.Show(Recipe != null ? Recipe.ListName : "");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ApplyHighlight(false);   // 선택된 칸은 커서가 빠져도 강조를 유지
        if (TooltipUI.Instance != null) TooltipUI.Instance.Hide();
    }
}
