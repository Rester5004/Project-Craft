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
    /// <summary>itemName 으로 Items 를 조회한다(placeable 인벤토리 복원 등에 사용).</summary>
    public Items GetItem(string itemName)
    {
        if (!string.IsNullOrEmpty(itemName) && itemDictionary.TryGetValue(itemName, out Items item))
            return item;
        return null;
    }
    /// <summary>blockId(=blockName) 로 기계 정보(MachineBlock)를 조회한다. 없거나 기계가 아니면 null.</summary>
    public MachineBlock GetMachineInfo(string blockId)
    {
        if (!string.IsNullOrEmpty(blockId) && blockDictionary.TryGetValue(blockId, out BlockBase block))
            return block as MachineBlock;
        return null;
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
