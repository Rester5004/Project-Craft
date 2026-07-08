using UnityEngine;

public class TestItemGiver : MonoBehaviour
{
    private Inventory inventory;   
    public Items itemToGive;   
    public int amount = 10;

    void Start()
    {
        inventory = Inventory.Instance;
        inventory.AddItem(itemToGive, amount);
    }
}