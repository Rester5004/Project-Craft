using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach this to the return block in the underground scene.
/// Press E while close to return to the configured surface scene.
/// </summary>
public class ReturnToSurfacePortal : MonoBehaviour
{
    [SerializeField] private string surfaceSceneName = "MapTest";
    [SerializeField] private Transform player;
    [SerializeField, Min(0.1f)] private float useDistance = 1.5f;

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
            return;

        if (player == null)
        {
            PlayerForTest playerController = FindFirstObjectByType<PlayerForTest>();
            if (playerController == null)
                return;

            player = playerController.transform;
        }

        float distanceSquared = ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude;
        if (distanceSquared <= useDistance * useDistance)
            SceneManager.LoadScene(surfaceSceneName);
    }
}
