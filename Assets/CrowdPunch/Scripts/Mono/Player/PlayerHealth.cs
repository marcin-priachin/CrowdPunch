using System;
using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    /// <summary>
    /// Traditional GameObject-owned player health state.
    /// </summary>
    [RequireComponent(typeof(PlayerEcsBridge))]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private PlayerEcsBridge ecsBridge;
        [SerializeField] private float maxHealth = 100f;

        private float currentHealth;
        private float invincibilityRemainingSeconds;

        public event Action<float, float> Changed;
        public event Action<Vector3> DamageAccepted;

        public float CurrentHealth => currentHealth;

        public float MaxHealth => maxHealth;

        public float Normalized => maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);

        public bool IsInvincible => invincibilityRemainingSeconds > 0f;

        private void Reset()
        {
            ecsBridge = GetComponent<PlayerEcsBridge>();
        }

        private void Awake()
        {
            EnsureBridge();
            currentHealth = Mathf.Max(0f, maxHealth);
            Publish();
        }

        private void Start()
        {
            Changed?.Invoke(currentHealth, maxHealth);
        }

        private void OnEnable()
        {
            EnsureBridge();

            if (ecsBridge != null)
            {
                ecsBridge.EnemyContactHitReceived += TryApplyEnemyContactHit;
            }
        }

        private void OnDisable()
        {
            if (ecsBridge != null)
            {
                ecsBridge.EnemyContactHitReceived -= TryApplyEnemyContactHit;
            }
        }

        private void Update()
        {
            invincibilityRemainingSeconds = Mathf.Max(0f, invincibilityRemainingSeconds - Time.deltaTime);
            Publish();
        }

        public void ApplyDamage(float amount)
        {
            SetHealth(currentHealth - Mathf.Max(0f, amount));
        }

        public void Restore(float amount)
        {
            SetHealth(currentHealth + Mathf.Max(0f, amount));
        }

        public void ResetHealth()
        {
            invincibilityRemainingSeconds = 0f;
            SetHealth(maxHealth);
        }

        private void TryApplyEnemyContactHit(float damagePercent, float invincibilitySeconds, Vector3 pushImpulse)
        {
            if (invincibilityRemainingSeconds > 0f || currentHealth <= 0f)
            {
                return;
            }

            ApplyDamage(maxHealth * Mathf.Clamp01(damagePercent));
            invincibilityRemainingSeconds = Mathf.Max(0f, invincibilitySeconds);
            DamageAccepted?.Invoke(pushImpulse);
        }

        private void SetHealth(float value)
        {
            bool wasAlive = currentHealth > 0f;
            float clampedHealth = Mathf.Clamp(value, 0f, Mathf.Max(0f, maxHealth));
            if (Mathf.Approximately(currentHealth, clampedHealth))
            {
                return;
            }

            currentHealth = clampedHealth;
            Publish();
            Changed?.Invoke(currentHealth, maxHealth);

            if (wasAlive && currentHealth <= 0f)
            {
                gameObject.SetActive(false);
            }
        }

        private void Publish()
        {
            EnsureBridge();
            ecsBridge?.PublishPlayerHealth(currentHealth, maxHealth);
        }

        private void EnsureBridge()
        {
            if (ecsBridge == null)
            {
                ecsBridge = GetComponent<PlayerEcsBridge>();
            }
        }
    }
}
