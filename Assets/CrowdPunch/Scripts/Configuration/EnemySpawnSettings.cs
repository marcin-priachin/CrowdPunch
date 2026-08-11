using CrowdPunch.Components;
using UnityEngine;
using UnityEngine.Serialization;

namespace CrowdPunch.Configuration
{
    public enum EnemyArchetype
    {
        Baseline,
        Ranged,
        Explosive,
        Dasher
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

        [Header("Dasher (provisional)")]
        [SerializeField, Min(0f)] private float dasherPreferredMinimumDistance = 6f;
        [SerializeField, Min(0f)] private float dasherPreferredMaximumDistance = 10f;
        [SerializeField, Min(0f)] private float dasherPreparationMinimumDistance = 5f;
        [SerializeField, Min(0f)] private float dasherPreparationMaximumDistance = 12f;
        [SerializeField, Min(0f)] private float dasherApproachSpeed = 3f;
        [SerializeField, Min(0f)] private float dasherRetreatSpeed = 4f;
        [SerializeField] private DasherPreparationMovement dasherPreparationMovement = DasherPreparationMovement.ImmediateStop;
        [SerializeField, Min(0f)] private float dasherTelegraphDuration = 0.5f;
        [SerializeField, Min(0f)] private float dasherDashSpeed = 18f;
        [SerializeField, Min(0f)] private float dasherMaximumDistance = 12f;
        [SerializeField, Min(0f)] private float dasherRecoveryDuration = 1.25f;
        [SerializeField] private DasherAvoidancePolicy dasherAvoidancePolicy = DasherAvoidancePolicy.BetweenDasherAndPlayer;
        [SerializeField, Min(0f)] private float dasherCorridorWidth = 1.5f;
        [SerializeField, Min(0f)] private float dasherBehindPlayerDistance = 4f;
        [SerializeField, Min(0f)] private float dasherPlayerDamage = 20f;
        [SerializeField, Min(0f)] private float dasherPlayerKnockback = 18f;
        [SerializeField, Min(0f)] private float dasherPlayerInvincibilitySeconds = 0.5f;
        [SerializeField, Min(0f)] private float dasherDashEnemyDamage = 15f;
        [SerializeField, Min(0f)] private float dasherDashEnemyKnockback = 14f;
        [SerializeField, Range(0f, 1f)] private float dasherDashImpactPositionWeight = 0.35f;
        [SerializeField, Min(0f)] private float dasherLaunchedEnemyDamage = 25f;
        [SerializeField, Min(0f)] private float dasherLaunchedEnemyKnockback = 18f;
        [SerializeField, Range(0f, 1f)] private float dasherLaunchedImpactPositionWeight = 0.35f;
        [SerializeField, Min(0f)] private float dasherEliteDamage = 15f;
        [SerializeField, Min(0f)] private float dasherEliteKnockback = 8f;
        [SerializeField, Min(0f)] private float dasherBossDamage = 5f;
        [SerializeField, Min(0f)] private float dasherBossKnockback = 2f;
        [SerializeField] private bool dasherPreserveMomentumAgainstElites;
        [SerializeField] private bool dasherPreserveMomentumAgainstBosses;

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
        public DasherSettings DasherSettings => new DasherSettings
        {
            PreferredMinimumDistance = dasherPreferredMinimumDistance, PreferredMaximumDistance = dasherPreferredMaximumDistance,
            PreparationMinimumDistance = dasherPreparationMinimumDistance, PreparationMaximumDistance = dasherPreparationMaximumDistance,
            ApproachSpeed = dasherApproachSpeed, RetreatSpeed = dasherRetreatSpeed, PreparationMovement = dasherPreparationMovement,
            TelegraphDuration = dasherTelegraphDuration, DashSpeed = dasherDashSpeed, MaximumDistance = dasherMaximumDistance,
            RecoveryDuration = dasherRecoveryDuration, AvoidancePolicy = dasherAvoidancePolicy, CorridorWidth = dasherCorridorWidth,
            BehindPlayerDistance = dasherBehindPlayerDistance, PlayerDamage = dasherPlayerDamage, PlayerKnockback = dasherPlayerKnockback,
            PlayerInvincibilitySeconds = dasherPlayerInvincibilitySeconds, DashEnemyDamage = dasherDashEnemyDamage,
            DashEnemyKnockback = dasherDashEnemyKnockback, DashImpactPositionWeight = dasherDashImpactPositionWeight,
            LaunchedEnemyDamage = dasherLaunchedEnemyDamage, LaunchedEnemyKnockback = dasherLaunchedEnemyKnockback,
            LaunchedImpactPositionWeight = dasherLaunchedImpactPositionWeight,
            EliteDamage = dasherEliteDamage, EliteKnockback = dasherEliteKnockback,
            BossDamage = dasherBossDamage, BossKnockback = dasherBossKnockback,
            PreserveMomentumAgainstElites = dasherPreserveMomentumAgainstElites ? (byte)1 : (byte)0,
            PreserveMomentumAgainstBosses = dasherPreserveMomentumAgainstBosses ? (byte)1 : (byte)0
        };
    }
}
