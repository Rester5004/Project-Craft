using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 데이터 타일맵(Blocks/Floor)을 보고 실제로 보이는 벽·바닥 텍스처를 그린다.
///
/// 어떤 그림을 쓸지는 <b>블록이 정한다</b> — 좌표의 타일 ID 로 <see cref="MainBlock"/> 을 찾아
/// 벽이면 <see cref="MainBlock.wallAtlas"/>, 바닥이면 <see cref="MainBlock.floorSprite"/> 를 쓴다.
/// 덕분에 스테이지마다 다른 타일셋을 쓰면서도 오토타일 조견표(<see cref="TileAtlasManager"/>)는 하나로 공유한다.
/// </summary>
public class TilemapTextureLoader : Singleton<TilemapTextureLoader>
{
    protected override bool PersistAcrossScenes => false;

    [Header("아틀라스 설정")]
    [Tooltip("커서 윤곽선 시트. 벽 시트들과 격자가 같아 스테이지 구분 없이 공용으로 쓴다.")]
    [SerializeField] private TileAtlas outlineAtlas;
    public Tile floorOutLine;
    [Range(0f, 1f)]
    public float gemChance = 0.3f; // 보석이 등장할 확률
    public Vector2Int frontAtlasBase = new Vector2Int(8, 0);

    [Header("타일맵")]
    [SerializeField] Tilemap wallBottomTilemap; // 벽 "앞면" 전용 - 항상 플레이어보다 뒤에 고정 정렬
    [SerializeField] Tilemap wallTopTilemap; // 벽 "윗면" 전용 - 플레이어와 같은 Order in Layer로 Y-sort
    [SerializeField] Tilemap floorTextureTilemap; // 타일맵 컴포넌트 연결
    [SerializeField] Tilemap floorOverlayTilemap; // 바닥 '위에' 겹치는 것(입구·웅덩이). sortingOrder 115
    [SerializeField] Tilemap blocksTilemap;
    [SerializeField] Tilemap floorTilemap;
    [SerializeField] Tilemap outlineTilemap;

    // 아틀라스별로 스프라이트 격자표와 런타임 Tile 캐시를 따로 들고 있는다.
    // (같은 스프라이트를 여러 칸에 찍을 때 Tile을 매번 새로 생성하지 않기 위함)
    private readonly Dictionary<TileAtlas, Sprite[,]> spriteTables = new();
    private readonly Dictionary<TileAtlas, Tile[,]> tileCaches = new();
    private readonly Dictionary<Sprite, Tile> floorTileCache = new();

    // 같은 블록에 대해 경고를 매 칸마다 쏟지 않기 위한 기록
    private readonly HashSet<string> warnedBlocks = new();

    // 현재 outline이 표시되어 있는 위치 (ShowOutline 재호출 시 이전 outline을 지우기 위해 추적)
    private Vector2Int? currentOutlinePos;

    private bool isFloorOutLine;

    protected override void Awake()
    {
        base.Awake();
        ConfigureYSorting();
        // 커서 윤곽선이 조명을 받지 않게 하는 Unlit 머티리얼은 <b>GameRig 프리팹의 OutLine 타일맵에
        // 직접 지정</b>돼 있다 — 코드로 넣으면 이 Awake 가 다른 초기화보다 먼저 도는지에 기대게 된다.
    }

    /// <summary>
    /// 플레이어가 벽 뒤로 가면 가려지도록, 벽 윗면 타일맵(wallTopTilemap)만 개별 타일 단위로 그리게 하고
    /// 카메라가 Y좌표 기준으로 그리기 순서를 정하게 설정합니다.
    /// (플레이어 SpriteRenderer의 Order in Layer가 wallTopTilemap과 같아야 Y-sort가 적용됨.
    ///  wallTextureTilemap(앞면)은 플레이어보다 낮은 고정 Order in Layer를 쓰면 되므로 Y-sort 불필요.)
    /// </summary>
    private void ConfigureYSorting()
    {
        TilemapRenderer wallTopRenderer = wallTopTilemap.GetComponent<TilemapRenderer>();
        if (wallTopRenderer != null)
        {
            wallTopRenderer.mode = TilemapRenderer.Mode.Individual;
        }

        if (Camera.main != null)
        {
            Camera.main.transparencySortMode = TransparencySortMode.CustomAxis;
            Camera.main.transparencySortAxis = new Vector3(0f, 1f, 0f);
        }
    }

