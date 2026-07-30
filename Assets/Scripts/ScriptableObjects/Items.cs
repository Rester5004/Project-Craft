using UnityEngine;

[CreateAssetMenu(fileName = "Item", menuName = "Items/Items")]
public class Items : ScriptableObject
{
    [Tooltip("내부 ID. 세이브 파일의 키이므로 바꾸면 기존 세이브가 깨진다. 영어로 유지할 것.")]
    public string itemName;

    [Tooltip("화면에 표시할 이름(한글). 비우면 itemName 을 그대로 쓴다.")]
    public string displayName;

    public bool placeable;
    public Sprite Icon;
    public int maxStack;

    [Tooltip("연료로 태웠을 때 나오는 에너지. 0 이면 연료가 아니다. (갈탄 200 / 석탄 400 / 수소 1000)")]
    [Min(0f)] public float burnEnergy;

    /// <summary>화로·발전기의 연료로 쓸 수 있는가.</summary>
    public bool IsFuel => burnEnergy > 0f;

    /// <summary>UI 표시용 이름. displayName 이 비어 있으면 ID 로 폴백한다.</summary>
    public string DisplayName => string.IsNullOrEmpty(displayName) ? itemName : displayName;
}
