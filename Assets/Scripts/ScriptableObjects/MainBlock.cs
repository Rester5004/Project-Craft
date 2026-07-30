using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 지형 타일 블록(벽/바닥). blockName 의 "wall:" / "floor:" 접두사는 Chunk.IsWall/IsFloor 가
/// 검사하는 로직이므로 표시용으로 바꾸지 말고 displayName 을 쓴다.
///
/// 파일명 = 클래스명을 유지해야 에셋의 m_Script 참조가 잡힌다
/// (Blocks.cs 안에 두면 MonoScript 가 BlockBase 로 잡혀 에셋이 로드되지 않는다).
/// </summary>
[CreateAssetMenu(fileName = "MainBlock", menuName = "Blocks/MainBlock")]
public class MainBlock : BlockBase
{
    public Tile assetPath;
}
