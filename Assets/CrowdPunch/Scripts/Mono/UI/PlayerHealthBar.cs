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

        internal void StyleRingside()
        {
            if (fillImage == null || transform is not RectTransform track) return;
            track.sizeDelta = new Vector2(360, 14);
            track.anchoredPosition = new Vector2(80, -53);
            if (TryGetComponent<Image>(out var background))
                background.color = new Color32(16, 19, 56, 175);
            CelComicUi.Stretch(fillImage.rectTransform);
            fillImage.color = new Color32(120, 220, 96, 255);
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
