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

    private Color originalParticleColor;
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
    private bool captured;   // 원래 파티클 색을 이미 기억했는가

    /// <summary>
    /// 파티클의 <b>손대기 전</b> 색을 기억한다. <c>Awake</c> 와 <see cref="UpdateLayout"/> 중
    /// <b>먼저 오는 쪽</b>에서 부른다 — 어느 쪽이 먼저일지 정해져 있지 않기 때문이다.
    ///
    /// 예전에는 Start 에서 잡았는데, 기계 UI 를 여는 경로가 같은 호출 스택 안에서
    /// <c>SetActive(true)</c> → <c>AttachUI</c> → <c>PushProgress/PushFuel/PushEnergy</c> →
    /// <c>FillAmount</c> setter → UpdateLayout 까지 내려온다. 그 시점의 값은 아직
    /// default(투명 검정)라 UpdateLayout 이 파티클을 <b>불투명 검정</b>으로 칠했고,
    /// 다음 프레임의 Start 가 그 검정을 "원래 색" 으로 기억해 세션 내내 검게 남았다.
    /// Awake 로 옮기는 것만으로는 부족하다 — 비활성 오브젝트는 Awake 가 아예 돌지 않아서,
    /// 그 사이에 setter 가 닿으면 같은 일이 벌어진다.
    /// </summary>
    private void Capture()
    {
        if (captured || _particleImage == null) return;
        captured = true;
        originalParticleColor = _particleImage.color;
    }

    private void Awake() => Capture();

    private void Update()
    {
        UpdateLayout(_fillAmount);
    }

    public void UpdateLayout(float value)
    {
        // 인스펙터 배선이 비어 있어도 터지지 않는다(코드로 만드는 슬롯 프리팹이 있다).
        if (_fillImage == null || _particleImage == null) return;
        Capture();

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
            _particleImage.color = new Color(originalParticleColor.r, originalParticleColor.g, originalParticleColor.b, 0);
        }
        else
        {
            _particleImage.color = new Color(originalParticleColor.r, originalParticleColor.g, originalParticleColor.b, 1);
        }
    }

}
