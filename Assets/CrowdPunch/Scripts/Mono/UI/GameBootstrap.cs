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
        [SerializeField] private PlayerController playerController;
        [SerializeField] private GameObject gameCanvasPrefab;
        [SerializeField] private bool createGameCanvas = true;

        private void Awake()
        {
            if (playerBridge == null)
            {
                playerBridge = FindFirstObjectByType<PlayerEcsBridge>();
            }

            EnsurePlayerController();
            EnsurePlayerHealth();
            EnsurePunchTrajectoryPreview();
            EnsureGameCanvas();
            BindPlayerHealthBars();

            PlayerBridgeRegistry.Register(playerBridge);
        }

        private void OnDestroy()
        {
            PlayerBridgeRegistry.Unregister(playerBridge);
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            ReactivatePlayerObject();
            EnsurePlayerController();
            EnsurePlayerHealth();

            playerController?.ResetPlayerState();
            playerHealth?.ResetHealth();
            GameRestartRegistry.RequestRestart();
            BindPlayerHealthBars();
        }

        private void ReactivatePlayerObject()
        {
            GameObject playerObject = null;

            if (playerBridge != null)
            {
                playerObject = playerBridge.gameObject;
            }
            else if (playerHealth != null)
            {
                playerObject = playerHealth.gameObject;
            }
            else if (playerController != null)
            {
                playerObject = playerController.gameObject;
            }

            if (playerObject != null && !playerObject.activeSelf)
            {
                playerObject.SetActive(true);
            }
        }

        private void EnsurePlayerController()
        {
            if (playerController != null || playerBridge == null)
            {
                return;
            }

            playerController = playerBridge.GetComponent<PlayerController>();
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

        private void EnsurePunchTrajectoryPreview()
        {
            if (playerBridge != null && playerBridge.GetComponent<PunchTrajectoryPreview>() == null)
            {
                playerBridge.gameObject.AddComponent<PunchTrajectoryPreview>();
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