    // ── 블록 조회 ───────────────────────────────────────────────────────

    /// <summary>해당 셀의 지형 블록. 청크가 없거나 등록되지 않은 블록이면 null.</summary>
    private MainBlock BlockAt(Vector2Int pos)
    {
        if (WorldMap.Instance == null || ItemDictionary.Instance == null) return null;

        string tileId = WorldMap.Instance.GetTileId(pos);
        if (string.IsNullOrEmpty(tileId)) return null;

        MainBlock block = ItemDictionary.Instance.GetBlock(tileId) as MainBlock;
        if (block == null) WarnOnce(tileId, "딕셔너리에 MainBlock 으로 등록되어 있지 않습니다");
        return block;
    }

    private void WarnOnce(string blockId, string reason)
    {
        if (!warnedBlocks.Add(blockId)) return;
        Debug.LogWarning($"[TilemapTextureLoader] 블록 '{blockId}' 의 텍스처를 그릴 수 없습니다 — {reason}.", this);
    }

    // ── 런타임 Tile 생성 ────────────────────────────────────────────────

    /// <summary>
    /// 아틀라스 좌표(Vector2Int)를 던지면 유니티 Tile 객체를 동적으로 생성해 반환합니다.
    /// 같은 (아틀라스, 좌표) 는 항상 같은 스프라이트라 한 번 만든 Tile 을 계속 재사용합니다.
    /// </summary>
    public Tile CreateRuntimeTile(TileAtlas atlas, Vector2Int atlasCoord)
    {
        if (atlas == null || atlasCoord.x < 0 || atlasCoord.y < 0) return null;

        if (!tileCaches.TryGetValue(atlas, out Tile[,] cache))
        {
            spriteTables[atlas] = atlas.BuildTable();
            cache = new Tile[Mathf.Max(1, atlas.gridSize.x), Mathf.Max(1, atlas.gridSize.y)];
            tileCaches[atlas] = cache;
        }

        if (atlasCoord.x >= cache.GetLength(0) || atlasCoord.y >= cache.GetLength(1)) return null;

        Tile cachedTile = cache[atlasCoord.x, atlasCoord.y];
        if (cachedTile != null) return cachedTile;

        Sprite targetSprite = spriteTables[atlas][atlasCoord.x, atlasCoord.y];
        if (targetSprite == null) return null;

        Tile newTile = ScriptableObject.CreateInstance<Tile>();
        newTile.sprite = targetSprite;
        cache[atlasCoord.x, atlasCoord.y] = newTile;
        return newTile;
    }

    // ── 바닥 ────────────────────────────────────────────────────────────

    public void LoadFloorTexture(Vector2Int pos)
    {
        MainBlock block = BlockAt(pos);
        if (block == null) return;

        if (block.floorSprite == null)
        {
            WarnOnce(block.blockName, "floorSprite 가 비어 있습니다");
            return;
        }

        if (!floorTileCache.TryGetValue(block.floorSprite, out Tile floorTile))
        {
            floorTile = ScriptableObject.CreateInstance<Tile>();
            floorTile.sprite = block.floorSprite;
            floorTileCache[block.floorSprite] = floorTile;
        }

        floorTextureTilemap.SetTile((Vector3Int)pos, floorTile);
    }

