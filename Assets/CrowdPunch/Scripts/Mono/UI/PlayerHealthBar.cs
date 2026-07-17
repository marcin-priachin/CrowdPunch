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

        public void Bind(PlayerHealth health)
        {
            Bind(health, fillImage);
        }

        public void Bind(PlayerHealth health, Image fill)
        {
            if (playerHealth != null)
            {
                playerHealth.Changed -= UpdateFill;
            }

            playerHealth = health;
            fillImage = fill;

            if (isActiveAndEnabled && playerHealth != null)
            {
                playerHealth.Changed += UpdateFill;
                UpdateFill(playerHealth.CurrentHealth, playerHealth.MaxHealth);
            }
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
            if (playerHealth != null)
            {
                playerHealth.Changed += UpdateFill;
                UpdateFill(playerHealth.CurrentHealth, playerHealth.MaxHealth);
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.Changed -= UpdateFill;
            }
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
