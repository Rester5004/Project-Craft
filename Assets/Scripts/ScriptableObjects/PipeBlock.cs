using UnityEngine;

/// <summary>
/// 배치할 수 있는 파이프 한 종류.
///
/// <b><see cref="BlockBase"/> 를 직접 상속한다.</b> <see cref="MainBlock"/> 을 상속하면
/// ItemDictionary 가 지형 블록 역인덱스에 넣어 버려 배치가 지형 경로로 새고,
/// WorldMap.Place 가 "wall:" 접두사가 아니라는 이유로 조용히 실패한다(아무 일도 안 일어남).
///
/// 파일명 = 클래스명을 유지해야 에셋의 m_Script 참조가 잡힌다.
/// </summary>
[CreateAssetMenu(fileName = "PipeBlock", menuName = "Blocks/PipeBlock")]
public class PipeBlock : BlockBase
{
    [Header("종류 · 등급")]
    [Tooltip("무엇을 나르는가. 종류가 같아야 이어진다 — 아이템 파이프와 액체 파이프는 맞붙어도 남남이다.")]
    public PipeKind kind = PipeKind.Item;

    [Tooltip("등급. 같은 종류면 등급이 달라도 이어지고, 이동시간은 지나는 칸마다 그 칸의 등급을 따른다.")]
    [Min(0)] public int tier;

    [Header("운반")]
    [Tooltip("이 파이프 한 칸을 지나는 데 드는 시간(초). 경로 전체 시간은 지나는 칸마다 이 값을 더한 것이다.")]
    [Min(0.01f)] public float secondsPerCell = 0.4f;

    [Tooltip("한 번에 싣는 최대 개수. 실은 짐이 도착하기 전에는 다음 짐을 싣지 않는다.")]
    [Min(1)] public int throughput = 8;

    [Header("그림")]
    [Tooltip("연결 마스크로 색인하는 그림 묶음. Tools/Tiles/Build Pipe Atlas 가 만든다.")]
    public PipeAtlas atlas;

    [Tooltip("그림에 곱할 색. 같은 그림으로 등급을 구분할 때 쓴다(흰색이면 원본 그대로).")]
    public Color tint = Color.white;

    /// <summary>지금 실제로 물건을 나르는 종류인가. 유체·기체는 아직 배치·오토타일까지만 지원한다.</summary>
    public bool CarriesItems => kind == PipeKind.Item;
}
