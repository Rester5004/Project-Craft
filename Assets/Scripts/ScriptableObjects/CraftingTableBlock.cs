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
    // 티어는 MachineBlock 으로 올라갔다(모든 기계가 티어로 레시피를 거른다). 필드 이름이 같아 기존 에셋은 그대로 읽힌다.

    [Tooltip("업그레이드 칸에 아이템을 넣어 티어를 올릴 수 있는 조합대인가(코어 조합기만 켠다).\n" +
             "켜면 그 칸은 성능 모듈이 아니라 CoreUpgradeTable 의 재료를 받고, 넣으면 소모된다.")]
    public bool acceptsTierUpgrade = false;

    /// <summary>조합대는 입출력 슬롯이 0 인 것이 정상이므로 기본값으로 폴백하지 않는다.</summary>
    public override bool AllowsZeroSlots => true;

    /// <summary>
    /// 조합대는 자동으로 가공하지 않는다. 입력 슬롯은 도구 부품을 놓는 자리이고,
    /// 제작은 플레이어가 조합 버튼을 눌러야 일어난다.
    /// </summary>
    public override bool AutoProcess => false;
}
