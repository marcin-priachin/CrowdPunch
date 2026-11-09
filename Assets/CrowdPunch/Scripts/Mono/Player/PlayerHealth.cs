using System;
using CrowdPunch.Configuration;
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
        private GameRuntimeSettings gameSettings;
        private float invincibilityRemainingSeconds;

        public event Action<float, float> Changed;
        public event Action<Vector3> DamageAccepted;

        public float CurrentHealth => currentHealth;

        public float MaxHealth => maxHealth;

        public float Normalized => maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);

        private bool RuntimeInvincibility => FeedbackTimeController.IsMenuPreview || FeedbackTimeController.IsLevelClear ||
            (gameSettings != null && gameSettings.PlayerInvincible);

        public bool IsInvincible => RuntimeInvincibility || invincibilityRemainingSeconds > 0f;

        public void Configure(GameRuntimeSettings settings)
        {
            gameSettings = settings;
        }

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
                ecsBridge.WizardHitReceived += ApplyWizardHit;
                ecsBridge.TrailDamageReceived += ApplyTrailDamage;
                ecsBridge.LinkedPairDamageReceived += ApplyTrailDamage;
                ecsBridge.DragonFlameReceived += ApplyWizardHit;
                ecsBridge.GroundHazardDamageReceived += ApplyTrailDamage;
            }
        }

        private void OnDisable()
        {
            if (ecsBridge != null)
            {
                ecsBridge.EnemyContactHitReceived -= TryApplyEnemyContactHit;
                ecsBridge.WizardHitReceived -= ApplyWizardHit;
                ecsBridge.TrailDamageReceived -= ApplyTrailDamage;
                ecsBridge.LinkedPairDamageReceived -= ApplyTrailDamage;
                ecsBridge.DragonFlameReceived -= ApplyWizardHit;
                ecsBridge.GroundHazardDamageReceived -= ApplyTrailDamage;
            }
        }

        private void Update()
        {
            invincibilityRemainingSeconds = Mathf.Max(0f, invincibilityRemainingSeconds - Time.deltaTime);
            Publish();
        }

        public void ApplyDamage(float amount)
        {
            if (RuntimeInvincibility) return;
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
            if (IsInvincible || currentHealth <= 0f)
            {
                return;
            }

            ApplyDamage(maxHealth * Mathf.Clamp01(damagePercent));
            invincibilityRemainingSeconds = Mathf.Max(0f, invincibilitySeconds);
            DamageAccepted?.Invoke(pushImpulse);
        }

        private void ApplyWizardHit(float amount, Vector3 impulse)
        {
            if (RuntimeInvincibility || currentHealth <= 0) return;
            ApplyDamage(amount);
            DamageAccepted?.Invoke(impulse);
        }

        private void ApplyTrailDamage(float amount)
        {
            if (RuntimeInvincibility || currentHealth <= 0 || amount <= 0) return;
            ApplyDamage(amount);
            DamageAccepted?.Invoke(Vector3.zero);
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
