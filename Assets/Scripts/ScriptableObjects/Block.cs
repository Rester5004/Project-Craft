using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 일반 블록. 파일명 = 클래스명을 유지해야 에셋의 m_Script 참조가 잡힌다
/// (Blocks.cs 안에 두면 MonoScript 가 BlockBase 로 잡혀 에셋이 로드되지 않는다).
/// </summary>
[CreateAssetMenu(fileName = "Block", menuName = "Blocks/Block")]
public class Block : BlockBase
{
    public Tile assetPath;
}
