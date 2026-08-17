using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>
/// 벽이 빛을 막게 한다. 로드된 청크마다 벽 칸을 <b>그리디 메싱</b>으로 큰 사각형 몇 개로 묶고,
/// 사각형마다 <see cref="ShadowCaster2D"/> 를 하나 세운다.
///
/// <b>씬·프리팹을 건드리지 않으려고 런타임에 만든다</b>
/// (<see cref="PipeNetworkManager"/> · <see cref="PlacementPreview"/> · <see cref="MapLighting"/> 와 같은 규약).
///
/// <b>왜 칸마다 하나가 아니라 그리디 메싱인가</b>: 이 세계는 대부분 꽉 찬 돌이라
/// 통째로 벽인 청크는 사각형 <b>1개</b>로 줄어든다. 칸마다 세우면 청크 하나에 256개다.
///
/// <b>⚠ 캐스터 오브젝트에 <see cref="SpriteRenderer"/> 를 붙이면 안 된다.</b>
/// <see cref="ShadowCaster2D.Awake"/> 는 <c>shapePath</c> 가 비어 있을 때(= AddComponent 로 갓 만든 것은
/// 언제나 비어 있다) <b>스프라이트에서 모양을 뽑는 provider 를 먼저 고른다.</b> 그런데 그 provider
/// (<c>ShadowShape2DProvider_SpriteRenderer</c>)는 <c>drawMode</c> 가 Simple 이면 스프라이트 메시를 쓰는데,
/// 1×1 스프라이트로는 <b>정점 0개짜리 빈 그림자 메시</b>가 나온다 — 벽이 빛을 전혀 막지 않는다(실측).
/// 게다가 그 경로는 <c>#if UNITY_EDITOR</c> 안에만 있어 <b>에디터와 빌드가 다르게 동작</b>한다.
///
/// 렌더러가 없으면 provider 가 잡히지 않아 <c>ShapeEditor</c> 로 떨어지고, <c>Awake</c> 가
/// <c>Bounds(position, one)</c> 에서 <b>로컬 단위 사각형</b>을 만든다. 그래서
/// <b>배율 1 로 만들어 모양을 굳힌 뒤 <c>localScale = (w,h,1)</c> 로 늘린다</b> —
/// 렌더링이 <c>transform.localToWorldMatrix</c> 를 곱해 정확히 w×h 그림자가 된다.
/// 덕분에 <b>풀에서 꺼내 쓸 때 모양을 다시 만들 필요가 없다</b>(위치·크기만 바꾸면 된다).
/// </summary>
public class ChunkShadowCasters : MonoBehaviour
{
    public static ChunkShadowCasters Active { get; private set; }

    [Tooltip("셀 중심 좌표를 얻는 데만 쓴다(격자 원점·셀 크기를 여기서 다시 가정하지 않도록). PlaceableObjects 타일맵.")]
    [SerializeField] private Tilemap reference;

    private readonly Dictionary<Vector2Int, Transform> roots = new();
    private readonly Dictionary<Vector2Int, List<GameObject>> live = new();
    private readonly Stack<GameObject> pool = new();

    // 그리디 메싱 작업 버퍼. 청크마다 새로 할당하지 않으려고 들고 있는다.
    private readonly bool[,] wall = new bool[WorldMap.ChunkSize, WorldMap.ChunkSize];
    private readonly bool[,] used = new bool[WorldMap.ChunkSize, WorldMap.ChunkSize];

    // 이 컴포넌트는 GameRig 프리팹에 저작돼 있다. Awake 는 어떤 Start 보다 먼저 도므로
    // MapGenerator.Start 안의 첫 UpdateChunks() → RenderChunk 가 이미 선 Active 를 본다.
    private void Awake()
    {
        if (Active == null) Active = this;
    }

    private void OnDestroy()
    {
        if (Active == this) Active = null;
    }

    /// <summary>청크 하나의 벽 그림자를 (다시) 만든다. 이미 있으면 먼저 반납한다.</summary>
    public void Build(Vector2Int chunkId, Chunk chunk)
    {
        if (chunk == null) return;
        Release(chunkId);

        int size = WorldMap.ChunkSize;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                wall[x, y] = Chunk.IsWall(chunk.GetTile(x, y));
                used[x, y] = false;
            }

        Transform root = EnsureRoot(chunkId);
        List<GameObject> casters = new();

