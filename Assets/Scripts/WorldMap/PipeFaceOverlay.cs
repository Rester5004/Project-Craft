using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 렌치로 지정한 기계 면을 색 막대로 보여 준다. 파랑 = 기계에 넣기만, 빨강 = 기계에서 꺼내기만.
///
/// <b>타일맵을 쓰지 않는 이유</b>: 한 파이프가 기계 두 대에 닿으면 두 면이 동시에 칠해져야 하는데
/// 타일맵은 셀당 타일이 하나뿐이다. 칠해지는 면은 플레이어가 손으로 설정한 것뿐이라 수가 적어
/// <see cref="SpriteRenderer"/> 를 풀에서 꺼내 쓰는 편이 싸고 단순하다.
///
/// 끊긴 면(<see cref="PipeFaceMode.Cut"/>)은 여기서 그리지 않는다 —
/// 양쪽 스프라이트가 막힌 끝 모양으로 바뀌는 것이 이미 충분한 표시다.
/// </summary>
public class PipeFaceOverlay : MonoBehaviour
{
    /// <summary>
    /// 파이프(120)·플레이어(130)·벽 윗면(140) 위. 가려지는 것보다 보이는 쪽이 중요하다.
    /// ⚠ 예전 배율의 <c>5</c> 였는데 정렬 순서가 <c>(옛값 + 10) × 10</c> 으로 바뀔 때 빠졌다 —
    /// 5 는 바닥(100)보다 아래라 <b>막대가 통째로 안 보였다.</b>
    /// </summary>
    private const int SortingOrder = 150;

    private static readonly Color InsertColor = new Color(0.30f, 0.62f, 1f, 0.95f);
    private static readonly Color ExtractColor = new Color(1f, 0.35f, 0.30f, 0.95f);

    private const float EdgeOffset = 0.42f;   // 셀 중심에서 이음매까지
    private const float BarLength = 0.36f;    // 파이프 굵기(0.25)보다 조금 넓게 — 덮지 않고 감싸 보이도록
    private const float BarThickness = 0.14f;

    /// <summary>셀 중심 좌표를 얻는 데만 쓴다(격자 원점·셀 크기를 여기서 다시 가정하지 않도록).</summary>
    public Tilemap Reference { get; set; }

    private Sprite barSprite;
    private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();

    /// <summary>지금 칠해야 할 면을 전부 다시 그린다. 남는 막대는 꺼 둔다.</summary>
    public void Rebuild(IEnumerable<KeyValuePair<Vector2Int, PipeCell>> cells)
    {
        int used = 0;

        foreach (KeyValuePair<Vector2Int, PipeCell> pair in cells)
        {
            PipeCell pipe = pair.Value;
            if (pipe == null || pipe.record == null) continue;

            for (int dir = 0; dir < PipeRouter.Directions.Length; dir++)
            {
                PipeFaceMode mode = PipeRouter.FaceOf(pipe.record, dir);
                if (mode != PipeFaceMode.Insert && mode != PipeFaceMode.Extract) continue;

                // 기계가 실제로 있을 때만 그린다. 설정 자체는 기계를 캐도 남겨 두므로
                // (같은 기계를 다시 세우면 그대로 살아난다) 여기서 걸러야 빈 바닥에 막대가 뜨지 않는다.
                if (!PipeRouter.MachineAt(pair.Key + PipeRouter.Directions[dir])) continue;

                Place(used++, pair.Key, dir, mode == PipeFaceMode.Insert ? InsertColor : ExtractColor);
            }
        }

        for (int i = used; i < pool.Count; i++)
            if (pool[i] != null) pool[i].gameObject.SetActive(false);
    }

    private void Place(int index, Vector2Int cell, int dir, Color color)
    {
        SpriteRenderer bar = Take(index);
        Vector2Int direction = PipeRouter.Directions[dir];

        Vector3 center = Reference != null
            ? Reference.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0))
            : new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

        bar.transform.position = center + new Vector3(direction.x, direction.y, 0f) * EdgeOffset;
        // 막대는 면과 나란해야 한다 — 위아래 면이면 가로로 눕고, 좌우 면이면 세로로 선다.
        bar.transform.localScale = direction.x != 0
            ? new Vector3(BarThickness, BarLength, 1f)
            : new Vector3(BarLength, BarThickness, 1f);
        bar.color = color;
        bar.gameObject.SetActive(true);
    }

    private SpriteRenderer Take(int index)
    {
        while (pool.Count <= index)
        {
            GameObject host = new GameObject("PipeFace");
            host.transform.SetParent(transform, false);

            SpriteRenderer renderer = host.AddComponent<SpriteRenderer>();
            renderer.sprite = EnsureSprite();
            renderer.sortingOrder = SortingOrder;
            pool.Add(renderer);
        }
        return pool[index];
    }

    /// <summary>1×1 흰 점 하나. 모양은 전부 스케일로 낸다(막대 그림을 따로 그릴 필요가 없다).</summary>
    private Sprite EnsureSprite()
    {
        if (barSprite != null) return barSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.filterMode = FilterMode.Point;
        texture.Apply();

        barSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return barSprite;
    }
}
