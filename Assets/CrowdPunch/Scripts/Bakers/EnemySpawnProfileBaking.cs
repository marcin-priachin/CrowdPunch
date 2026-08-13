using CrowdPunch.Components;
using CrowdPunch.Configuration;
using Unity.Entities;
using UnityEngine;

namespace CrowdPunch.Bakers
{
    /// <summary>Converts one reusable settings asset into the shared ECS spawn profile.</summary>
    internal static class EnemySpawnProfileBaking
    {
        public static bool TryCreate<TAuthoring>(
            Baker<TAuthoring> baker,
            EnemySpawnSettings settings,
            out EnemySpawnProfile profile)
            where TAuthoring : Component
        {
            profile = default;
            if (settings == null)
            {
                return false;
            }

            baker.DependsOn(settings);
            Entity enemyPrefab = settings.EnemyPrefab == null
                ? Entity.Null
                : baker.GetEntity(settings.EnemyPrefab, TransformUsageFlags.Dynamic);
            Entity projectilePrefab = settings.RangedProjectilePrefab == null
                ? Entity.Null
                : baker.GetEntity(settings.RangedProjectilePrefab, TransformUsageFlags.Dynamic);

            profile = new EnemySpawnProfile
            {
                EnemyPrefab = enemyPrefab,
                Archetype = settings.Archetype switch
                {
                    Configuration.EnemyArchetype.Ranged => EnemyArchetypeKind.Ranged,
                    Configuration.EnemyArchetype.Explosive => EnemyArchetypeKind.Explosive,
                    Configuration.EnemyArchetype.Dasher => EnemyArchetypeKind.Dasher,
                    _ => EnemyArchetypeKind.Baseline
                },
                RespawnEnabled = settings.RespawnEnabled ? (byte)1 : (byte)0,
                RangedSettings = new RangedEnemySettings
                {
                    ProjectilePrefab = projectilePrefab,
                    PreferredMinimumDistance = settings.PreferredMinimumDistance,
                    PreferredMaximumDistance = settings.PreferredMaximumDistance,
                    EngagementRange = settings.EngagementRange,
                    RetreatSpeed = settings.RetreatSpeed,
                    ApproachSpeed = settings.ApproachSpeed,
                    InitialAttackDelay = settings.InitialAttackDelay,
                    InitialDelayVariation = settings.InitialDelayVariation,
                    WindUpDuration = settings.WindUpDuration,
                    Cooldown = settings.Cooldown,
                    CooldownVariation = settings.CooldownVariation,
                    ProjectileDamage = settings.ProjectileDamage,
                    PlayerInvincibilitySeconds = settings.PlayerInvincibilitySeconds,
                    ProjectileSpeed = settings.ProjectileSpeed,
                    ProjectileAimSpreadRadius = settings.ProjectileAimSpreadRadius,
                    ProjectileAimTargetYOffset = settings.ProjectileAimTargetYOffset,
                    ProjectileArcHeight = settings.ProjectileArcHeight,
                    ProjectileMinimumAltitude = settings.ProjectileMinimumAltitude,
                    ProjectileLifetime = settings.ProjectileLifetime,
                    ProjectileRadius = settings.ProjectileRadius,
                    ProjectilePlayerLayers = settings.ProjectilePlayerLayers
                },
                ExplosiveSettings = new ExplosiveEnemySettings
                {
                    Radius = settings.ExplosionRadius,
                    Damage = settings.ExplosionDamage,
                    NormalEnemyKnockbackForce = settings.NormalEnemyKnockbackForce,
                    PlayerEliteKnockbackForce = settings.PlayerEliteKnockbackForce,
                    BossKnockbackForce = settings.BossKnockbackForce,
                    PlayerInvincibilitySeconds = settings.ExplosionPlayerInvincibilitySeconds,
                    VisualDuration = settings.ExplosionVisualDuration,
                    VisualSizeMultiplier = settings.ExplosionVisualSizeMultiplier
                },
                DasherSettings = settings.DasherSettings
            };
            return true;
        }
    }
}
