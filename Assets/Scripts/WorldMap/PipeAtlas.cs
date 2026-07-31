using UnityEngine;

/// <summary>
/// 파이프 한 종류의 그림 묶음. 4방향 연결 마스크(N=1, E=2, S=4, W=8)로 <b>직접</b> 색인한다.
///
/// 시트에 없는 세 칸(N만 · S만 · 연결없음)은 있는 그림을 돌리거나 대체해 채우므로
/// 16칸이 항상 다 차 있다 — 런타임에 "그림 없음" 분기를 둘 필요가 없다.
///
/// 손으로 채우지 않는다. Tools/Tiles/Build Pipe Atlas 가 시트에서 뽑아 넣는다.
/// </summary>
[CreateAssetMenu(fileName = "PipeAtlas", menuName = "Tiles/Pipe Atlas")]
public class PipeAtlas : ScriptableObject
{
    [Tooltip("연결 마스크(0~15)로 색인하는 그림 16장.")]
    public Sprite[] sprites = new Sprite[16];

    [Tooltip("그 칸의 그림을 몇 도 돌려 쓸지(0 / 90 / -90). 시트에 없는 N만·S만을 채우는 데 쓴다.")]
    public int[] rotations = new int[16];

    /// <summary>마스크에 해당하는 그림(범위를 벗어나면 null).</summary>
    public Sprite SpriteFor(int mask)
        => sprites != null && mask >= 0 && mask < sprites.Length ? sprites[mask] : null;

    /// <summary>마스크에 해당하는 회전 각도(도).</summary>
    public int RotationFor(int mask)
        => rotations != null && mask >= 0 && mask < rotations.Length ? rotations[mask] : 0;
}
