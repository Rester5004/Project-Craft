using System.IO;
using UnityEngine;

/// <summary>
/// 세이브 파일을 <b>잃지 않게</b> 읽고 쓰는 공통 규약. WorldMap 과 PlayerSave 가 함께 쓴다.
///
/// 왜 필요한가 — 예전에는 두 가지가 겹쳐 월드가 통째로 사라질 수 있었다.
///   ① <c>File.Open(path, FileMode.Create)</c> 는 <b>쓰기 전에 먼저 파일을 비운다.</b>
///      기록 도중 프로세스가 죽으면(Alt+F4·강제 종료·디스크 가득) 앞부분만 남은 잘린 파일이 된다.
///      매직·버전·개수는 멀쩡히 읽혀 검증을 통과하므로, 청크 루프 중간에서야 터진다.
///   ② 그 예외를 받은 쪽이 <c>File.Delete</c> 로 세이브를 지웠다. 복구 수단이 없었다.
///
/// 그래서 <see cref="WriteAtomic"/> 는 <b>임시 파일에 다 쓴 뒤에야</b> 진짜 파일과 바꾸고,
/// <see cref="Quarantine"/> 는 읽을 수 없는 파일을 <b>지우는 대신 옆으로 치운다</b>.
/// 쓰기가 중간에 실패해도 <b>기존 파일은 손대지 않은 채로 남는다</b>.
/// </summary>
public static class SafeFile
{
    /// <summary>임시 파일에 먼저 쓰고, 성공했을 때만 원본과 교체한다. 실패하면 원본이 그대로 남는다.</summary>
    /// <returns>실제로 교체까지 끝났으면 true.</returns>
    public static bool WriteAtomic(string path, System.Action<BinaryWriter> body)
    {
        if (string.IsNullOrEmpty(path) || body == null) return false;

        string temp = path + ".tmp";
        try
        {
            using (BinaryWriter writer = new BinaryWriter(File.Open(temp, FileMode.Create)))
                body(writer);
        }
        catch (System.Exception e)
        {
            // 여기서 실패해도 원본은 아직 손대지 않았다 — 그것이 이 방식의 요점이다.
            Debug.LogError($"[SafeFile] '{path}' 기록 실패, 기존 파일을 그대로 둡니다: {e.Message}");
            TryDelete(temp);
            return false;
        }

        try
        {
            // File.Replace 는 대상이 있어야 한다. 첫 저장이면 그냥 옮긴다.
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SafeFile] '{path}' 교체 실패, 기존 파일을 그대로 둡니다: {e.Message}");
            TryDelete(temp);
            return false;
        }
    }

    /// <summary>
    /// 읽을 수 없는 파일을 <c>.corrupt</c> 로 옮긴다. <b>지우지 않는다</b> —
    /// 포맷 오류인지 일시적 IO 오류인지 구분할 수 없으므로, 원본이 남아 있어야 나중에 살릴 수 있다.
    /// </summary>
    public static void Quarantine(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        try
        {
            string dead = path + ".corrupt";
            if (File.Exists(dead)) File.Delete(dead);   // 직전 것 하나만 남긴다
            File.Move(path, dead);
            Debug.LogWarning($"[SafeFile] 읽을 수 없는 파일을 '{dead}' 로 옮겼습니다(지우지 않았습니다).");
        }
        catch (System.Exception e)
        {
            // 격리조차 못 해도 게임은 계속 떠야 한다. 다음 저장이 이 파일을 덮어쓴다.
            Debug.LogError($"[SafeFile] '{path}' 격리 실패: {e.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (System.Exception e) { Debug.LogWarning($"[SafeFile] 임시 파일 '{path}' 정리 실패: {e.Message}"); }
    }
}
