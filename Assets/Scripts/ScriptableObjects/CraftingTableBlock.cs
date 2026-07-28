using UnityEngine;

/// <summary>
/// 조합대 블록. 재료를 입력 슬롯이 아니라 플레이어 인벤토리에서 직접 소모하고
/// 결과도 인벤토리로 돌려준다(<see cref="CraftingTableUI"/>).
///
/// 배치(ItemDictionary.GetGameObjectFromBlockDictionary) · UI 해석(GetMachineInfo) ·
/// 설정 적용(MachineInstance.ApplyConfig)이 모두 <see cref="MachineBlock"/> 을 요구하므로
/// 이를 상속한다. 에셋에서는 입력/출력 슬롯 수를 0 으로 둔다.
/// </summary>
[CreateAssetMenu(fileName = "CraftingTableBlock", menuName = "Blocks/CraftingTableBlock")]
public class CraftingTableBlock : MachineBlock
{
    [Header("조합대 설정")]
    [Tooltip("이 조합대의 티어. recipe.tier 가 이 값 이하인 레시피만 목록에 나타난다.")]
    [Min(0)] public int tier;

    /// <summary>조합대는 입출력 슬롯이 0 인 것이 정상이므로 기본값으로 폴백하지 않는다.</summary>
    public override bool AllowsZeroSlots => true;
}
