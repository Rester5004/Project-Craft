using UnityEngine;

/// <summary>
/// 도구 부품의 종류(막대 · 망치 머리 · 곡괭이 머리 …).
/// 조합대의 부품 칸은 "이 종류인가"만 보고 재질은 가리지 않으므로,
/// 레시피가 "막대 1개" 처럼 종류 단위로 재료를 요구할 수 있다.
/// </summary>
[CreateAssetMenu(fileName = "ToolPartKind", menuName = "Tools/Tool Part Kind")]
public class ToolPartKind : ScriptableObject
{
    [Tooltip("내부 ID(영문 소문자). 예: rod, hammer_head, pickaxe_head")]
    public string kindId;

    [Tooltip("화면에 표시할 이름(한글). 예: 막대, 곡괭이 머리")]
    public string displayName;

    [Tooltip("부품 칸이 비었을 때 보여 줄 안내용 아이콘(선택).")]
    public Sprite icon;

    /// <summary>표시에 쓸 이름(displayName 이 비면 kindId, 그것도 비면 에셋 이름).</summary>
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(displayName)) return displayName;
            return string.IsNullOrEmpty(kindId) ? name : kindId;
        }
    }
}
