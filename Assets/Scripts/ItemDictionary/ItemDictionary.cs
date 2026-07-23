using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class ItemDictionary : Singleton<ItemDictionary>
{
    private Dictionary<string, Items> itemDictionary = new Dictionary<string, Items>();
    private Dictionary<string, BlockBase> blockDictionary = new Dictionary<string, BlockBase>();

    [Header("Items")]
    [SerializeField] private List<Items> itemsList;

    [Header("Blocks")]
    [SerializeField] private List<BlockBase> blocksList;
    protected override void Awake()
    {
        base.Awake();
        foreach (Items item in itemsList)
        {
            if (!itemDictionary.ContainsKey(item.itemName))
            {
                itemDictionary.Add(item.itemName, item);
            }
            else
            {
                Debug.LogWarning($"Duplicate item name detected: {item.itemName}. Please ensure unique names for all items.");
            }
        }
        foreach (BlockBase block in blocksList)
        {
            if (!blockDictionary.ContainsKey(block.blockName))
            {
                blockDictionary.Add(block.blockName, block);
            }
            else
            {
                Debug.LogWarning($"Duplicate block name detected: {block.blockName}. Please ensure unique names for all blocks.");
            }
        }
    }
    public TileBase GetTileFromBlockDictionary(string name)
    {
        if (blockDictionary.ContainsKey(name))
        {
            MainBlock tmp = (MainBlock)blockDictionary[name];
            return tmp.assetPath;
        }
        else
        {
            Debug.Log(name+"is not exists in dictionary.");
        }
        return null;
    }
    public GameObject GetGameObjectFromBlockDictionary(string name)
    {
        if (blockDictionary.ContainsKey(name))
        {
            MachineBlock tmp = (MachineBlock)blockDictionary[name];
            return tmp.machinePrefab;
        }
        else
        {
            Debug.Log(name+"is not exists in dictionary.");
        }
        return null;
    }
}
