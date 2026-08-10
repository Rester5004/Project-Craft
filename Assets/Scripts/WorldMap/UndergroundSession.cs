using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 지하맵에 들어가 있는 동안의 상태 <b>한 곳</b>. static 인 이유는 씬을 넘나들며 살아 있어야 하는데
/// 오브젝트로 두면 그 오브젝트를 또 <c>DontDestroyOnLoad</c> 로 지켜야 하기 때문이다.
///
/// 지하는 저장하지 않으므로(<see cref="WorldMap.IsEphemeral"/>) 여기 값도 저장하지 않는다 —
/// 게임을 끄면 방은 사라지고, 다음 실행은 <see cref="SurfaceReturnPosition"/> 에서 시작한다.
/// </summary>
public static class UndergroundSession
{
    public const string SurfaceSceneName = "MapTest";
    public const string UndergroundSceneName = "UndergroundScene";

    /// <summary>지금 지하에 있는가. <see cref="PlayerSave"/> 가 이것을 보고 좌표를 덮어쓰지 않는다.</summary>
    public static bool IsActive { get; private set; }

    /// <summary>들어올 때 쓴 탐지기 등급. 벽 재질과 전리품 범위를 정한다.</summary>
    public static int Tier { get; private set; }

    /// <summary>
    /// 지상으로 돌아갈 자리. <b>세이브에도 이 값이 쓰인다</b> — 지하에서 게임을 끄면
    /// 지하 좌표가 <c>player.dat</c> 에 남아 다음 실행에 허공에서 시작하기 때문이다.
    /// </summary>
    public static Vector2 SurfaceReturnPosition { get; private set; }

    /// <summary>이번 판을 만든 월드. 검증에서 물·전리품 개수를 세는 데도 쓴다.</summary>
    public static UndergroundWorld World { get; private set; }

    /// <summary>
    /// 지하로 내려간다. <b>씬을 로드하기 전에 월드를 갈아 끼운다</b> — <see cref="WorldMap"/> 은 씬을 넘어
    /// 살아남으므로 교체가 그대로 따라오고, 새 씬의 <c>MapGenerator.Start</c> 와 순서를 다투지 않는다.
    /// </summary>
    public static void Enter(int tier, Vector2 surfacePosition)
    {
        if (IsActive) return;
        if (WorldMap.Instance == null) return;

        Tier = Mathf.Max(0, tier);
        SurfaceReturnPosition = surfacePosition;
        World = new UndergroundWorld(Tier, Random.Range(int.MinValue, int.MaxValue));
        IsActive = true;

        WorldMap.Instance.EnterEphemeralWorld(World.Generate);
        SceneManager.LoadScene(UndergroundSceneName);
    }

    /// <summary>지상으로 돌아온다. 지하에서 판 벽도, 남겨 둔 물건도 함께 사라진다(의도).</summary>
    public static void Exit()
    {
        if (!IsActive) return;

        // ⚠ <b>IsActive 를 내리기 전에 플레이어를 저장한다.</b> 두 가지가 여기 걸려 있다:
        //  · 지상 씬에서 PlayerSave.Load 가 이 파일로 인벤토리를 되돌리므로, 여기서 안 쓰면
        //    <b>지하에서 주운 것이 통째로 사라진다</b>(들어가기 직전에 쓴 옛 내용으로 덮인다).
        //  · 아직 IsActive 라서 좌표는 SurfaceReturnPosition 이 쓰인다 — 먼저 내리면 지하 좌표가 남는다.
        PlayerSave save = Object.FindFirstObjectByType<PlayerSave>();
        if (save != null) save.Save();

        IsActive = false;
        World = null;

        if (WorldMap.Instance != null) WorldMap.Instance.ReturnToPersistentWorld();
        SceneManager.LoadScene(SurfaceSceneName);
    }
}
