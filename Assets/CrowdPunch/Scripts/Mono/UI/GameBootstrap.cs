using CrowdPunch.Mono.Player;
using UnityEngine;

namespace CrowdPunch.Mono.UI
{
    /// <summary>
    /// Scene-level MonoBehaviour bootstrap for hybrid player-to-ECS bridge wiring.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private PlayerEcsBridge playerBridge;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private GameObject gameCanvasPrefab;
        [SerializeField] private bool createGameCanvas = true;

        private void Awake()
        {
            if (playerBridge == null)
            {
                playerBridge = FindFirstObjectByType<PlayerEcsBridge>();
            }

            EnsurePlayerHealth();
            EnsureGameCanvas();
            BindPlayerHealthBars();

            PlayerBridgeRegistry.Register(playerBridge);
        }

        private void OnDestroy()
        {
            PlayerBridgeRegistry.Unregister(playerBridge);
        }

        private void EnsurePlayerHealth()
        {
            if (playerHealth != null || playerBridge == null)
            {
                return;
            }

            playerHealth = playerBridge.GetComponent<PlayerHealth>();
            if (playerHealth == null)
            {
                playerHealth = playerBridge.gameObject.AddComponent<PlayerHealth>();
            }
        }

        private void EnsureGameCanvas()
        {
            if (!createGameCanvas || FindFirstObjectByType<PlayerHealthBar>() != null)
            {
                return;
            }

            GameObject prefab = gameCanvasPrefab != null
                ? gameCanvasPrefab
                : Resources.Load<GameObject>("GameCanvas");

            if (prefab != null)
            {
                Instantiate(prefab);
            }
        }

        private void BindPlayerHealthBars()
        {
            if (playerHealth == null)
            {
                return;
            }

            foreach (PlayerHealthBar healthBar in FindObjectsByType<PlayerHealthBar>(FindObjectsSortMode.None))
            {
                healthBar.Bind(playerHealth);
            }
        }
    }
}
