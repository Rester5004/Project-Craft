using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 조명 배치물의 <c>Light2D</c> 를 <see cref="LightBlock"/> 값대로 맞추고,
/// 같은 오브젝트의 <see cref="MachineInstance"/> 가 "돌고 있는가" 에 맞춰 켜고 끈다.
///
/// <b>판정을 여기서 다시 하지 않는다.</b> 전력이 있는지·횃불이라 항상 켜지는지는
/// <see cref="MachineInstance.Update"/> 의 상시 소비 분기 한 곳이 정하고, 여기는 그 결과
/// (<see cref="MachineInstance.IsRunning"/>)만 읽는다 — 두 벌로 두면 그림과 빛이 어긋난다.
///
/// <c>Light2D</c> 는 프리팹의 자식으로 두는 것이 옳다. 청크가 언로드되면 오브젝트째 파괴되고
/// 다시 들어오면 <c>Instantiate</c> 로 새로 서므로, 빛도 자동으로 함께 살아난다
/// (바깥 목록에 등록하면 <c>OnDestroy</c> 로 빼 줘야 하는데 그 실수를 아예 없앤다).
/// </summary>
[RequireComponent(typeof(MachineInstance))]
public class LightEmitter : MonoBehaviour
{
    /// <summary>불꽃이 흔들리는 폭(세기의 비율)과 속도.</summary>
    private const float FlickerAmount = 0.12f;
    private const float FlickerSpeed = 6f;

    private MachineInstance instance;
    private Light2D light2D;
    private LightBlock block;
    private float baseIntensity;
    private float noiseSeed;

    private void Awake()
    {
        instance = GetComponent<MachineInstance>();
        light2D = GetComponentInChildren<Light2D>(true);
        noiseSeed = Random.value * 100f;
    }

    private void Start() => ApplyFromBlock();

    /// <summary>SO 값을 Light2D 에 베껴 넣는다. 배치 직후 한 번이면 된다.</summary>
    private void ApplyFromBlock()
    {
        if (instance == null || light2D == null) return;

        block = instance.Info as LightBlock;
        if (block == null)
        {
            // 조명이 아닌 기계에 이 컴포넌트가 붙었거나, 딕셔너리가 아직 안 섰다.
            Debug.LogWarning($"[LightEmitter] '{instance.blockId}' 는 LightBlock 이 아닙니다. 빛을 끕니다.", this);
            light2D.enabled = false;
            return;
        }

        light2D.lightType = Light2D.LightType.Point;
        light2D.color = block.lightColor;
        light2D.pointLightInnerRadius = block.innerRadius;
        light2D.pointLightOuterRadius = block.outerRadius;
        light2D.shadowsEnabled = block.castsShadows;
        light2D.shadowIntensity = 0.75f;   // 완전한 검정보다 조금 남기는 편이 방향을 읽기 쉽다
        baseIntensity = block.lightIntensity;
        light2D.intensity = baseIntensity;
    }

    private void Update()
    {
        if (light2D == null) return;

        // Start 보다 먼저 Bind 가 끝나지 않았을 수 있다(딕셔너리 복구 등) — 한 번 더 시도한다.
        if (block == null)
        {
            if (instance == null || instance.Info as LightBlock == null) return;
            ApplyFromBlock();
            if (block == null) return;
        }

        bool on = instance.IsRunning;
        if (light2D.enabled != on) light2D.enabled = on;
        if (!on || !block.flicker) return;

        // 불꽃. Random 이 아니라 노이즈라야 프레임마다 튀지 않고 부드럽게 흔들린다.
        float noise = Mathf.PerlinNoise(noiseSeed, Time.time * FlickerSpeed);
        light2D.intensity = baseIntensity * (1f + (noise - 0.5f) * 2f * FlickerAmount);
    }
}
