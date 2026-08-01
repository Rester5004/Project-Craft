using UnityEngine;

/// <summary>
/// 파이프의 연결면을 설정하는 렌치.
///
/// 필드가 하나도 없는데도 별도 클래스인 이유는 <b>손에 든 것이 렌치인지 타입으로 묻기 위해서다</b>.
/// <c>itemName == "wrench"</c> 문자열 비교를 쓰면 에셋 이름을 고치는 순간 조용히 동작이 멈춘다.
/// 나중에 상위 등급 렌치가 생겨도 이 클래스를 그대로 쓰면 판정하는 쪽 분기가 늘지 않는다.
///
/// 부품 조합도 내구도도 없는 단순 아이템이라 <see cref="ToolItem"/> 이 아니라 <see cref="Items"/> 를 상속한다.
/// </summary>
[CreateAssetMenu(fileName = "WrenchItem", menuName = "Items/Wrench")]
public class WrenchItem : Items
{
}
