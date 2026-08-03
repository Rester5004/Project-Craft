using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class ItemDictionary : Singleton<ItemDictionary>
{
    private Dictionary<string, Items> itemDictionary = new Dictionary<string, Items>();
    private Dictionary<string, BlockBase> blockDictionary = new Dictionary<string, BlockBase>();
    // 한글 표시 이름 → 아이템. /give 나 레시피 임포트에서 한글로 찾을 때 쓴다.
    private Dictionary<string, Items> displayNameDictionary = new Dictionary<string, Items>();
    // 아이템 → 그 아이템을 놓으면 생기는 지형 블록. dropItem 의 역방향이라 캔 것을 그대로 되놓을 수 있다.
    private Dictionary<Items, MainBlock> terrainByItem = new Dictionary<Items, MainBlock>();
    // 아이템 → 그 아이템을 놓으면 깔리는 파이프. 위와 같은 역인덱스 발상이다.
    private Dictionary<Items, PipeBlock> pipeByItem = new Dictionary<Items, PipeBlock>();

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

            RegisterTerrainPlacement(block);
            RegisterPipePlacement(block);
        }
    }

    /// <summary>
    /// 캔 아이템으로 같은 지형을 되놓을 수 있도록 dropItem 의 역방향 색인을 만든다.
    /// dropItem 을 그대로 키로 쓰기 때문에 "캐면 나오는 것"과 "놓으면 되는 것"이 어긋날 일이 없다.
    /// </summary>
    private void RegisterTerrainPlacement(BlockBase block)
    {
        MainBlock terrain = block as MainBlock;
        if (terrain == null || terrain.dropItem == null) return;

        if (terrainByItem.TryGetValue(terrain.dropItem, out MainBlock existing))
        {
            Debug.LogWarning($"아이템 '{terrain.dropItem.itemName}' 를 떨구는 지형 블록이 둘입니다"
                + $"({existing.blockName}, {terrain.blockName}). 배치에는 먼저 등록된 쪽을 씁니다.");
            return;
        }

        terrainByItem.Add(terrain.dropItem, terrain);
    }

    /// <summary>이 아이템을 놓으면 생기는 지형 블록. 지형이 아니면 null(기계 배치로 넘어간다).</summary>
    public MainBlock GetTerrainBlockFor(Items item)
    {
        if (item != null && terrainByItem.TryGetValue(item, out MainBlock block))
            return block;
        return null;
    }

    /// <summary>파이프도 지형과 같은 방식으로 dropItem 역인덱스를 만든다.</summary>
    private void RegisterPipePlacement(BlockBase block)
    {
        PipeBlock pipe = block as PipeBlock;
        if (pipe == null || pipe.dropItem == null) return;

        if (pipeByItem.TryGetValue(pipe.dropItem, out PipeBlock existing))
        {
            Debug.LogWarning($"아이템 '{pipe.dropItem.itemName}' 를 떨구는 파이프 블록이 둘입니다"
                + $"({existing.blockName}, {pipe.blockName}). 배치에는 먼저 등록된 쪽을 씁니다.");
            return;
        }

        pipeByItem.Add(pipe.dropItem, pipe);
    }

    /// <summary>이 아이템을 놓으면 깔리는 파이프. 파이프가 아니면 null.</summary>
    public PipeBlock GetPipeBlockFor(Items item)
    {
        if (item != null && pipeByItem.TryGetValue(item, out PipeBlock pipe))
            return pipe;
        return null;
    }

    /// <summary>blockId 로 파이프 정보를 조회한다. 없거나 파이프가 아니면 null.</summary>
    public PipeBlock GetPipeInfo(string blockId) => GetBlock(blockId) as PipeBlock;
    /// <summary>itemName 으로 Items 를 조회한다(placeable 인벤토리 복원 등에 사용).</summary>
    public Items GetItem(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return null;
        if (itemDictionary.TryGetValue(itemName, out Items item)) return item;

        // 통합돼 사라진 이름일 수 있다. 옛 세이브·인벤토리가 그 이름을 들고 있어도
        // 이 폴백 덕분에 정본으로 읽힌다 — 없으면 그 칸이 조용히 비어 버린다.
        string canonical = ItemAliases.Resolve(itemName);
        if (canonical != itemName && itemDictionary.TryGetValue(canonical, out Items merged)) return merged;

        return null;
    }
    /// <summary>등록된 모든 아이템 ID(명령어 자동완성·오타 안내용).</summary>
    public IEnumerable<string> ItemNames => itemDictionary.Keys;

    /// <summary>등록된 아이템 전부. 목록을 통째로 보여 주는 화면(아이템 브라우저)이 쓴다.</summary>
    public IEnumerable<Items> AllItems => itemDictionary.Values;

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

    /// <summary>
    /// blockId(=blockName) 로 블록을 조회한다(종류 무관). 없으면 null.
    ///
    /// 배치물은 <c>blockId == itemName == blockName</c> 규약이라 아이템 이름이 바뀌면 블록 이름도 바뀐다.
    /// 그래서 <b>아이템과 같은 별칭 표</b>를 폴백으로 쓴다 — 없으면 세이브에 이미 놓인 파이프·기계가
    /// 이름이 바뀐 순간 통째로 사라진다.
    /// </summary>
    public BlockBase GetBlock(string blockId)
    {
        if (string.IsNullOrEmpty(blockId)) return null;
        if (blockDictionary.TryGetValue(blockId, out BlockBase block)) return block;

        string canonical = ItemAliases.Resolve(blockId);
        if (canonical != blockId && blockDictionary.TryGetValue(canonical, out BlockBase renamed)) return renamed;

        return null;
    }

    /// <summary>blockId(=blockName) 로 기계 정보(MachineBlock)를 조회한다. 없거나 기계가 아니면 null.</summary>
    public MachineBlock GetMachineInfo(string blockId) => GetBlock(blockId) as MachineBlock;
    // 아래 둘은 예전에 무조건 캐스팅을 했다. 파이프처럼 MainBlock/MachineBlock 이 아닌 블록이 생기면
    // InvalidCastException 이 나므로 타입 검사로 바꾼다.
    public TileBase GetTileFromBlockDictionary(string name)
    {
        if (!blockDictionary.TryGetValue(name, out BlockBase block))
        {
            Debug.Log(name + "is not exists in dictionary.");
            return null;
        }

        if (block is MainBlock main) return main.assetPath;

        Debug.LogWarning($"[ItemDictionary] '{name}' 은 지형 블록이 아니라 타일이 없습니다({block.GetType().Name}).");
        return null;
    }
    public GameObject GetGameObjectFromBlockDictionary(string name)
    {
        if (!blockDictionary.TryGetValue(name, out BlockBase block))
        {
            Debug.Log(name + "is not exists in dictionary.");
            return null;
        }

        if (block is MachineBlock machine) return machine.machinePrefab;

        Debug.LogWarning($"[ItemDictionary] '{name}' 은 기계가 아니라 프리팹이 없습니다({block.GetType().Name}).");
        return null;
    }
}
