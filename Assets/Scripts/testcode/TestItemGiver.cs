using UnityEngine;
using System.Collections.Generic;

public class TestItemGiver : MonoBehaviour
{
    private Inventory inventory;   
    public List<Items> itemToGive;   
    public int amount = 10;

    void Start()
    {
        inventory = Inventory.Instance;
        foreach(Items i in itemToGive)
            inventory.AddItem(i, amount);
    }
}