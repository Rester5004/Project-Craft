using UnityEngine;

[CreateAssetMenu(fileName = "Item", menuName = "Items/Items")]
public class Items : ScriptableObject
{
    public string itemName;
    public bool placeable;
    public Sprite Icon;
    public int maxStack;
}
