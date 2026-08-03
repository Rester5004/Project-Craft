using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class UndergroundSceneSetup : MonoBehaviour
{
    private void Start()
    {
        CreateObject("Underground Player", new Vector2(-2f, 0f), new Color(0.9f, 0.9f, 1f), 0.55f)
            .AddComponent<UndergroundScenePlayer>();
        CreateObject("Return To Surface Block", new Vector2(2f, 0f), new Color(1f, 0.75f, 0.2f), 0.7f);
    }

    private static GameObject CreateObject(string objectName, Vector2 position, Color color, float size)
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

public class UndergroundScenePlayer : MonoBehaviour
{
    private const float Speed = 5f;
    private const float ReturnDistance = 1.5f;
    private static readonly Vector2 ReturnPosition = new(2f, 0f);

    private void Update()
    {
        if (Keyboard.current == null) return;

        Vector2 movement = new(
            (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f),
            (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f));
        transform.position += (Vector3)(movement.normalized * Speed * Time.deltaTime);

        if (Keyboard.current.eKey.wasPressedThisFrame
            && ((Vector2)transform.position - ReturnPosition).sqrMagnitude <= ReturnDistance * ReturnDistance)
            SceneManager.LoadScene("MapTest");
    }
}
