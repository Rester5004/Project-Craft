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

        if (iconImage != null)
        {
            iconImage.enabled = output != null;
            if (output != null) iconImage.sprite = output.Icon;
            iconImage.color = new Color(1f, 1f, 1f, alpha);
        }

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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!craftable || Recipe == null) return;
        OnClicked?.Invoke(Recipe);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (slotImage != null && selectedSlotSprite != null) slotImage.sprite = selectedSlotSprite;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (slotImage != null) slotImage.sprite = defaultSlotSprite;
    }
}
