using UnityEngine;

/// <summary>
/// 레시피의 <b>확률 부산물</b> 한 줄. 추출기가 분쇄물을 받아 확률로 내놓는 산출물이 이것이다.
///
/// <b>여기 적는 확률은 "그 레시피에서 이 산출물이 가질 수 있는 가장 낮은 값"이다.</b>
/// 어느 기계가 이것을 얻을 수 있는지, 얼마나 잘 얻는지는 레시피가 아니라
/// <see cref="ExtractionTable"/> 이 정한다 — 등급마다 레시피를 복제하지 않기 위해서다.
/// 최종 확률 = <c>chance</c> × 표 배수 × <see cref="MachineBlock.chanceMultiplier"/>.
/// </summary>
[System.Serializable]
public class ChanceOutput
{
    [Tooltip("확률로 나오는 아이템.")]
    public Items item;

    [Tooltip("당첨됐을 때 나오는 개수.")]
    [Min(1)] public int count = 1;

    [Tooltip("기본(최저) 확률 0~1. 기계별 차이는 ExtractionTable 의 배수가 낸다.")]
    [Range(0f, 1f)] public float chance;
}
