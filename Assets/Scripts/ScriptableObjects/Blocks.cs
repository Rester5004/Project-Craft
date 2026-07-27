using UnityEngine;
using UnityEngine.Tilemaps;

public class BlockBase : ScriptableObject
{
    public string blockName;
}
[CreateAssetMenu(fileName = "MainBlock", menuName = "Blocks/MainBlock")]
public class MainBlock : BlockBase
{
    public Tile assetPath;
}
[CreateAssetMenu(fileName = "Block", menuName = "Blocks/Block")]
public class Block : BlockBase
{
    public Tile assetPath;
}
// MachineBlock 은 MachineBlock.cs 로 분리됨(에셋의 m_Script 참조가 잡히도록 파일명=클래스명 유지).
