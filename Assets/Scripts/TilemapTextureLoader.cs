using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapTextureLoader : Singleton<TilemapTextureLoader>
{
    [Header("아틀라스 설정")]
    public Texture2D tilemapTexture; // 슬라이스한 타일맵 PNG 파일 등록
    public Vector2Int atlasGridSize = new Vector2Int(16, 16); // 아틀라스의 전체 가로/세로 타일 개수 (예: 16x16칸짜리 이미지)
    public Vector2Int frontAtlasBase = new Vector2Int(8, 0); // X=8, Y=4
    [Range(0f, 1f)]
    public float gemChance = 0.3f; // 보석이 등장할 확률 (8%)
    public Sprite testFloorTexture;

    [Header("타일맵")]
    [SerializeField] Tilemap wallTextureTilemap; // 타일맵 컴포넌트 연결
    [SerializeField] Tilemap floorTextureTilemap; // 타일맵 컴포넌트 연결
    [SerializeField] Tilemap blocksTilemap;
    [SerializeField] Tilemap floorTilemap;

    // 좌표로 스프라이트를 초고속 탐색하기 위한 2차원 배열
    private Sprite[,] spriteAtlasTable;

    protected override void Awake()
    {
        base.Awake();
        CacheAtlasSprites();
    }

    private void CacheAtlasSprites()
    {
        spriteAtlasTable = new Sprite[atlasGridSize.x, atlasGridSize.y];

        // Resources 폴더 기준으로 확장자를 제외한 경로를 적어줍니다.
        // LoadAll<Sprite>는 하위 에셋 중 'Sprite' 타입만 알아서 필터링해서 배열로 반환합니다.
        Sprite[] allSprites = Resources.LoadAll<Sprite>("Tilesets/stage2_wall_tilemap");

        if (allSprites == null || allSprites.Length == 0)
        {
            Debug.LogError("Resources에서 스프라이트 아틀라스를 찾을 수 없습니다!");
            return;
        }

        foreach (Sprite sprite in allSprites)
        {
            int gridX = Mathf.RoundToInt(sprite.rect.x / sprite.rect.width);
            int gridY = Mathf.RoundToInt(sprite.rect.y / sprite.rect.height);

            if (gridX < atlasGridSize.x && gridY < atlasGridSize.y)
            {
                spriteAtlasTable[gridX, gridY] = sprite;
            }
        }
    }

    /// <summary>
    /// 앞서 계산한 아틀라스 좌표(Vector2Int)를 던지면 유니티 Tile 객체를 동적으로 생성해 반환합니다.
    /// </summary>
    public Tile CreateRuntimeTile(Vector2Int atlasCoord)
    {
        if (atlasCoord.x < 0 || atlasCoord.y < 0) return null;

        // 1. 2차원 캐시 배열에서 해당 좌표의 스프라이트 쏙 빼오기
        Sprite targetSprite = spriteAtlasTable[atlasCoord.x, atlasCoord.y];

        if (targetSprite != null)
        {
            // 2. 유니티 타일맵에 찍을 수 있는 Scriptable Tile 인스턴스 생성
            Tile newTile = ScriptableObject.CreateInstance<Tile>();
            newTile.sprite = targetSprite;
            return newTile;
        }

        return null;
    }

    public void LoadFloorTexture(Vector2Int pos)
    {
        Tile floorTile = ScriptableObject.CreateInstance<Tile>();
        floorTile.sprite = testFloorTexture;
        floorTextureTilemap.SetTile((Vector3Int)pos, floorTile);
    }

    public void LoadWallTexture(Vector2Int pos)
    {
        Vector3Int currentGridPos = (Vector3Int)pos;

        // 1. 데이터 상에 블록이 없으면 패스
        if (blocksTilemap.GetTile(currentGridPos) == null) return;

        // 2. 현재 pos 기준 8방향 비트마스크 및 조견표(윗면) 아틀라스 좌표 추출
        int bitmask = CalculateBitmask(pos);
        Vector2Int topAtlas = TileAtlasManager.Instance.GetAtlasCoordinate((byte)bitmask);

        // =================================================================
        // [단계 1] 한 칸 위(Y + 1) 좌표에 "벽 윗면(Top Wall)" 그리기
        // =================================================================
        Vector3Int topGridPos = currentGridPos + Vector3Int.up;
        Tile topWallTile = CreateRuntimeTile(topAtlas);

        if (topWallTile != null)
        {
            wallTextureTilemap.SetTile(topGridPos, topWallTile);
        }

        // =================================================================
        // [단계 2] 현재 제자리(pos) 좌표에 "앞면 벽(Front Wall)" 그리기
        // =================================================================
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

        // 최종 앞면 아틀라스 좌표 확정 및 생성
        Vector2Int frontAtlas = new Vector2Int(frontAtlasBase.x + xOffset, frontAtlasBase.y + yOffset);
        Tile frontWallTile = CreateRuntimeTile(frontAtlas);

        if (frontWallTile != null)
        {
            wallTextureTilemap.SetTile(currentGridPos, frontWallTile);
        }
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
        wallTextureTilemap.SetTile((Vector3Int)pos, null);
    }

    /// <summary>
    /// 윗면 타일의 비트마스크와 배치될 벽의 그리드 좌표를 기반으로 
    /// 정면 벽의 아틀라스 좌표(Vector2Int)를 계산하여 반환합니다.
    /// 벽을 생성하지 않아야 하는 경우 Vector2Int(-1, -1)을 반환합니다.
    /// </summary>
    private Vector2Int GetFrontWallAtlasCoordinate(Vector2Int wallGridPos, byte topTileBitmask)
    {
        // 1. 윗면 타일의 변 연결성 분해
        bool e = (topTileBitmask & 4) != 0;  // 오른쪽 블록 존재 여부
        bool w = (topTileBitmask & 64) != 0; // 왼쪽 블록 존재 여부

        // 2. Y 좌표 오프셋 결정 (양옆의 연결 상태만 봅니다)
        int yOffset = 0;
        if (!w && !e) yOffset = 0;      // 양쪽이 다 뚫린 고립된 정면 벽 (Isolated)
        else if (w && e) yOffset = 1;   // 사방이 꽉 찬 내부 및 연속된 정면 벽 (Straight)
        else if (!w && e) yOffset = 2;  // 왼쪽만 뚫리고 오른쪽은 막힌 정면 벽 (Left Corner)
        else if (w && !e) yOffset = 3;  // 왼쪽은 막히고 오른쪽이 뚫린 정면 벽 (Right Corner)

        // 3. X 좌표 오프셋 결정 (기존의 홀짝 패턴 및 보석 확률 유지)
        int xOffset = 0;
        bool isEvenX = (Mathf.Abs(wallGridPos.x) % 2 == 0);
        bool isGem = UnityEngine.Random.value < gemChance;

        if (!isGem)
        {
            xOffset = isEvenX ? 1 : 0; // 일반 벽 변형 (X축 홀짝)
        }
        else
        {
            xOffset = isEvenX ? 3 : 2; // 보석 벽 변형
        }

        // 4. 기준 좌표(8, 4)에 오프셋을 더해 최종 아틀라스 좌표 반환
        return new Vector2Int(frontAtlasBase.x + xOffset, frontAtlasBase.y + yOffset);
    }
}