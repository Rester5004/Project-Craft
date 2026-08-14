using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 손에 든 배치물이 <b>어디에 어떻게 놓일지</b>를 커서 자리에 미리 그린다.
/// 반투명한 기계 그림 한 장과, 발자국 칸마다 놓이는 초록/빨강 사각형으로 이루어진다.
///
/// <b>씬·프리팹을 건드리지 않으려고 런타임에 만든다</b>(<see cref="MapGenerator"/> 가
/// <see cref="PipeNetworkManager"/> 와 같은 방식으로 세운다).
///
/// <b>타일맵이 아니라 <see cref="SpriteRenderer"/> 풀인 이유</b>는
/// <see cref="PipeFaceOverlay"/> 와 같다 — 기계 그림과 칸 색이 같은 칸에 겹쳐야 하는데
/// 타일맵은 셀당 타일이 하나뿐이다. 게다가 기계 그림은 발자국보다 크게 삐져나올 수 있다.
///
/// ⚠ <b>놓을 수 있는지 판정하는 것은 여기가 아니다.</b> 판정은 전부
/// <c>PlayerInteraction.CanPlaceFootprint</c> 한 곳이 하고 이 클래스는 결과만 칠한다 —
/// 판정을 두 벌로 두면 "초록인데 안 놓이는" 상태가 반드시 생긴다.
/// </summary>
public class PlacementPreview : MonoBehaviour
{
    /// <summary>PowerLink 오버레이(160)보다 위. 칸 색은 170, 기계 그림은 그 위인 180이다.</summary>
    private const int CellSortingOrder = 170;
    private const int GhostSortingOrder = 180;

    private static readonly Color OkColor = new Color(0.35f, 0.95f, 0.45f, 0.35f);
    private static readonly Color BlockedColor = new Color(1f, 0.30f, 0.25f, 0.45f);

    /// <summary>기계 그림은 지형이 비쳐 보일 만큼만 남긴다.</summary>
    private const float GhostAlpha = 0.55f;

    /// <summary>셀 중심 좌표를 얻는 데만 쓴다(격자 원점·셀 크기를 여기서 다시 가정하지 않도록).</summary>
    public Tilemap Reference { get; set; }

    public static PlacementPreview Active { get; private set; }

    private Sprite cellSprite;
    private SpriteRenderer ghost;
    private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();

    /// <summary>씬을 건드리지 않고 만든다. 이미 있으면 그것을 돌려준다.</summary>
    public static PlacementPreview EnsureCreated(Transform parent, Tilemap reference)
    {
        if (Active != null) return Active;

        GameObject go = new GameObject("PlacementPreview");
        if (parent != null) go.transform.SetParent(parent, false);

        Active = go.AddComponent<PlacementPreview>();
        Active.Reference = reference;
        return Active;
    }

    private void OnDestroy()
    {
        if (Active == this) Active = null;
    }

    /// <summary>
    /// 발자국을 그린다. <paramref name="blocked"/> 에 든 칸만 빨강이고 나머지는 초록이다.
    /// <paramref name="sprite"/> 가 없으면 칸 색만 그린다(그림이 없는 배치물도 자리는 보여야 한다).
    /// </summary>
    public void Show(Vector2Int origin, Vector2Int size, Sprite sprite, ICollection<Vector2Int> blocked)
    {
        int w = Mathf.Max(1, size.x);
        int h = Mathf.Max(1, size.y);

        int used = 0;
        foreach (Vector2Int cell in WorldMap.Cells(origin, new Vector2Int(w, h)))
        {
            bool bad = blocked != null && blocked.Contains(cell);
            PlaceCell(used++, cell, bad ? BlockedColor : OkColor);
        }
        for (int i = used; i < pool.Count; i++) pool[i].gameObject.SetActive(false);

        ShowGhost(origin, w, h, sprite);
    }

    /// <summary>미리보기를 감춘다. 오브젝트는 풀에 남겨 다음에 다시 쓴다.</summary>
    public void Hide()
    {
        for (int i = 0; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        if (ghost != null) ghost.gameObject.SetActive(false);
    }

    private void ShowGhost(Vector2Int origin, int w, int h, Sprite sprite)
    {
        if (sprite == null)
        {
            if (ghost != null) ghost.gameObject.SetActive(false);
            return;
        }

        if (ghost == null)
        {
            GameObject host = new GameObject("Ghost");
            host.transform.SetParent(transform, false);
            ghost = host.AddComponent<SpriteRenderer>();
            ghost.sortingOrder = GhostSortingOrder;
        }

        ghost.sprite = sprite;
        ghost.color = new Color(1f, 1f, 1f, GhostAlpha);
        // 기준점은 왼쪽 아래 칸이고 스프라이트 피벗은 Center 라, 실제로 세워질 자리와 똑같이
        // 발자국 정중앙으로 반 칸씩 민다(MapGenerator.SpawnPlaceable 과 같은 식이어야 한다).
        ghost.transform.position = CenterOf(origin) + new Vector3((w - 1) * 0.5f, (h - 1) * 0.5f, 0f);
        ghost.gameObject.SetActive(true);
    }

    private void PlaceCell(int index, Vector2Int cell, Color color)
    {
        SpriteRenderer square = Take(index);
        square.transform.position = CenterOf(cell);
        square.color = color;
        square.gameObject.SetActive(true);
    }

    private Vector3 CenterOf(Vector2Int cell)
        => Reference != null
            ? Reference.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0))
            : new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

    private SpriteRenderer Take(int index)
    {
        while (pool.Count <= index)
        {
            GameObject host = new GameObject("PreviewCell");
            host.transform.SetParent(transform, false);

            SpriteRenderer renderer = host.AddComponent<SpriteRenderer>();
            renderer.sprite = EnsureCellSprite();
            renderer.sortingOrder = CellSortingOrder;
            pool.Add(renderer);
        }
        return pool[index];
    }

    /// <summary>한 칸을 정확히 채우는 흰 사각형(1픽셀 × PPU 1). 새 아트 에셋이 필요 없다.</summary>
    private Sprite EnsureCellSprite()
    {
        if (cellSprite != null) return cellSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.filterMode = FilterMode.Point;
        texture.Apply();

        cellSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return cellSprite;
    }
}
