using UnityEngine;

[CreateAssetMenu(fileName = "Item", menuName = "Items/Items")]
public class Items : ScriptableObject
{
    public string itemName;
    public Sprite itemIcon;
    public int itemID;
    public int maxStack;

}
[CreateAssetMenu(fileName = "machine", menuName = "Items/machines")]
public class machine : Items
{
    public int machineType;
    public int machineLevel;
}
