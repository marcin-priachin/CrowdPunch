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
        [SerializeField] private PlayerPunch playerPunch;
        [SerializeField] private GameObject gameCanvasPrefab;
        [SerializeField] private bool createGameCanvas = true;
        [SerializeField] private bool showEnemyHealthBars = true;
        [SerializeField] private bool showEnemyStates = true;

        private void Awake()
        {
            if (playerBridge == null)
            {
                playerBridge = FindFirstObjectByType<PlayerEcsBridge>();
            }

            EnsurePlayerController();
            EnsurePlayerPunch();
            EnsurePlayerHealth();
            EnsurePlayerInvincibilityFeedback();
            EnsurePunchTrajectoryPreview();
            EnsureExplosionFeedback();
            EnsureGameCanvas();
            EnsureEnemyHealthBarCanvas();
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
            EnsurePlayerPunch();
            EnsurePlayerHealth();

            playerController?.ResetPlayerState();
            playerPunch?.ResetPunchState();
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

        private void EnsurePlayerPunch()
        {
            if (playerPunch != null || playerBridge == null)
            {
                return;
            }

            playerPunch = playerBridge.GetComponent<PlayerPunch>();
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

        private void EnsurePlayerInvincibilityFeedback()
        {
            if (playerBridge != null && playerBridge.GetComponent<PlayerInvincibilityFeedback>() == null)
            {
                playerBridge.gameObject.AddComponent<PlayerInvincibilityFeedback>();
            }
        }

        private void EnsurePunchTrajectoryPreview()
        {
            if (playerBridge != null && playerBridge.GetComponent<PunchTrajectoryPreview>() == null)
            {
                playerBridge.gameObject.AddComponent<PunchTrajectoryPreview>();
            }
        }

        private void EnsureExplosionFeedback()
        {
            if (playerBridge != null && playerBridge.GetComponent<ExplosionFeedback>() == null)
            {
                playerBridge.gameObject.AddComponent<ExplosionFeedback>();
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

        private void EnsureEnemyHealthBarCanvas()
        {
            EnemyHealthBarCanvas enemyCanvas = FindFirstObjectByType<EnemyHealthBarCanvas>();
            if (enemyCanvas != null)
            {
                enemyCanvas.Configure(showEnemyHealthBars, showEnemyStates);
                return;
            }

            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                enemyCanvas = canvas.gameObject.AddComponent<EnemyHealthBarCanvas>();
                enemyCanvas.Configure(showEnemyHealthBars, showEnemyStates);
            }
        }
    }
}
