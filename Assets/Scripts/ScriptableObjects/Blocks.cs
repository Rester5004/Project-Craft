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

    [Header("Machine UI 설정")]
    public int inputSlotCount = 3;
    public int outputSlotCount = 6;
    public int gasSlotCount = 0;
    public float maxGasAmountForSlot1 = 0f;
    public float maxGasAmountForSlot2 = 0f;
    public float maxEnergyAmount = 0f;
    public bool isUseEnergy = false;
}
