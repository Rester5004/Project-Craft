using UnityEngine;
using UnityEngine.Tilemaps;

public class BlockBase : ScriptableObject
{
    [Tooltip("내부 ID. 세이브의 타일/배치 키이며 wall:·floor: 접두사는 로직이 검사한다. 영어로 유지할 것.")]
    public string blockName;

    [Tooltip("화면에 표시할 이름(한글). 비우면 blockName 을 그대로 쓴다.")]
    public string displayName;

    /// <summary>UI 표시용 이름. displayName 이 비어 있으면 ID 로 폴백한다.</summary>
    public string DisplayName => string.IsNullOrEmpty(displayName) ? blockName : displayName;
}
// 파생 블록은 모두 파일명=클래스명으로 분리되어 있다.
// 한 파일에 여러 ScriptableObject 를 두면 MonoScript 가 첫 클래스에만 잡혀
// 나머지 타입의 에셋은 m_Script 가 비어 로드 자체가 실패한다.
//   MainBlock → MainBlock.cs
//   Block → Block.cs
//   MachineBlock → MachineBlock.cs
//   CraftingTableBlock → CraftingTableBlock.cs
