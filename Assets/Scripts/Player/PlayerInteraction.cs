using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private MapGenerator mapGenerator;
    private Inventory inventory;
    private Vector2Int targetGlobalCell;
    private Vector2Int playerGlobalCell;
    private Vector2 mousePosition;
    private bool isPointerOverUI;



    [SerializeField] private LayerMask occupiedLayers = (1 << 6) | (1 << 7);
    [SerializeField, Range(0.1f, 1f)] private float occupancyCheckSize = 0.8f;
    [SerializeField] private int placementFlag;
    [Header("Mining")]
    [SerializeField, Min(0.1f)] private float miningHoldDuration = 0.75f;

    private Vector2Int miningTarget;
    private float miningProgress;
    private bool isMining;

    private void OnEnable()
    {
        if (InputActionManager.Instance != null){
            InputActionManager.Instance.OnHitPerformed += HandleHitPerformed;
            InputActionManager.Instance.OnUsePerformed += HandleUsePerformed;
            InputActionManager.Instance.OnInteractPerformed += HandleInteractPerformed;
        }
    }
    void Start()
    {
        inventory = Inventory.Instance;
        PrototypeMapTransitions.Initialize();
    }

    private void OnDisable()
    {
        if (InputActionManager.Instance != null){
            InputActionManager.Instance.OnHitPerformed -= HandleHitPerformed;
            InputActionManager.Instance.OnUsePerformed -= HandleUsePerformed;
            InputActionManager.Instance.OnInteractPerformed -= HandleInteractPerformed;
        }
    }

    private void HandleHitPerformed()
    {
        if (UIManager.Instance != null && UIManager.Instance.isAnyUIOpen && UIManager.Instance.OpenUICount > 0)
            return;

        if (isPointerOverUI)
            return;
        // 채굴은 Update에서 버튼을 누르고 있는 동안 처리한다.
    }
    private void HandleInteractPerformed()
    {
        PrototypeMapTransitions.TryUseNearest(transform);
    }
    private void HandleUsePerformed()
    {
        ItemStack selectedItemStack = inventory.GetSelectedItem();
        if(selectedItemStack == null || selectedItemStack.item == null || !selectedItemStack.item.placeable)
            return;
        Vector2 mouseWorldPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2Int playerCell = Vector2Int.RoundToInt(transform.position);
        Vector2Int targetCell = Vector2Int.RoundToInt(mouseWorldPosition);
        Vector2Int difference = targetCell - playerCell;

        if (Mathf.Abs(difference.x) + Mathf.Abs(difference.y) != 1) return;
        /*
        Collider2D occupiedCollider = Physics2D.OverlapBox(
            targetCell,
            Vector2.one * occupancyCheckSize,
            0f,
            occupiedLayers);

        if (occupiedCollider != null) return;
        */
        
        placementFlag = 1;
        Debug.Log($"[MachinePlacement] 설치 요청 성공. placementFlag = {placementFlag}, 위치: {targetCell}");
    }
    private bool GetIsCardinalAdjacent(Vector2Int targetGlobalCell, Vector2Int playerGlobalCell)
    {
        SetGlobalCellPositions();
        Vector2Int delta = targetGlobalCell - playerGlobalCell;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1;
    }
    private void SetGlobalCellPositions()
    {
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue(); 
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
        mousePosition = mousePos;
        Vector2Int chunkToMining = Chunk.GetChunkId(mousePos);
        Vector2Int toMining = Chunk.GetLocalCellPositionInChunk(mousePos);

        Vector2Int playerChunk = Chunk.GetChunkId(transform.position);
        Vector2Int playerCell = Chunk.GetLocalCellPositionInChunk(transform.position);

        targetGlobalCell = chunkToMining * WorldMap.ChunkSize + toMining;
        playerGlobalCell = playerChunk * WorldMap.ChunkSize + playerCell;
    }
    private void Update()
    {
        SetGlobalCellPositions();
        isPointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        UpdateMining();
        if(GetIsCardinalAdjacent(targetGlobalCell, playerGlobalCell))
        {
            TilemapTextureLoader.Instance.ShowOutline(targetGlobalCell);
        } 
    }

    private void UpdateMining()
    {
        bool canMine = !isPointerOverUI
            && (UIManager.Instance == null || !UIManager.Instance.isAnyUIOpen || UIManager.Instance.OpenUICount == 0)
            && Mouse.current != null
            && Mouse.current.leftButton.isPressed
            && GetIsCardinalAdjacent(targetGlobalCell, playerGlobalCell)
            && WorldMap.Instance != null
            && WorldMap.Instance.IsMineable(Chunk.GetChunkId(mousePosition), Chunk.GetLocalCellPositionInChunk(mousePosition));

        if (!canMine)
        {
            CancelMining();
            return;
        }

        if (!isMining || miningTarget != targetGlobalCell)
        {
            miningTarget = targetGlobalCell;
            miningProgress = 0f;
            isMining = true;
        }

        miningProgress += Time.deltaTime;
        if (miningProgress < miningHoldDuration)
            return;

        if (WorldMap.Instance.Mining(Chunk.GetChunkId(mousePosition), Chunk.GetLocalCellPositionInChunk(mousePosition)))
            mapGenerator.RefreshMinedTile(targetGlobalCell);
        CancelMining();
    }

    private void CancelMining()
    {
        isMining = false;
        miningProgress = 0f;
    }

    private void OnGUI()
    {
        if (!isMining || miningHoldDuration <= 0f)
            return;

        const float width = 170f;
        const float height = 14f;
        Rect background = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.72f, width, height);
        GUI.Box(background, GUIContent.none);
        Color previous = GUI.color;
        GUI.color = new Color(0.25f, 0.9f, 1f, 1f);
        GUI.DrawTexture(new Rect(background.x + 2f, background.y + 2f, (width - 4f) * Mathf.Clamp01(miningProgress / miningHoldDuration), height - 4f), Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
