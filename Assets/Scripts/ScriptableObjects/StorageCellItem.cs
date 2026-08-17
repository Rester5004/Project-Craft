using UnityEngine;

/// <summary>
/// 저장 네트워크의 셀 한 종류. <b>한도 두 개가 정체성</b>이고, 내용은 스택에 붙는
/// <see cref="StorageCellInstance"/> 가 들고 다닌다 — 그래서 셀을 캐면 내용이 그대로 따라온다.
///
/// <b>한도를 인스턴스가 아니라 에셋에 두는 이유</b>는 밸런스가 반드시 여러 번 바뀌기 때문이다
/// (<see cref="UpgradeModuleItem.valuePerUnit"/> 과 같은 규약). 인스턴스에 복사해 두면
/// 이미 만들어진 셀만 옛 한도를 영원히 들고 다닌다.
/// </summary>
[CreateAssetMenu(fileName = "StorageCellItem", menuName = "Items/StorageCellItem")]
public class StorageCellItem : Items
{
    [Header("저장 셀 한도")]
    [Tooltip("담을 수 있는 아이템 종류 수. AE2 의 바이트 회계 대신 이 두 한도만 남겼다 —\n" +
             "아무거나 다 넣으면 개수보다 종류가 먼저 차는 것이 재미의 핵심이다.")]
    [Min(1)] public int typeLimit = 8;

    [Tooltip("담을 수 있는 총 개수(종류와 무관한 합계).")]
    [Min(1)] public int totalLimit = 4096;

    /// <summary>
    /// 셀은 <b>내용이 저마다 다르므로 겹쳐지면 안 된다</b>. maxStack 을 1 로 강제한다 —
    /// 에셋에서 실수로 64 로 두면 내용이 다른 셀 둘이 한 칸에 합쳐지며 한쪽이 사라진다.
    /// </summary>
    private void OnValidate()
    {
        if (maxStack != 1) maxStack = 1;
    }
}
