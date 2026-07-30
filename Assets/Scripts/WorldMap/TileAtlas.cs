using UnityEngine;

/// <summary>
/// 격자로 슬라이스된 타일 시트 하나. 오토타일링이 쓰는 (x, y) 아틀라스 좌표로 스프라이트를 찾아 준다.
///
/// 예전에는 <c>Resources.LoadAll</c> 로 시트를 읽었지만, 그러면 타일 에셋이 Resources 폴더에 묶이고
/// 지역마다 다른 시트를 쓸 수도 없었다. 시트를 에셋으로 참조하게 만들어 두 문제를 한 번에 없앤다.
///
/// <see cref="sprites"/> 는 손으로 채우지 않는다 — Tools/Tiles/Build Tile Atlas 메뉴가 시트에서 뽑아 넣는다.
/// </summary>
[CreateAssetMenu(fileName = "TileAtlas", menuName = "Tiles/Tile Atlas")]
public class TileAtlas : ScriptableObject
{
    [Tooltip("아틀라스의 가로/세로 칸 수. 이 범위를 벗어난 스프라이트는 버린다.")]
    public Vector2Int gridSize = new Vector2Int(12, 8);

    [Tooltip("시트에서 슬라이스된 스프라이트 전부. 격자 좌표는 sprite.rect 로 계산하므로 순서는 상관없다.")]
    public Sprite[] sprites;

    /// <summary>
    /// 스프라이트를 격자 좌표로 색인한 표를 만든다. 빈 칸은 null 이다.
    /// rect 기준이라 시트에 구멍이 있어도(스테이지1 시트처럼) 나머지 칸 위치가 밀리지 않는다.
    /// </summary>
    public Sprite[,] BuildTable()
    {
        Sprite[,] table = new Sprite[Mathf.Max(1, gridSize.x), Mathf.Max(1, gridSize.y)];

        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogError($"[TileAtlas] '{name}' 에 스프라이트가 없습니다. Tools/Tiles/Build Tile Atlas 로 채우세요.", this);
            return table;
        }

        foreach (Sprite sprite in sprites)
        {
            if (sprite == null) continue;

            int gridX = Mathf.RoundToInt(sprite.rect.x / sprite.rect.width);
            int gridY = Mathf.RoundToInt(sprite.rect.y / sprite.rect.height);

            if (gridX >= 0 && gridX < table.GetLength(0) && gridY >= 0 && gridY < table.GetLength(1))
                table[gridX, gridY] = sprite;
        }

        return table;
    }
}
