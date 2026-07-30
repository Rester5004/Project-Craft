using UnityEngine;

/// <summary>
/// 완성된 커스텀 도구의 아이템. 도구 <b>종류</b> 하나당 에셋 하나이며,
/// 재질 조합과 내구도는 스택마다 붙는 <see cref="ToolInstance"/> 가 들고 있다.
/// 개체마다 내용이 달라 합쳐질 수 없으므로 <c>maxStack</c> 은 1 로 둔다.
/// </summary>
[CreateAssetMenu(fileName = "ToolItem", menuName = "Items/Tool")]
public class ToolItem : Items
{
    [Header("도구")]
    [Tooltip("이 아이템이 어떤 도구인지.")]
    public ToolDefinition definition;
}
