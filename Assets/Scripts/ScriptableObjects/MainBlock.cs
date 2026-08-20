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

    [Tooltip("이 바닥을 걸을 때 나는 소리. <b>여러 걸음이 든 루프 클립</b>이라 걷는 동안 반복 재생된다 " +
             "(PlayerFootsteps). 비어 있으면 그 바닥은 조용하다 — 물웅덩이가 지금 그렇다. " +
             "지형 소리의 정본은 이 필드 하나이고, 조회는 WorldMap.FootstepAt 이 한다.")]
    public AudioClip footstepSound;

    [Tooltip("이 바닥이 유체 웅덩이면 그 유체. 비어 있으면 평범한 바닥이다. " +
             "빈 그릇을 들고 우클릭하면 여기서 퍼진다(PlayerInteraction.TryFillContainer) — " +
             "퍼도 타일은 줄지 않는 무한 원천이다.")]
    public FluidDefine fluid;
}
