using CrowdPunch.Mono.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CrowdPunch.Mono.UI
{
    /// <summary>
    /// Scene UI health bar for the GameObject-owned player.
    /// </summary>
    public sealed class PlayerHealthBar : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Image fillImage;

        private bool isSubscribed;

        public void Bind(PlayerHealth health)
        {
            Bind(health, fillImage);
        }

        public void Bind(PlayerHealth health, Image fill)
        {
            Unsubscribe();

            playerHealth = health;
            fillImage = fill;

            Subscribe();
            Refresh();
        }

        private void Awake()
        {
            if (playerHealth == null)
            {
                playerHealth = FindFirstObjectByType<PlayerHealth>();
            }
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (isSubscribed)
            {
                return;
            }

            if (playerHealth != null)
            {
                playerHealth.Changed += UpdateFill;
                isSubscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (!isSubscribed)
            {
                return;
            }

            if (playerHealth != null)
            {
                playerHealth.Changed -= UpdateFill;
            }

            isSubscribed = false;
        }

        private void Refresh()
        {
            if (playerHealth == null)
            {
                return;
            }

            UpdateFill(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }

        private void UpdateFill(float current, float max)
        {
            if (fillImage == null)
            {
                return;
            }

            fillImage.fillAmount = max <= 0f ? 0f : Mathf.Clamp01(current / max);
        }
    }
}