    /// <summary>
    /// 바닥 <b>위에</b> 겹치는 오버레이(지하 입구 · 석유/물 웅덩이)를 그린다.
    ///
    /// 바닥 텍스처와 같은 기계(<see cref="MainBlock.floorSprite"/> + 스프라이트별 Tile 캐시)를 그대로 쓴다 —
    /// 다른 것은 <b>어느 타일맵에 찍느냐</b>뿐이다. 그림의 모서리가 뚫려 있어 아래 바닥이 비쳐 보인다.
    /// 오버레이가 없는 칸은 <b>반드시 지운다</b> — 안 그러면 입구를 써서 없앤 자리에 그림이 남는다.
    /// </summary>
    public void LoadFloorOverlay(Vector2Int pos)
    {
        if (floorOverlayTilemap == null) return;

        WorldMap map = WorldMap.InstanceIfAlive;
        string overlayId = map != null ? map.GetOverlayAt(pos) : null;
        if (string.IsNullOrEmpty(overlayId))
        {
            floorOverlayTilemap.SetTile((Vector3Int)pos, null);
            return;
        }

        MainBlock block = ItemDictionary.Instance != null
            ? ItemDictionary.Instance.GetBlock(overlayId) as MainBlock : null;
        if (block == null || block.floorSprite == null)
        {
            WarnOnce(overlayId, "오버레이 블록이 없거나 floorSprite 가 비어 있습니다");
            floorOverlayTilemap.SetTile((Vector3Int)pos, null);
            return;
        }

        if (!floorTileCache.TryGetValue(block.floorSprite, out Tile overlayTile))
        {
            overlayTile = ScriptableObject.CreateInstance<Tile>();
            overlayTile.sprite = block.floorSprite;
            floorTileCache[block.floorSprite] = overlayTile;
        }

        floorOverlayTilemap.SetTile((Vector3Int)pos, overlayTile);
    }

    // ── 벽 ──────────────────────────────────────────────────────────────

    public void LoadWallTexture(Vector2Int pos)
    {
        Vector3Int currentGridPos = (Vector3Int)pos;

        // 1. 데이터 상에 블록이 없으면 패스
        if (blocksTilemap.GetTile(currentGridPos) == null) return;

        MainBlock block = BlockAt(pos);
        if (block == null) return;
        if (block.wallAtlas == null)
        {
            WarnOnce(block.blockName, "wallAtlas 가 비어 있습니다");
            return;
        }

        var (topAtlas, frontAtlas) = CalculateWallAtlasCoords(pos);

        // =================================================================
        // [단계 1] 한 칸 위(Y + 1) 좌표에 "벽 윗면(Top Wall)" 그리기
        // =================================================================
        Vector3Int topGridPos = currentGridPos + Vector3Int.up;
        Tile topWallTile = CreateRuntimeTile(block.wallAtlas, topAtlas);

        if (topWallTile != null)
        {
            wallTopTilemap.SetTile(topGridPos, topWallTile);
        }

        // =================================================================
        // [단계 2] 현재 제자리(pos) 좌표에 "앞면 벽(Front Wall)" 그리기
        // =================================================================
        Tile frontWallTile = CreateRuntimeTile(block.wallAtlas, frontAtlas);

        if (frontWallTile != null)
        {
            wallBottomTilemap.SetTile(currentGridPos, frontWallTile);
        }
    }

    /// <summary>
    /// 특정 좌표의 블록이 그려야 할 "윗면(top)"/"앞면(front)" 아틀라스 좌표를 계산합니다.
    /// LoadWallTexture와 ShowOutline이 항상 동일한 좌표를 사용하도록 공통 로직으로 분리했습니다.
    /// </summary>
    private (Vector2Int topAtlas, Vector2Int frontAtlas) CalculateWallAtlasCoords(Vector2Int pos)
    {
        // 현재 pos 기준 8방향 비트마스크 및 조견표(윗면) 아틀라스 좌표 추출
        int bitmask = CalculateBitmask(pos);
        Vector2Int topAtlas = TileAtlasManager.Instance.GetAtlasCoordinate((byte)bitmask);

        bool e = (bitmask & 4) != 0;
        bool w = (bitmask & 64) != 0;

        // 동/서 조건에 따른 정면 벽 모양 결정 (yOffset)
        int yOffset = 0; // 기본 일자형 앞면 벽
        if (!w && !e) yOffset = 3;      // 고립 벽
        else if (w && e) yOffset = 2;   // 직선 앞면 벽
        else if (!w && e) yOffset = 1;  // 왼쪽 끝 칸도 직선 모양으로 유지
        else if (w && !e) yOffset = 0;  // 오른쪽 끝 칸도 직선 모양으로 유지

        // X축 랜덤 패턴(보석 벽면 등) 연산
        int xOffset = (Mathf.Abs(pos.x) % 2 == 0) ? 1 : 0;
        float pseudoRandom = (Mathf.Sin(pos.x * 12.9898f + pos.y * 78.233f) * 43758.5453f) % 1f;
        if (Mathf.Abs(pseudoRandom) < gemChance)
        {
            xOffset = (Mathf.Abs(pos.x) % 2 == 0) ? 3 : 2;
        }

        Vector2Int frontAtlas = new Vector2Int(frontAtlasBase.x + xOffset, frontAtlasBase.y + yOffset);
        return (topAtlas, frontAtlas);
    }

