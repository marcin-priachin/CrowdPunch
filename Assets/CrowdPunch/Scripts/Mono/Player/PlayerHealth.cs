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

        public event Action<float, float> Changed;

        public float CurrentHealth => currentHealth;

        public float MaxHealth => maxHealth;

        public float Normalized => maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);

        private void Reset()
        {
            ecsBridge = GetComponent<PlayerEcsBridge>();
        }

        private void Awake()
        {
            if (ecsBridge == null)
            {
                ecsBridge = GetComponent<PlayerEcsBridge>();
            }

            currentHealth = Mathf.Max(0f, maxHealth);
            Publish();
        }

        private void Start()
        {
            Changed?.Invoke(currentHealth, maxHealth);
        }

        private void Update()
        {
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

        private void SetHealth(float value)
        {
            float clampedHealth = Mathf.Clamp(value, 0f, Mathf.Max(0f, maxHealth));
            if (Mathf.Approximately(currentHealth, clampedHealth))
            {
                return;
            }

            currentHealth = clampedHealth;
            Publish();
            Changed?.Invoke(currentHealth, maxHealth);
        }

        private void Publish()
        {
            ecsBridge.PublishPlayerHealth(currentHealth, maxHealth);
        }
    }
}
