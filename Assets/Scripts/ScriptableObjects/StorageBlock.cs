using UnityEngine;

/// <summary>
/// 저장 전용 블록. 가공하지 않고 아이템을 담아 두기만 한다(상자 · 아이템 저장소).
///
/// <b>저장 칸은 새 구간이 아니라 <see cref="MachineInventory.inputSlots"/> 다.</b>
/// 5번째 구간을 만들면 <c>PlaceableRecord</c> · <c>Chunk.Save/Load</c>(세이브 버전) ·
/// <c>MapGenerator.DropSlots</c> 가 전부 따라 늘어나는데, 입력 구간에 얹으면
/// <b>세이브 변경도 드랍 처리도 평면 인덱스 변경도 없다.</b>
/// 저장 블록은 <see cref="AutoProcess"/> 가 false 라 <c>Tick</c> 이 그 칸을 재료로 볼 일이 없다.
///
/// 상자와 아이템 저장소의 차이는 <b>칸 수와 고유 최대치 두 숫자뿐</b>이라 클래스를 나누지 않는다.
/// </summary>
[CreateAssetMenu(fileName = "StorageBlock", menuName = "Blocks/StorageBlock")]
public class StorageBlock : MachineBlock
{
    [Tooltip("저장 칸 수. 상자는 40, 아이템 저장소는 1.\n" +
             "MachineInstance.ApplyConfig 가 inputSlotCount 대신 이 값을 읽는다.")]
    [Min(1)] public int storageSlotCount = 40;

    [Tooltip("한 칸의 고유 최대치. 0 이면 아이템의 maxStack 을 따른다(상자).")]
    [Min(0)] public int baseCapacity = 0;

    [Tooltip("효율 업그레이드 모듈 1개당 늘어나는 최대치. baseCapacity 가 0 이면 무시된다.\n" +
             "모듈 에셋의 valuePerUnit 은 쓰지 않는다 — 그 값은 전력·연료 절감률이라 개수와 단위가 다르다.")]
    [Min(0)] public int capacityPerUpgrade = 0;

    /// <summary>고유 최대치를 쓰는가(= 아이템의 maxStack 을 무시하는가).</summary>
    public bool HasOwnCapacity => baseCapacity > 0;

    /// <summary>
    /// 입출력 슬롯이 0 인 것이 정상이므로 기본값(3/6)으로 폴백하지 않는다.
    /// <b>이걸 켜지 않으면 ApplyConfig 가 조용히 3/6 칸을 만든다.</b>
    /// </summary>
    public override bool AllowsZeroSlots => true;

    /// <summary>저장만 한다. 레시피를 고르지도, 진행도를 돌리지도 않는다.</summary>
    public override bool AutoProcess => false;
}
