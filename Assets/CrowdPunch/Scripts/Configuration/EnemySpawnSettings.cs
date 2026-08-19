using System;
using CrowdPunch.Components;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace CrowdPunch.Configuration
{
    public enum EnemyArchetype
    {
        Baseline,
        Ranged,
        Explosive,
        Dasher,
        Elite
    }

    [Serializable]
    public struct EnemyArchetypeSeparationOverride
    {
        [SerializeField] private EnemyArchetype archetype;
        [SerializeField, Min(0f)] private float separationDistanceMin;
        [SerializeField, Min(0f)] private float separationDistanceMax;
        [SerializeField, Min(0f)] private float separationWeight;

        public EnemyArchetype Archetype => archetype;
        public float SeparationDistanceMin => separationDistanceMin;
        public float SeparationDistanceMax => separationDistanceMax;
        public float SeparationWeight => separationWeight;
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

        [Header("Common movement (provisional)")]
        [SerializeField, Min(0f)] private float moveSpeed = 4f;
        [SerializeField, Min(0f)] private float wanderSpeed = 1.5f;
        [SerializeField, Min(0f)] private float chargeDistance = 12f;
        [SerializeField, Min(0f)] private float chargeSpeedMultiplier = 1.75f;
        [SerializeField, Min(0f)] private float acceleration = 12f;
        [SerializeField, Min(0f)] private float brakingAcceleration = 8f;
        [SerializeField, Min(0f)] private float turnSpeed = 12f;
        [SerializeField, Min(0f)] private float stoppingDistance = 1.25f;
        [SerializeField, Min(0f)] private float surroundDistance = 3.5f;
        [SerializeField, Min(0f)] private float surroundRingSpacing = 0.75f;
        [FormerlySerializedAs("separationDistance")]
        [SerializeField, Min(0f)] private float separationDistanceMin = 1.1f;
        [SerializeField, Min(0f)] private float separationDistanceMax = 1.6f;
        [SerializeField, Min(0f)] private float separationWeight = 1.4f;

        [Header("Target archetype separation overrides (provisional)")]
        [SerializeField] private EnemyArchetypeSeparationOverride[] archetypeSeparationOverrides =
            Array.Empty<EnemyArchetypeSeparationOverride>();

        [Header("Common health and contact (provisional)")]
        [SerializeField, Min(0.01f)] private float maxHealth = 30f;
        [SerializeField, Range(0f, 1f)] private float contactDamagePercent = 0.05f;
        [SerializeField, Min(0f)] private float contactPushStrength = 10f;
        [SerializeField, Min(0f)] private float contactInvincibilitySeconds = 0.5f;
        [SerializeField, Min(0f)] private float contactRadius = 0.75f;
        [SerializeField, Min(0f)] private float contactAttemptDistance = 5f;
        [SerializeField, Min(0f)] private float contactAttemptIntervalMin = 2f;
        [SerializeField, Min(0f)] private float contactAttemptIntervalMax = 5f;
        [SerializeField, Min(0f)] private float contactAttemptDuration = 0.75f;
        [SerializeField, Min(0f)] private float contactAttemptSpeedMultiplier = 1.25f;
        [SerializeField, Min(0f)] private float contactAttemptSeparationWeight = 0.35f;

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
        [SerializeField, Min(0f)] private float dasherLaunchedEnemyDamage = 25f;
        [SerializeField, Min(0f)] private float dasherLaunchedEnemyKnockback = 18f;
        [SerializeField, Range(0f, 1f)] private float dasherLaunchedImpactPositionWeight = 0.35f;
        [SerializeField, Min(0f)] private float dasherEliteDamage = 15f;
        [SerializeField, Min(0f)] private float dasherEliteKnockback = 8f;
        [SerializeField, Min(0f)] private float dasherBossDamage = 5f;
        [SerializeField, Min(0f)] private float dasherBossKnockback = 2f;
        [SerializeField] private bool dasherPreserveMomentumAgainstElites;
        [SerializeField] private bool dasherPreserveMomentumAgainstBosses;

        [Header("Elite punch - cadence")]
        [SerializeField, Min(0f)] private float eliteInitialDelay = 2f, eliteCooldown = 4f, eliteCooldownVariation = 1f;
        [SerializeField, Min(0f)] private float eliteMaximumSetupDuration = 5f, eliteRetargetInterval = 0.25f;
        [Header("Elite punch - target eligibility")]
        [SerializeField, Min(0f)] private float eliteMaximumSearchRange = 20f, eliteMinimumTargetPlayerDistance = 2f, eliteMaximumTargetPlayerDistance = 18f;
        [SerializeField] private bool eliteAllowActiveTargets = true, eliteAllowRecoveringTargets = true, eliteAllowLaunchedTargets, eliteAllowSharedTargets;
        [SerializeField, Min(1)] private int eliteMaximumEvaluatedCandidates = 64;
        [Header("Elite punch - tactic selection")]
        [SerializeField, Range(0f, 1f)] private float eliteClearPathTacticProbability = 0.5f;
        [SerializeField, Min(0f)] private float eliteClearPathAlignmentWeight = 2f, eliteClearPathRepositionWeight = 1f, eliteClearPathDistanceWeight = 0.25f;
        [SerializeField, Min(0f)] private float eliteCrowdCorridorRadius = 1.5f, eliteCrowdDistanceBeyondPlayer = 3f, eliteCrowdNearPlayerWeight = 1f, eliteMinimumCrowdScore;
        [Header("Elite punch - repositioning")]
        [SerializeField, Min(0f)] private float eliteDesiredPunchDistance = 1f, elitePositionTolerance = 0.25f, eliteAimAngleToleranceDegrees = 8f;
        [SerializeField, Min(0f)] private float elitePlayerMovementInvalidationDistance = 1.5f, eliteTargetMovementInvalidationDistance = 1f, eliteSetupMovementSpeedMultiplier = 1.25f;
        [SerializeField] private bool eliteApplySeparationDuringSetup;
        [Header("Elite punch - effects")]
        [SerializeField, Min(0f)] private float elitePunchRange = 1.5f, elitePunchRadius = 0.75f, eliteLaunchForce = 20f, elitePunchDamage = 10f;
        [SerializeField, Range(0f, 1f)] private float elitePushDirectionPositionWeight = 0.35f;
        [SerializeField] private ElitePunchInteractionMode eliteInteractionMode = ElitePunchInteractionMode.SelectedTargetOnly;
        [SerializeField] private bool eliteProjectileReceivesDamage = true, eliteAffectActive = true, eliteAffectRecovering = true, eliteAffectLaunched;
        [SerializeField] private bool eliteCanDirectlyHitPlayer;
        [SerializeField, Min(0f)] private float eliteDirectPlayerDamage = 10f, elitePlayerPush = 8f, elitePlayerInvincibilityDuration = 0.5f;
        [Header("Elite punch - optional wind-up / telegraph")]
        [SerializeField, Min(0f)] private float eliteWindUpDuration;
        [SerializeField] private bool eliteEnableTelegraph;
        [SerializeField, Min(0f)] private float eliteTelegraphDuration = 0.25f;

        public GameObject EnemyPrefab => enemyPrefab;
        public GameObject RangedProjectilePrefab => rangedProjectilePrefab;
        public EnemyArchetype Archetype => archetype;
        public int InitialCount => initialCount;
        public float Radius => radius;
        public bool RespawnEnabled => respawnEnabled;
        public EnemyMovementSettings MovementSettings => new EnemyMovementSettings
        {
            MoveSpeed = moveSpeed, WanderSpeed = wanderSpeed, ChargeDistance = chargeDistance,
            ChargeSpeedMultiplier = chargeSpeedMultiplier, Acceleration = acceleration,
            BrakingAcceleration = brakingAcceleration, TurnSpeed = turnSpeed, StoppingDistance = stoppingDistance,
            SurroundDistance = surroundDistance, SurroundRingSpacing = surroundRingSpacing,
            SeparationDistanceMin = separationDistanceMin, SeparationDistanceMax = separationDistanceMax,
            SeparationWeight = separationWeight
        };
        public EnemyArchetypeSeparationSettings ArchetypeSeparationSettings
        {
            get
            {
                EnemyArchetypeSeparationSettings settings = default;
                if (archetypeSeparationOverrides == null)
                {
                    return settings;
                }
                foreach (EnemyArchetypeSeparationOverride entry in archetypeSeparationOverrides)
                {
                    settings.Set(
                        (EnemyArchetypeKind)entry.Archetype,
                        entry.SeparationDistanceMin,
                        entry.SeparationDistanceMax,
                        entry.SeparationWeight);
                }
                return settings;
            }
        }
        public Health Health => new Health { Current = maxHealth, Max = maxHealth };
        public EnemyContactDamageSettings ContactDamageSettings => new EnemyContactDamageSettings
        {
            DamagePercent = contactDamagePercent, PushStrength = contactPushStrength,
            PlayerInvincibilitySeconds = contactInvincibilitySeconds, ContactRadius = contactRadius,
            AttemptDistance = contactAttemptDistance, AttemptIntervalMin = contactAttemptIntervalMin,
            AttemptIntervalMax = contactAttemptIntervalMax, AttemptDuration = contactAttemptDuration,
            AttemptSpeedMultiplier = contactAttemptSpeedMultiplier,
            AttemptSeparationWeight = contactAttemptSeparationWeight
        };
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
            ApproachSpeed = approachSpeed, RetreatSpeed = retreatSpeed, PreparationMovement = dasherPreparationMovement,
            TelegraphDuration = dasherTelegraphDuration, DashSpeed = dasherDashSpeed, MaximumDistance = dasherMaximumDistance,
            RecoveryDuration = dasherRecoveryDuration, AvoidancePolicy = dasherAvoidancePolicy, CorridorWidth = dasherCorridorWidth,
            BehindPlayerDistance = dasherBehindPlayerDistance, PlayerDamage = dasherPlayerDamage, PlayerKnockback = dasherPlayerKnockback,
            PlayerInvincibilitySeconds = dasherPlayerInvincibilitySeconds,
            LaunchedEnemyDamage = dasherLaunchedEnemyDamage, LaunchedEnemyKnockback = dasherLaunchedEnemyKnockback,
            LaunchedImpactPositionWeight = dasherLaunchedImpactPositionWeight,
            EliteDamage = dasherEliteDamage, EliteKnockback = dasherEliteKnockback,
            BossDamage = dasherBossDamage, BossKnockback = dasherBossKnockback,
            PreserveMomentumAgainstElites = dasherPreserveMomentumAgainstElites ? (byte)1 : (byte)0,
            PreserveMomentumAgainstBosses = dasherPreserveMomentumAgainstBosses ? (byte)1 : (byte)0
        };
        public ElitePunchSettings ElitePunchSettings => new ElitePunchSettings
        {
            InitialDelay=eliteInitialDelay, Cooldown=eliteCooldown, CooldownVariation=eliteCooldownVariation, MaximumSetupDuration=eliteMaximumSetupDuration, RetargetInterval=eliteRetargetInterval,
            MaximumSearchRange=eliteMaximumSearchRange, MinimumTargetPlayerDistance=eliteMinimumTargetPlayerDistance, MaximumTargetPlayerDistance=eliteMaximumTargetPlayerDistance,
            AllowActiveTargets=eliteAllowActiveTargets?(byte)1:(byte)0, AllowRecoveringTargets=eliteAllowRecoveringTargets?(byte)1:(byte)0, AllowLaunchedTargets=eliteAllowLaunchedTargets?(byte)1:(byte)0, AllowSharedTargets=eliteAllowSharedTargets?(byte)1:(byte)0,
            ClearPathTacticProbability=math.saturate(eliteClearPathTacticProbability), MaximumEvaluatedCandidates=math.max(1,eliteMaximumEvaluatedCandidates),
            ClearPathAlignmentWeight=eliteClearPathAlignmentWeight, ClearPathRepositionWeight=eliteClearPathRepositionWeight, ClearPathDistanceWeight=eliteClearPathDistanceWeight,
            CrowdCorridorRadius=eliteCrowdCorridorRadius, CrowdDistanceBeyondPlayer=eliteCrowdDistanceBeyondPlayer, CrowdNearPlayerWeight=eliteCrowdNearPlayerWeight, MinimumCrowdScore=eliteMinimumCrowdScore,
            DesiredPunchDistance=eliteDesiredPunchDistance, PositionTolerance=elitePositionTolerance, AimAngleToleranceDegrees=eliteAimAngleToleranceDegrees,
            PlayerMovementInvalidationDistance=elitePlayerMovementInvalidationDistance, TargetMovementInvalidationDistance=eliteTargetMovementInvalidationDistance, SetupMovementSpeedMultiplier=eliteSetupMovementSpeedMultiplier, ApplySeparationDuringSetup=eliteApplySeparationDuringSetup?(byte)1:(byte)0,
            PunchRange=elitePunchRange, PunchRadius=elitePunchRadius, LaunchForce=eliteLaunchForce, PunchDamage=elitePunchDamage, PushDirectionPositionWeight=elitePushDirectionPositionWeight,
            InteractionMode=eliteInteractionMode, ProjectileReceivesDamage=eliteProjectileReceivesDamage?(byte)1:(byte)0, AffectActive=eliteAffectActive?(byte)1:(byte)0, AffectRecovering=eliteAffectRecovering?(byte)1:(byte)0, AffectLaunched=eliteAffectLaunched?(byte)1:(byte)0,
            CanDirectlyHitPlayer=eliteCanDirectlyHitPlayer?(byte)1:(byte)0, DirectPlayerDamage=eliteDirectPlayerDamage, PlayerPush=elitePlayerPush, PlayerInvincibilityDuration=elitePlayerInvincibilityDuration,
            WindUpDuration=eliteWindUpDuration, EnableTelegraph=eliteEnableTelegraph?(byte)1:(byte)0, TelegraphDuration=eliteTelegraphDuration
        };
    }
}
