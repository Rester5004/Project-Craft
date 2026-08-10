using UnityEngine;

/// <summary>
/// 지하 씬에서만 하는 일. 지형·전리품·물은 <see cref="UndergroundWorld"/> 가 이미 만들어 두었고
/// 플레이어·UI·타일맵은 <c>GameRig</c> 프리팹이 들고 온다 — 여기 남은 것은 <b>둘뿐</b>이다:
/// 플레이어를 방 한가운데로 옮기는 것과, 돌아갈 포탈을 세우는 것.
///
/// <b>Awake 에서 옮기는 이유</b>: <c>MapGenerator.Start</c> 가 곧바로 플레이어 자리를 보고 청크를 부른다.
/// Start 에서 옮기면 엉뚱한 청크를 한 번 그린 뒤에야 따라온다.
/// </summary>
[DefaultExecutionOrder(-50)]
public class UndergroundSceneSetup : MonoBehaviour
{
    private void Awake()
    {
        Vector2 spawn = new(UndergroundWorld.SpawnCell.x + 0.5f, UndergroundWorld.SpawnCell.y + 0.5f);

        PlayerForTest player = FindFirstObjectByType<PlayerForTest>();
        if (player != null)
        {
            player.transform.position = new Vector3(spawn.x, spawn.y, player.transform.position.z);

            // Rigidbody2D 로 움직이므로 물리 위치까지 맞춰야 한 프레임 뒤에 되돌아가지 않는다
            // (PlayerSave.SetPosition 과 같은 이유).
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null) { body.position = spawn; body.linearVelocity = Vector2.zero; }
        }
        else Debug.LogWarning("[UndergroundSceneSetup] 플레이어를 찾지 못했습니다 — GameRig 프리팹이 씬에 있는지 확인하세요.");
    }

    private void Start()
    {
        // 포탈은 플레이어 발밑(스폰 칸)에 둔다. 놓칠 수 없는 자리이고, 방이 좁아 따로 찾아다닐 것도 없다.
        Vector2 spawn = new(UndergroundWorld.SpawnCell.x + 0.5f, UndergroundWorld.SpawnCell.y + 0.5f);
        UndergroundPortal.Create(spawn, UndergroundPortal.Kind.ToSurface, UndergroundSession.Tier);
    }
}
