using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapTextureLoader : Singleton<TilemapTextureLoader>
{
    [Header("아틀라스 설정")]
    public Texture2D tilemapTexture;
    public Tile floorOutLine;
    public Texture2D outlineTexture; // 슬라이스한 타일맵 PNG 파일 등록
    public Vector2Int atlasGridSize = new Vector2Int(16, 16); // 아틀라스의 전체 가로/세로 타일 개수 (예: 16x16칸짜리 이미지)
    public Vector2Int frontAtlasBase = new Vector2Int(8, 0); // X=8, Y=4
    [Range(0f, 1f)]
    public float gemChance = 0.3f; // 보석이 등장할 확률 (8%) 
    public Sprite testFloorTexture;

    [Header("타일맵")]
    [SerializeField] Tilemap wallBottomTilemap; // 벽 "앞면" 전용 - 항상 플레이어보다 뒤에 고정 정렬
    [SerializeField] Tilemap wallTopTilemap; // 벽 "윗면" 전용 - 플레이어와 같은 Order in Layer로 Y-sort
    [SerializeField] Tilemap floorTextureTilemap; // 타일맵 컴포넌트 연결
    [SerializeField] Tilemap blocksTilemap;
    [SerializeField] Tilemap floorTilemap;
    [SerializeField] Tilemap outlineTilemap;

    // 좌표로 스프라이트를 초고속 탐색하기 위한 2차원 배열
    private Sprite[,] spriteAtlasTable;
    private Sprite[,] outlineSpriteAtlasTable;

    // 아틀라스 좌표별로 한 번 만든 Tile 인스턴스를 재사용하기 위한 캐시
    // (같은 스프라이트를 여러 칸에 찍을 때 Tile을 매번 새로 생성하지 않기 위함)
    private Tile[,] tileCache;
    private Tile[,] outlineTileCache;
    private Tile cachedFloorTile;

    // 현재 outline이 표시되어 있는 위치 (ShowOutline 재호출 시 이전 outline을 지우기 위해 추적)
    private Vector2Int? currentOutlinePos;

    private bool isFloorOutLine;
    protected override void Awake()
    {
        base.Awake();
        CacheAtlasSprites();
        ConfigureYSorting();
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

    private void CacheAtlasSprites()
    {
        tileCache = new Tile[atlasGridSize.x, atlasGridSize.y];
        outlineTileCache = new Tile[atlasGridSize.x, atlasGridSize.y];

        spriteAtlasTable = LoadAtlasSprites("Tilesets/stage2_wall_tilemap");
        // wall_tilemap_outline은 stage2_wall_tilemap과 동일한 그리드로 슬라이스되어 있어
        // 같은 아틀라스 좌표(topAtlas/frontAtlas)를 그대로 outline 쪽에도 사용할 수 있다.
        outlineSpriteAtlasTable = LoadAtlasSprites("Tilesets/wall_tilemap_outline");
    }

    /// <summary>
    /// Resources 폴더 기준 경로의 슬라이스된 스프라이트들을 grid 좌표별 2차원 배열로 캐싱합니다.
    /// </summary>
    private Sprite[,] LoadAtlasSprites(string resourcePath)
    {
        Sprite[,] table = new Sprite[atlasGridSize.x, atlasGridSize.y];

        // LoadAll<Sprite>는 하위 에셋 중 'Sprite' 타입만 알아서 필터링해서 배열로 반환합니다.
        Sprite[] allSprites = Resources.LoadAll<Sprite>(resourcePath);

        if (allSprites == null || allSprites.Length == 0)
        {
            Debug.LogError($"Resources에서 스프라이트 아틀라스를 찾을 수 없습니다! ({resourcePath})");
            return table;
        }

        foreach (Sprite sprite in allSprites)
        {
            int gridX = Mathf.RoundToInt(sprite.rect.x / sprite.rect.width);
            int gridY = Mathf.RoundToInt(sprite.rect.y / sprite.rect.height);

            if (gridX < atlasGridSize.x && gridY < atlasGridSize.y)
            {
                table[gridX, gridY] = sprite;
            }
        }

        return table;
    }

    /// <summary>
    /// 앞서 계산한 아틀라스 좌표(Vector2Int)를 던지면 유니티 Tile 객체를 동적으로 생성해 반환합니다.
    /// </summary>
    public Tile CreateRuntimeTile(Vector2Int atlasCoord)
    {
        if (atlasCoord.x < 0 || atlasCoord.y < 0) return null;

        // 0. 이미 만들어둔 Tile이 있으면 그걸 그대로 재사용 (같은 좌표는 항상 같은 스프라이트라
        //    매번 새 Tile 오브젝트를 만들 필요가 없음)
        Tile cachedTile = tileCache[atlasCoord.x, atlasCoord.y];
        if (cachedTile != null) return cachedTile;

        // 1. 2차원 캐시 배열에서 해당 좌표의 스프라이트 쏙 빼오기
        Sprite targetSprite = spriteAtlasTable[atlasCoord.x, atlasCoord.y];

        if (targetSprite != null)
        {
            // 2. 유니티 타일맵에 찍을 수 있는 Scriptable Tile 인스턴스 생성 후 캐시에 저장
            Tile newTile = ScriptableObject.CreateInstance<Tile>();
            newTile.sprite = targetSprite;
            tileCache[atlasCoord.x, atlasCoord.y] = newTile;
            return newTile;
        }

        return null;
    }

    /// <summary>
    /// CreateRuntimeTile과 동일한 구조로, outline 전용 아틀라스/캐시를 사용하는 버전입니다.
    /// </summary>
    public Tile CreateRuntimeOutlineTile(Vector2Int atlasCoord)
    {
        if (atlasCoord.x < 0 || atlasCoord.y < 0) return null;

        Tile cachedTile = outlineTileCache[atlasCoord.x, atlasCoord.y];
        if (cachedTile != null) return cachedTile;

        Sprite targetSprite = outlineSpriteAtlasTable[atlasCoord.x, atlasCoord.y];

        if (targetSprite != null)
        {
            Tile newTile = ScriptableObject.CreateInstance<Tile>();
            newTile.sprite = targetSprite;
            outlineTileCache[atlasCoord.x, atlasCoord.y] = newTile;
            return newTile;
        }

        return null;
    }

    public void LoadFloorTexture(Vector2Int pos)
    {
        if (cachedFloorTile == null)
        {
            cachedFloorTile = ScriptableObject.CreateInstance<Tile>();
            cachedFloorTile.sprite = testFloorTexture;
        }

        floorTextureTilemap.SetTile((Vector3Int)pos, cachedFloorTile);
    }

    public void LoadWallTexture(Vector2Int pos)
    {
        Vector3Int currentGridPos = (Vector3Int)pos;

        // 1. 데이터 상에 블록이 없으면 패스
        if (blocksTilemap.GetTile(currentGridPos) == null) return;

        var (topAtlas, frontAtlas) = CalculateWallAtlasCoords(pos);

        // =================================================================
        // [단계 1] 한 칸 위(Y + 1) 좌표에 "벽 윗면(Top Wall)" 그리기
        // =================================================================
        Vector3Int topGridPos = currentGridPos + Vector3Int.up;
        Tile topWallTile = CreateRuntimeTile(topAtlas);

        if (topWallTile != null)
        {
            wallTopTilemap.SetTile(topGridPos, topWallTile);
        }

        // =================================================================
        // [단계 2] 현재 제자리(pos) 좌표에 "앞면 벽(Front Wall)" 그리기
        // =================================================================
        Tile frontWallTile = CreateRuntimeTile(frontAtlas);

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
        wallBottomTilemap.SetTile((Vector3Int)pos, null);
        wallTopTilemap.SetTile((Vector3Int)pos, null);
    }

    /// <summary>
    /// pos 위치의 블록에 outline을 표시합니다. LoadWallTexture가 그리는 것과 동일한
    /// 아틀라스 좌표(같은 인덱스)를 사용해 outlineTilemap의 같은 두 칸(자신 + 한 칸 위)에 그립니다.
    /// 커서 위치 계산 및 호출 타이밍은 별도로 구현합니다.
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

        Tile topOutline = CreateRuntimeOutlineTile(topAtlas);
        Tile frontOutline = CreateRuntimeOutlineTile(frontAtlas);

        if (topOutline != null)
            outlineTilemap.SetTile((Vector3Int)(pos + Vector2Int.up), topOutline);
        if (frontOutline != null)
            outlineTilemap.SetTile((Vector3Int)pos, frontOutline);

        currentOutlinePos = pos;
    }

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