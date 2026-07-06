using UnityEngine;
using UnityEngine.UI;

public class FillingSlot : MonoBehaviour
{
    [Header("images")]
    [SerializeField] Image _underImage;
    [SerializeField] Image _fillImage;
    [SerializeField] Image _particleImage;
    [SerializeField] Image _overImage;

    [Range(0f, 1f)]
    [SerializeField] float _fillAmount = 0f;
    public float FillAmount
    {
        get
        {
            return _fillAmount;
        }

        set
        {
            float newValue = Mathf.Clamp01(value);
            _fillAmount = newValue;
            UpdateLayout(newValue);
        }
    }

    private void Update()
    {
        UpdateLayout(_fillAmount);
    }

    public void UpdateLayout(float value)
    {
        // fill amount 업데이트
        _fillImage.fillAmount = value;

        // particle 위치 업데이트
        _particleImage.rectTransform.anchoredPosition =
            new Vector2(
                0,
                _fillImage.rectTransform.rect.y + (value) * _fillImage.rectTransform.rect.height
            );

        // 많이 차있다면 particle 이미지 투명화
        if (_fillAmount > 0.9f || _fillAmount < 0.05f)
        {
            _particleImage.color = new Color(1, 1, 1, 0);
        }
        else
        {
            _particleImage.color = new Color(1, 1, 1, 1);
        }
    }

}
