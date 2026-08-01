using UnityEngine;
using UnityEngine.SceneManagement;

public class PrototypeMapTransitions : MonoBehaviour
{
    private const string UndergroundSceneName = "UndergroundScene";
    private const float UseDistance = 1.5f;
    private static readonly Vector2 EntrancePosition = new(2f, 0f);
    private static bool initialized;

    private void OnDestroy() => initialized = false;

    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;

        GameObject portal = new("Surface To Underground Portal");
        portal.transform.position = EntrancePosition;
        portal.transform.localScale = Vector3.one * 0.65f;
        portal.AddComponent<PrototypeMapTransitions>();

        SpriteRenderer renderer = portal.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        renderer.color = new Color(0.2f, 0.85f, 1f);
        renderer.sortingOrder = 10;
    }

    public static bool TryUseNearest(Transform player)
    {
        Initialize();
        if (((Vector2)player.position - EntrancePosition).sqrMagnitude > UseDistance * UseDistance)
            return false;

        SceneManager.LoadScene(UndergroundSceneName);
        return true;
    }
}
