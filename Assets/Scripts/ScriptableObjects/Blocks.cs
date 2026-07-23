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
[CreateAssetMenu(fileName = "MachineBlock", menuName = "Blocks/MachineBlock")]
public class MachineBlock : BlockBase
{
    public GameObject machinePrefab;
}
