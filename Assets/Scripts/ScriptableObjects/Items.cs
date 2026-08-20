using UnityEngine;

[CreateAssetMenu(fileName = "Item", menuName = "Items/Items")]
public class Items : ScriptableObject
{
    [Tooltip("내부 ID. 세이브 파일의 키이므로 바꾸면 기존 세이브가 깨진다. 영어로 유지할 것.")]
    public string itemName;

    [Tooltip("화면에 표시할 이름(한글). 비우면 itemName 을 그대로 쓴다.")]
    public string displayName;

    public bool placeable;
    public Sprite Icon;
    public int maxStack;

    [Tooltip("이 그릇에 유체가 담겼을 때 Icon 위에 겹칠 그림. '빈 그릇' 아이템(양동이·유리 용기)에만 채운다. " +
             "색은 여기가 아니라 FluidColors 가 정하므로 반드시 흰색 마스크로 그릴 것.")]
    public Sprite fluidOverlay;

    [Tooltip("연료로 태웠을 때 나오는 에너지. 0 이면 연료가 아니다. (갈탄 200 / 석탄 400 / 수소 1000)")]
    [Min(0f)] public float burnEnergy;

    [Tooltip("만들어질 때 붙는 내구도. 0 이면 평범한 소모품이다(공동 탐색기만 값을 갖는다). "
           + "개체 데이터가 붙으므로 maxStack 은 1 이어야 한다 — OnValidate 가 못박는다.")]
    [Min(0)] public int initialDurability;

    /// <summary>쓸 때마다 닳는 물건인가(개체 데이터 <see cref="ToolInstance"/> 를 달고 태어난다).</summary>
    public bool HasDurability => initialDurability > 0;

    /// <summary>화로·발전기의 연료로 쓸 수 있는가.</summary>
    public bool IsFuel => burnEnergy > 0f;

    /// <summary>UI 표시용 이름. displayName 이 비어 있으면 ID 로 폴백한다.</summary>
    public string DisplayName => string.IsNullOrEmpty(displayName) ? itemName : displayName;

    /// <summary>
    /// ⚠ <b>내구도가 있는 아이템은 한 칸에 하나뿐이어야 한다.</b> 개체 데이터는 스택당 하나라,
    /// 여러 개를 겹쳐 두면 그 하나가 닳아 없어질 때 <c>stack.Clear()</c> 로 전부가 함께 사라진다
    /// (<see cref="StorageCellItem"/> 이 같은 이유로 1 을 못박았다).
    /// </summary>
    private void OnValidate()
    {
        if (HasDurability && maxStack != 1) maxStack = 1;
    }
}
