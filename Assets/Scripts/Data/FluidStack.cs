/// <summary>
/// 유체 한 묶음. <see cref="ItemStack"/> 과 정확히 같은 자리를 맡는다 —
/// <b>레시피의 입출력 항목이자 기계 탱크 한 칸</b>이다.
///
/// 둘을 다른 클래스로 나누면 "레시피용"과 "탱크용" 규칙이 갈라져 언젠가 한쪽만 고쳐진다.
/// 아이템 쪽이 <c>ItemStack</c> 하나로 버티고 있는 것과 같은 이유다.
///
/// 양은 단위 없는 정수다(1 양동이 = <see cref="FluidDefine.bucketAmount"/> = 1000 이 규약).
/// </summary>
[System.Serializable]
public class FluidStack
{
    public FluidDefine fluid;
    public int amount;

    /// <summary>비어 있는가. 종류만 남고 양이 0 인 것도 비어 있는 것으로 본다.</summary>
    public bool IsEmpty => fluid == null || amount <= 0;

    /// <summary>탱크를 비운다. 종류까지 지워야 다음에 다른 유체가 들어올 수 있다.</summary>
    public void Clear()
    {
        fluid = null;
        amount = 0;
    }
}
