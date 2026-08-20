using UnityEngine;

public class TrashCanInventory : Singleton<TrashCanInventory>,IItemContainer
{
    public int Capacity => 1;
    private ItemStack[] trash = new ItemStack[1];
    public System.Action OnChanged;
    public ItemStack GetStack(int index)
    {
        return trash[0];
    }
    protected override void Awake()
    {
        base.Awake();
        trash[0] = new ItemStack();
    }
    public void NotifyChanged(){
        trash[0].Clear();
        OnChanged?.Invoke();
    }
    public int SlotCapacity(int index, Items item)
    {
        if (index != 0)
        {
            throw new System.IndexOutOfRangeException("TrashCanInventory only has one slot at index 0.");
        }
        return RecipeSolver.MaxStackOf(item); // Return the max stack size of the item
    }

    /// <summary>쓰레기통은 무엇이든 받는다.</summary>
    public bool AcceptsItem(int index, Items item) => true;
    void Update()
    {

    }
}
