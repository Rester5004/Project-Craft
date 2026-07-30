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
    [Tooltip("데이터 타일맵(Blocks/Floor)에 찍는 타일. 판정용인 동시에 맨 아래 깔리는 바닥 그림이라, " +
             "벽 스프라이트의 투명한 부분(바깥 모서리 등)으로 이게 비쳐 보인다. " +
             "그래서 벽 블록도 자기 지역의 바닥 타일을 써야 벽과 바닥이 이어져 보인다.")]
    public Tile assetPath;

    [Header("벽 전용")]
    [Tooltip("오토타일링에 쓸 벽 시트. 비우면 이 벽은 텍스처가 그려지지 않는다.")]
    public TileAtlas wallAtlas;

    [Header("바닥 전용")]
    [Tooltip("바닥에 까는 그림 한 장. 바닥은 오토타일링하지 않는다.")]
    public Sprite floorSprite;
}
