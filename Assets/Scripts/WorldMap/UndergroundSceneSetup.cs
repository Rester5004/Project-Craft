using UnityEngine;

/// <summary>
/// Temporary playable setup for the underground scene.
/// It creates a player and a return block until the actual underground map is built.
/// </summary>
public class UndergroundSceneSetup : MonoBehaviour
{
    [SerializeField] private Vector2 playerSpawnPosition = new(-2f, 0f);
    [SerializeField] private Vector2 returnPortalPosition = new(2f, 0f);

    private void Start()
    {
        if (FindFirstObjectByType<PlayerForTest>() == null)
            CreatePlayer();

        if (FindFirstObjectByType<ReturnToSurfacePortal>() == null)
            CreateReturnPortal();
    }

    private void CreatePlayer()
    {
        GameObject player = CreateVisibleObject("Underground Player", playerSpawnPosition, new Color(0.9f, 0.9f, 1f), 0.55f);
        player.AddComponent<Rigidbody2D>();
        player.AddComponent<PlayerForTest>();
    }

    private void CreateReturnPortal()
    {
        GameObject portal = CreateVisibleObject("Return To Surface Block", returnPortalPosition, new Color(1f, 0.75f, 0.2f), 0.7f);
        portal.AddComponent<ReturnToSurfacePortal>();
    }

    private static GameObject CreateVisibleObject(string objectName, Vector2 position, Color color, float size)
    {
        GameObject gameObject = new(objectName);
        gameObject.transform.position = position;
        gameObject.transform.localScale = Vector3.one * size;

        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        renderer.color = color;
        return gameObject;
    }
}
