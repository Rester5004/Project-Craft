using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class ItemDictionary : Singleton<ItemDictionary>
{
    private Dictionary<string, Items> itemDictionary = new Dictionary<string, Items>();
    private Dictionary<string, BlockBase> blockDictionary = new Dictionary<string, BlockBase>();
    // 한글 표시 이름 → 아이템. /give 나 레시피 임포트에서 한글로 찾을 때 쓴다.
    private Dictionary<string, Items> displayNameDictionary = new Dictionary<string, Items>();

    /// <summary>
    /// 한글 이름을 키로 비교할 때는 NFC 로 통일한다.
    /// '철 조각' 은 완성형(NFC)과 조합형(NFD)으로 표현될 수 있는데 보기엔 같아도 문자열 비교가 실패한다.
    /// </summary>
    public static string NormalizeName(string name)
        => string.IsNullOrEmpty(name) ? name : name.Normalize(System.Text.NormalizationForm.FormC);

    [Header("Items")]
    [SerializeField] private List<Items> itemsList;

    [Header("Blocks")]
    [SerializeField] private List<BlockBase> blocksList;
    protected override void Awake()
    {
        base.Awake();
        foreach (Items item in itemsList)
        {
            // 에셋이 삭제되면 리스트에 빈 칸이 남는다. 여기서 터지면 뒤쪽 아이템이 통째로 등록되지 않는다.
            if (item == null)
            {
                Debug.LogWarning("[ItemDictionary] itemsList 에 빈 칸이 있습니다(삭제된 에셋). Register All Assets 로 정리하세요.", this);
                continue;
            }

            if (!itemDictionary.ContainsKey(item.itemName))
            {
                itemDictionary.Add(item.itemName, item);
            }
            else
            {
                Debug.LogWarning($"Duplicate item name detected: {item.itemName}. Please ensure unique names for all items.");
            }

            // 표시 이름이 ID 와 다를 때만 보조 색인에 넣는다(한글 조회용).
            string display = NormalizeName(item.displayName);
            if (!string.IsNullOrEmpty(display) && display != item.itemName && !displayNameDictionary.ContainsKey(display))
                displayNameDictionary.Add(display, item);
        }
        foreach (BlockBase block in blocksList)
        {
            if (block == null)
            {
                Debug.LogWarning("[ItemDictionary] blocksList 에 빈 칸이 있습니다(삭제된 에셋). Register All Assets 로 정리하세요.", this);
                continue;
            }

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
    /// <summary>등록된 모든 아이템 ID(명령어 자동완성·오타 안내용).</summary>
    public IEnumerable<string> ItemNames => itemDictionary.Keys;

    /// <summary>등록된 모든 한글 표시 이름.</summary>
    public IEnumerable<string> DisplayNames => displayNameDictionary.Keys;

    /// <summary>한글 표시 이름으로 아이템을 조회한다.</summary>
    public Items GetItemByDisplayName(string displayName)
    {
        string key = NormalizeName(displayName);
        if (!string.IsNullOrEmpty(key) && displayNameDictionary.TryGetValue(key, out Items item))
            return item;
        return null;
    }

    /// <summary>ID → 한글 표시 이름 → 대소문자 무시 ID 순으로 찾는다(명령어 입력용).</summary>
    public Items FindItem(string itemName)
    {
        Items item = GetItem(itemName);
        if (item != null) return item;
        if (string.IsNullOrEmpty(itemName)) return null;

        item = GetItemByDisplayName(itemName);
        if (item != null) return item;

        foreach (KeyValuePair<string, Items> pair in itemDictionary)
            if (string.Equals(pair.Key, itemName, System.StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        return null;
    }

    /// <summary>blockId(=blockName) 로 블록을 조회한다(종류 무관). 없으면 null.</summary>
    public BlockBase GetBlock(string blockId)
    {
        if (!string.IsNullOrEmpty(blockId) && blockDictionary.TryGetValue(blockId, out BlockBase block))
            return block;
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
