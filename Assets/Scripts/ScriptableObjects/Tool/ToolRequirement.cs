using UnityEngine;

/// <summary>
/// 레시피가 요구하는 도구 하나. 재료와 달리 <b>소모되지 않고 내구도만 닳는다</b>.
/// 재질은 가리지 않으므로 설계도(<see cref="ToolDefinition"/>) 단위로만 지정한다.
/// </summary>
[System.Serializable]
public class ToolRequirement
{
    [Tooltip("필요한 도구의 종류.")]
    public ToolDefinition tool;

    [Tooltip("한 번 만들 때 닳는 내구도.")]
    [Min(1)] public int durabilityCost = 1;
}