    /// <summary>
    /// 특정 좌표를 기준으로 주변 8방향의 블록 존재 여부를 비트마스크로 환산합니다.
    /// 벽의 <b>종류는 구분하지 않습니다</b> — 스테이지 경계에서도 실루엣이 끊기지 않아야 하기 때문입니다.
    /// </summary>
    private int CalculateBitmask(Vector2Int centerPos)
    {
        int bitmask = 0;
        int bit = 1;

        // TileAtlasManager에 설정한 시계방향 8방향 순회 (N, NE, E, SE, S, SW, W, NW)
        foreach (Vector2Int dir in TileAtlasManager.All8Directions)
        {
            Vector2Int neighborPos = centerPos + dir;
            if (blocksTilemap.GetTile((Vector3Int)neighborPos) != null)
            {
                bitmask += bit;
            }
            bit *= 2;
        }

        return bitmask;
    }

    public void ClearTileTexture(Vector2Int pos)
    {
        floorTextureTilemap.SetTile((Vector3Int)pos, null);
        if (floorOverlayTilemap != null) floorOverlayTilemap.SetTile((Vector3Int)pos, null);
        wallBottomTilemap.SetTile((Vector3Int)pos, null);
        wallTopTilemap.SetTile((Vector3Int)pos, null);
    }

    // ── 커서 윤곽선 ─────────────────────────────────────────────────────

    /// <summary>
    /// pos 위치의 블록에 outline을 표시합니다. LoadWallTexture가 그리는 것과 동일한
    /// 아틀라스 좌표(같은 인덱스)를 사용해 outlineTilemap의 같은 두 칸(자신 + 한 칸 위)에 그립니다.
    /// </summary>
    public void ShowOutline(Vector2Int pos)
    {
        ClearOutline();

        if (blocksTilemap.GetTile((Vector3Int)pos) == null){
            if(floorTilemap.GetTile((Vector3Int)pos)!=null)
            {
                isFloorOutLine = true;
                outlineTilemap.SetTile((Vector3Int)pos, floorOutLine);
                currentOutlinePos = pos;
            }
            return;
        }
        var (topAtlas, frontAtlas) = CalculateWallAtlasCoords(pos);

        Tile topOutline = CreateRuntimeTile(outlineAtlas, topAtlas);
        Tile frontOutline = CreateRuntimeTile(outlineAtlas, frontAtlas);

        if (topOutline != null)
            outlineTilemap.SetTile((Vector3Int)(pos + Vector2Int.up), topOutline);
        if (frontOutline != null)
            outlineTilemap.SetTile((Vector3Int)pos, frontOutline);

        currentOutlinePos = pos;
    }

    /// <summary>해당 셀이 현재 표시 중인 윤곽선(outlineTilemap)에 속하는지 여부.</summary>
    public bool IsOutlined(Vector2Int cell)
        => outlineTilemap != null && outlineTilemap.GetTile((Vector3Int)cell) != null;

    public void ClearOutline()
    {
        if (currentOutlinePos == null) return;
        if(isFloorOutLine)
        {
            outlineTilemap.SetTile((Vector3Int)currentOutlinePos.Value, null);
            isFloorOutLine = false;
            currentOutlinePos = null;
            return;
        }
        Vector2Int pos = currentOutlinePos.Value;
        outlineTilemap.SetTile((Vector3Int)pos, null);
        outlineTilemap.SetTile((Vector3Int)(pos + Vector2Int.up), null);
        currentOutlinePos = null;
    }
}
