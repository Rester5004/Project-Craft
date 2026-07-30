using UnityEngine;
[System.Serializable]
public class ItemStack
{
    public Items item;
    public int count;

    // 개체별 데이터(커스텀 도구의 재질·내구도 등). 런타임 전용이라 에셋 직렬화에는 나타나지 않는다
    // — Recipe.inputs/outputs 도 List<ItemStack> 이므로 필드를 직렬화하면 레시피 에셋 전체가 재기록된다.
    // 세이브에는 PlayerSave / WorldMap 이 ItemInstanceSerializer 로 직접 싣는다.
    [System.NonSerialized] public ItemInstance instance;

    /// <summary>개체 데이터가 없는 평범한 스택인가(레시피 재료로 쓸 수 있는가).</summary>
    public bool IsPlain => instance == null;

    /// <summary>내용이 같아 한 칸으로 합쳐도 되는가.</summary>
    public bool CanStackWith(ItemStack other)
        => other != null && item == other.item && ItemInstance.Same(instance, other.instance);

    /// <summary>슬롯을 비운다(개체 데이터까지 확실히 지운다).</summary>
    public void Clear()
    {
        item = null;
        count = 0;
        instance = null;
    }
}

public class Gas
{
    public GasDefine gas;
    public float amount;
}