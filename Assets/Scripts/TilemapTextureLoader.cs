using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapTextureLoader : Singleton<TilemapTextureLoader>
{
    [Header("아틀라스 설정")]
    public Texture2D tilemapTexture; // 슬라이스한 타일맵 PNG 파일 등록
    public Vector2Int atlasGridSize = new Vector2Int(16, 16); // 아틀라스의 전체 가로/세로 타일 개수 (예: 16x16칸짜리 이미지)
    public Vector2Int frontAtlasBase = new Vector2Int(8, 4); // X=8, Y=4
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

        // [전제 조건] blocksTilemap에 블록 데이터가 있는 칸만 연산합니다.
        if (blocksTilemap.GetTile(currentGridPos) == null) return;

        // 1단계: 주변 8방향 비트마스크 계산
        int bitmask = CalculateBitmask(pos);

        // =================================================================
        // 2단계: 탑다운 정면 벽(Front) 조건 판단
        // =================================================================
        // 내 칸(pos)에 블록 데이터가 있고, 
        // 내 '아래 칸'(pos + Vector2Int.down)이 빈 공간(바닥, null)일 때 
        // 현재 내 칸에 정면 벽(Front)을 그려서 아래 바닥으로 낭떠러지가 떨어지는 입체감을 만듭니다.
        Vector2Int belowPos = pos + Vector2Int.down;
        Vector3Int belowGridPos = (Vector3Int)belowPos;

        if (blocksTilemap.GetTile(belowGridPos) == null)
        {
            // 규칙에 따라 정면 벽 아틀라스 좌표 계산 (X홀짝, Y코너 형태)
            Vector2Int frontAtlas = GetFrontWallAtlasCoordinate(pos, (byte)bitmask);

            if (frontAtlas.x >= 0 && frontAtlas.y >= 0)
            {
                Tile frontWallTile = CreateRuntimeTile(frontAtlas);
                if (frontWallTile != null)
                {
                    wallTextureTilemap.SetTile(currentGridPos, frontWallTile);
                    return; // 정면 벽을 그렸으므로 윗면 연산은 하지 않고 종료합니다.
                }
            }
        }

        // =================================================================
        // 3단계: 내 아래 칸도 블록으로 꽉 차 있다면 ➔ 벽 윗면(Top) 및 내부 채우기 그리기
        // =================================================================
        // 사방이 꽉 찬 회색 내부 영역은 내 아래 칸도 당연히 블록이므로 
        // 이 분기로 넘어와 bitmask = 255를 인지하고 46번(Full_Solid_Wall, 검은색 윗면)을 빽빽하게 채우게 됩니다.
        Vector2Int topAtlas = TileAtlasManager.Instance.GetAtlasCoordinate((byte)bitmask);
        Tile topTile = CreateRuntimeTile(topAtlas);

        if (topTile != null)
        {
            wallTextureTilemap.SetTile(currentGridPos, topTile);
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
        bool s = (topTileBitmask & 16) != 0;
        bool e = (topTileBitmask & 4) != 0;
        bool w = (topTileBitmask & 64) != 0;

        // 2. 윗면 타일의 SE, SW 코너 상태 연산
        string seC = (!e && !s) ? "outer" : (e && s) ? (((topTileBitmask & 8) != 0) ? "dark" : "inner") : "outer";
        string swC = (!s && !w) ? "outer" : (s && w) ? (((topTileBitmask & 32) != 0) ? "dark" : "inner") : "outer";

        // 규칙: SE, SW가 둘 다 outer이고 S가 비어있어야(false) 정면 벽이 내려옴
        if (seC != "outer" || swC != "outer" || s == true)
        {
            return new Vector2Int(-1, -1); // 벽 생성 안 함 플래그
        }

        // 3. Y 좌표 오프셋 결정 (벽의 형태 레이아웃)
        int yOffset = 0;
        if (!w && !e) yOffset = 0; // 양쪽 코너 (Isolated)
        else if (w && e) yOffset = 1; // 코너 없음 (Straight)
        else if (!w && e) yOffset = 2; // 왼쪽 코너 (Left Corner)
        else if (w && !e) yOffset = 3; // 오른쪽 코너 (Right Corner)

        // 4. X 좌표 오프셋 결정 (홀짝 및 보석 여부)
        int xOffset = 0;
        bool isEvenX = (Mathf.Abs(wallGridPos.x) % 2 == 0);

        bool isGem = UnityEngine.Random.value < gemChance;

        if (!isGem)
        {
            // 일반 벽 변형
            xOffset = isEvenX ? 1 : 0;
        }
        else
        {
            // 보석 벽 변형
            xOffset = isEvenX ? 3 : 2;
        }

        // 5. 기준 좌표(8, 4)에 오프셋을 더해 최종 아틀라스 좌표 반환
        return new Vector2Int(frontAtlasBase.x + xOffset, frontAtlasBase.y + yOffset);
    }
}