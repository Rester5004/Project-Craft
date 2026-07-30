using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이름으로 스프라이트를 찾는 런타임 레지스트리.
/// 도구 그림은 재질에 따라 <c>iron_hammer</c> · <c>gold_pickaxe_head</c> 처럼 이름 규칙으로 정해지는데,
/// 런타임에는 <c>AssetDatabase</c> 를 쓸 수 없고 시트가 <c>Resources</c> 밖에 있어 조회 수단이 없다.
/// 그래서 에디터 툴이 서브 스프라이트를 이 에셋에 모아 두고, 게임은 여기서만 찾는다.
/// </summary>
[CreateAssetMenu(fileName = "ToolSpriteLibrary", menuName = "Tools/Tool Sprite Library")]
public class ToolSpriteLibrary : ScriptableObject
{
    [Tooltip("도구 · 부품 스프라이트. 에디터 툴이 시트에서 자동으로 채운다.")]
    public List<Sprite> sprites = new();

    private Dictionary<string, Sprite> index;

    /// <summary>이름으로 스프라이트를 찾는다(없으면 null).</summary>
    public Sprite Get(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;
        EnsureIndex();
        return index.TryGetValue(spriteName, out Sprite sprite) ? sprite : null;
    }

    /// <summary>에디터에서 목록을 바꾼 뒤 색인을 다시 만들게 한다.</summary>
    public void Invalidate() => index = null;

    private void EnsureIndex()
    {
        if (index != null) return;

        index = new Dictionary<string, Sprite>();
        for (int i = 0; i < sprites.Count; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null) continue;
            if (index.ContainsKey(sprite.name))
            {
                Debug.LogWarning($"[ToolSpriteLibrary] 스프라이트 이름 '{sprite.name}' 이 중복이라 뒤쪽을 무시합니다.", this);
                continue;
            }
            index[sprite.name] = sprite;
        }
    }

    private void OnDisable() => index = null;   // 도메인 리로드 후 죽은 참조를 들고 있지 않게
}
