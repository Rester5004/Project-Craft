using UnityEngine;

/// <summary>
/// 걷는 동안 발밑 바닥의 소리를 <b>루프로</b> 튼다.
///
/// 왜 걸음마다 <c>PlayOneShot</c> 이 아닌가: 클립 한 장에 이미 여러 걸음이 들어 있다
/// (WalkOnStone 2.54초에 8걸음 · WalkOnDirt 2.10초에 6걸음). 걸음마다 새로 쏘면 8배로 겹친다.
/// 그래서 <see cref="SfxPlayer"/>(단발 전용)와 달리 <b>자기 <c>AudioSource</c> 를 켜고 끈다</b>.
///
/// 어느 바닥이 어떤 소리인지는 <see cref="MainBlock.footstepSound"/> 가 정하고
/// 조회는 <see cref="WorldMap.FootstepAt"/> 한 곳이 한다 — 여기서는 <b>언제 켜고 끄는지만</b> 안다.
/// </summary>
public class PlayerFootsteps : MonoBehaviour
{
    [Tooltip("발소리 전용. loop 1 · playOnAwake 0 · spatialBlend 0(2D) 으로 프리팹에 저작한다.")]
    [SerializeField] private AudioSource source;

    [Tooltip("걷는 중인지 묻는 상대. PlayerMove 는 UI가 열리면 입력을 이미 0으로 만든다.")]
    [SerializeField] private PlayerMove move;

    [Tooltip("월드 좌표 → 셀 변환에 쓰는 타일맵의 주인. PlayerInteraction 과 같은 변환을 써야 한 칸도 안 어긋난다.")]
    [SerializeField] private MapGenerator mapGenerator;

    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    // 마지막으로 조회한 칸. 같은 칸에 서 있는 동안은 딕셔너리를 다시 뒤지지 않는다.
    private Vector2Int lastCell;
    private bool hasCell;

    private void Awake()
    {
        if (move == null) move = GetComponent<PlayerMove>();
        if (source == null) source = GetComponent<AudioSource>();
        if (source != null) { source.loop = true; source.playOnAwake = false; }
    }

    private void Update()
    {
        if (source == null) return;

        if (move == null || !move.IsMoving)
        {
            // 멈추면 바로 끊는다. Stop 이라 다시 걸을 때 클립 맨 앞(= 첫 발소리)부터 시작한다.
            if (source.isPlaying) source.Stop();
            hasCell = false;
            return;
        }

        if (mapGenerator == null || mapGenerator.blocksTilemap == null) return;

        Vector2Int cell = (Vector2Int)mapGenerator.blocksTilemap.WorldToCell(transform.position);
        if (!hasCell || cell != lastCell)
        {
            lastCell = cell;
            hasCell = true;

            // ⚠ Instance 가 아니라 InstanceIfAlive — 게터는 못 찾으면 월드를 만들어 버리고,
            //    Awake 를 안 거친 그 유령이 종료 저장에서 세이브를 잘라 먹는다.
            WorldMap map = WorldMap.InstanceIfAlive;
            AudioClip clip = map != null ? map.FootstepAt(cell) : null;

            if (clip != source.clip)
            {
                source.clip = clip;
                if (clip != null) source.Play();   // 바닥을 넘어가는 순간 소리가 바뀐다
            }
        }

        if (source.clip == null)
        {
            if (source.isPlaying) source.Stop();   // 소리를 배정하지 않은 바닥(물 등)은 조용하다
            return;
        }

        source.volume = volume;
        if (!source.isPlaying) source.Play();
    }
}