        // 그리디 메싱 — 가로로 최대한 늘린 뒤, 그 폭이 통째로 벽인 동안 위로 늘린다.
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (!wall[x, y] || used[x, y]) continue;

                int w = 1;
                while (x + w < size && wall[x + w, y] && !used[x + w, y]) w++;

                int h = 1;
                while (y + h < size)
                {
                    bool rowClear = true;
                    for (int i = 0; i < w && rowClear; i++)
                        rowClear = wall[x + i, y + h] && !used[x + i, y + h];
                    if (!rowClear) break;
                    h++;
                }

                for (int dy = 0; dy < h; dy++)
                    for (int dx = 0; dx < w; dx++)
                        used[x + dx, y + dy] = true;

                casters.Add(Place(root, chunkId * size + new Vector2Int(x, y), w, h));
                x += w - 1;
            }

        live[chunkId] = casters;
    }

    /// <summary>청크의 그림자를 반납한다(오브젝트는 풀에 남겨 다음에 다시 쓴다).</summary>
    public void Release(Vector2Int chunkId)
    {
        if (!live.TryGetValue(chunkId, out List<GameObject> casters)) return;

        foreach (GameObject go in casters)
        {
            if (go == null) continue;
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            pool.Push(go);
        }
        live.Remove(chunkId);
    }

    /// <summary>지금 서 있는 캐스터 개수(검증용).</summary>
    public int ActiveCount
    {
        get
        {
            int n = 0;
            foreach (var kvp in live) n += kvp.Value.Count;
            return n;
        }
    }

    /// <summary>청크별 캐스터 개수(검증용).</summary>
    public int CountOf(Vector2Int chunkId)
        => live.TryGetValue(chunkId, out List<GameObject> casters) ? casters.Count : 0;

    /// <summary>
    /// 청크 부모. <see cref="CompositeShadowCaster2D"/> 를 달아 한 덩어리로 묶는다 —
    /// 안 묶으면 붙어 있는 사각형끼리 서로 그림자를 드리워 <b>이음매가 줄로 보인다</b>.
    /// </summary>
    private Transform EnsureRoot(Vector2Int chunkId)
    {
        if (roots.TryGetValue(chunkId, out Transform existing) && existing != null) return existing;

        GameObject go = new GameObject($"Chunk {chunkId.x},{chunkId.y}");
        go.transform.SetParent(transform, false);
        go.AddComponent<CompositeShadowCaster2D>();
        roots[chunkId] = go.transform;
        return go.transform;
    }

    /// <summary>사각형 하나를 세운다. <paramref name="origin"/> 은 왼쪽 아래 <b>월드 셀</b>.</summary>
    private GameObject Place(Transform root, Vector2Int origin, int w, int h)
    {
        Vector3 centre = CenterOf(origin) + new Vector3((w - 1) * 0.5f, (h - 1) * 0.5f, 0f);

        if (pool.Count > 0)
        {
            // 재사용: 모양은 이미 로컬 단위 사각형으로 굳어 있으므로 위치·크기만 바꾸면 된다.
            GameObject reused = pool.Pop();
            reused.transform.SetParent(root, false);
            reused.transform.position = centre;
            reused.transform.localScale = new Vector3(w, h, 1f);
            reused.SetActive(true);
            return reused;
        }

        GameObject go = new GameObject("WallShadow");
        go.transform.SetParent(root, false);

        // ⚠ 순서가 중요하다. 배율이 1 인 채로 ShadowCaster2D 를 붙여야 Awake 가
        //    <b>로컬 단위 사각형</b>(±0.5)으로 모양을 굳힌다. 배율을 먼저 주면 그만큼 나눠져
        //    모양이 1/w × 1/h 로 작아지고, 거기에 다시 배율이 곱해져 결국 1×1 그림자가 된다.
        //    ⚠ SpriteRenderer 를 붙이면 안 된다 — 클래스 주석 참고(빈 그림자 메시가 나온다).
        ShadowCaster2D caster = go.AddComponent<ShadowCaster2D>();
        caster.castsShadows = true;
        caster.selfShadows = false;

        go.transform.position = centre;
        go.transform.localScale = new Vector3(w, h, 1f);
        return go;
    }

    private Vector3 CenterOf(Vector2Int cell)
        => reference != null
            ? reference.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0))
            : new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
}
