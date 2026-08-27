using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    /// <summary>Provides white-box visibility for the player's temporary invulnerability state.</summary>
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerInvincibilityFeedback : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Renderer playerRenderer;
        [SerializeField, Min(0.02f)] private float blinkIntervalSeconds = 0.1f;

        private bool initialRendererEnabled;
        private bool wasInvincible;
        private float blinkElapsedSeconds;

        private void Reset()
        {
            playerHealth = GetComponent<PlayerHealth>();
            playerRenderer = GetComponent<Renderer>();
        }

        private void Awake()
        {
            if (playerHealth == null)
            {
                playerHealth = GetComponent<PlayerHealth>();
            }

            if (playerRenderer == null)
            {
                playerRenderer = GetComponent<Renderer>();
            }

            if (playerRenderer != null)
            {
                initialRendererEnabled = playerRenderer.enabled;
            }
        }

        private void Update()
        {
            bool isInvincible = playerHealth != null && playerHealth.IsInvincible;
            if (!isInvincible)
            {
                if (wasInvincible)
                {
                    RestoreRenderer();
                }

                wasInvincible = false;
                return;
            }

            if (!wasInvincible)
            {
                blinkElapsedSeconds = 0f;
            }

            wasInvincible = true;
            if (playerRenderer == null)
            {
                return;
            }

            float interval = Mathf.Max(0.02f, blinkIntervalSeconds);
            playerRenderer.enabled = Mathf.FloorToInt(blinkElapsedSeconds / interval) % 2 != 0;
            blinkElapsedSeconds += Time.deltaTime;
        }

        private void OnDisable()
        {
            RestoreRenderer();
            wasInvincible = false;
            blinkElapsedSeconds = 0f;
        }

        private void RestoreRenderer()
        {
            if (playerRenderer != null)
            {
                playerRenderer.enabled = initialRendererEnabled;
            }
        }
    }
}
