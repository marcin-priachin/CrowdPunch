using UnityEngine;
using UnityEngine.Serialization;

namespace CrowdPunch.Configuration
{
    public enum EnemyArchetype
    {
        Baseline,
        Ranged,
        Explosive
    }

    /// <summary>Reusable tuning for the initial ECS crowd.</summary>
    [CreateAssetMenu(fileName = "EnemySpawnSettings", menuName = "Crowd Punch/Enemy Spawn Settings")]
    public sealed class EnemySpawnSettings : ScriptableObject
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private GameObject rangedProjectilePrefab;
        [SerializeField] private EnemyArchetype archetype;
        [SerializeField, Min(0)] private int initialCount = 250;
        [SerializeField, Min(0f)] private float radius = 20f;
        [SerializeField] private bool respawnEnabled = true;

        [Header("Ranged positioning (provisional)")]
        [SerializeField, Min(0f)] private float preferredMinimumDistance = 8f;
        [SerializeField, Min(0f)] private float preferredMaximumDistance = 12f;
        [SerializeField, Min(0f)] private float engagementRange = 18f;
        [SerializeField, Min(0f)] private float retreatSpeed = 4f;
        [SerializeField, Min(0f)] private float approachSpeed = 3f;

        [Header("Ranged attack (provisional)")]
        [SerializeField, Min(0f)] private float initialAttackDelay = 1.5f;
        [SerializeField, Min(0f)] private float initialDelayVariation = 1.5f;
        [SerializeField, Min(0f)] private float windUpDuration = 0.75f;
        [SerializeField, Min(0f)] private float cooldown = 3f;
        [SerializeField, Min(0f)] private float cooldownVariation = 1.5f;
        [SerializeField, Min(0f)] private float projectileDamage = 10f;
        [SerializeField, Min(0f)] private float playerInvincibilitySeconds = 0.5f;

        [Header("Ranged projectile (provisional)")]
        [FormerlySerializedAs("projectileTravelDuration")]
        [SerializeField, Min(0.01f)] private float projectileSpeed = 8f;
        [SerializeField, Min(0f)] private float projectileAimSpreadRadius = 2.5f;
        [SerializeField] private float projectileAimTargetYOffset;
        [SerializeField, Min(0f)] private float projectileArcHeight = 5f;
        [SerializeField] private float projectileMinimumAltitude = -2f;
        [SerializeField, Min(0.01f)] private float projectileLifetime = 5f;
        [SerializeField, Min(0.01f)] private float projectileRadius = 0.4f;
        [SerializeField] private LayerMask projectilePlayerLayers = ~0;

        [Header("Explosion (provisional)")]
        [SerializeField, Min(0f)] private float explosionRadius = 5f;
        [SerializeField, Min(0f)] private float explosionDamage = 20f;
        [SerializeField, Min(0f)] private float normalEnemyKnockbackForce = 16f;
        [SerializeField, Min(0f)] private float playerEliteKnockbackForce = 10f;
        [SerializeField, Min(0f)] private float bossKnockbackForce = 5f;
        [SerializeField, Min(0f)] private float explosionPlayerInvincibilitySeconds = 0.5f;
        [SerializeField, Min(0.01f)] private float explosionVisualDuration = 0.35f;
        [SerializeField, Min(0f)] private float explosionVisualSizeMultiplier = 1f;

        public GameObject EnemyPrefab => enemyPrefab;
        public GameObject RangedProjectilePrefab => rangedProjectilePrefab;
        public EnemyArchetype Archetype => archetype;
        public int InitialCount => initialCount;
        public float Radius => radius;
        public bool RespawnEnabled => respawnEnabled;
        public float PreferredMinimumDistance => preferredMinimumDistance;
        public float PreferredMaximumDistance => preferredMaximumDistance;
        public float EngagementRange => engagementRange;
        public float RetreatSpeed => retreatSpeed;
        public float ApproachSpeed => approachSpeed;
        public float InitialAttackDelay => initialAttackDelay;
        public float InitialDelayVariation => initialDelayVariation;
        public float WindUpDuration => windUpDuration;
        public float Cooldown => cooldown;
        public float CooldownVariation => cooldownVariation;
        public float ProjectileDamage => projectileDamage;
        public float PlayerInvincibilitySeconds => playerInvincibilitySeconds;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileAimSpreadRadius => projectileAimSpreadRadius;
        public float ProjectileAimTargetYOffset => projectileAimTargetYOffset;
        public float ProjectileArcHeight => projectileArcHeight;
        public float ProjectileMinimumAltitude => projectileMinimumAltitude;
        public float ProjectileLifetime => projectileLifetime;
        public float ProjectileRadius => projectileRadius;
        public uint ProjectilePlayerLayers => unchecked((uint)projectilePlayerLayers.value);
        public float ExplosionRadius => explosionRadius;
        public float ExplosionDamage => explosionDamage;
        public float NormalEnemyKnockbackForce => normalEnemyKnockbackForce;
        public float PlayerEliteKnockbackForce => playerEliteKnockbackForce;
        public float BossKnockbackForce => bossKnockbackForce;
        public float ExplosionPlayerInvincibilitySeconds => explosionPlayerInvincibilitySeconds;
        public float ExplosionVisualDuration => explosionVisualDuration;
        public float ExplosionVisualSizeMultiplier => explosionVisualSizeMultiplier;
    }
}
