using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Bakers
{
    /// <summary>
    /// Converts a spawner GameObject into ECS spawn settings.
    /// </summary>
    public sealed class SpawnerBaker : Baker<SpawnerAuthoring>
    {
        public override void Bake(SpawnerAuthoring authoring)
        {
            if (authoring.Settings == null)
            {
                return;
            }

            DependsOn(authoring.Settings);
            Entity entity = GetEntity(TransformUsageFlags.WorldSpace);
            Entity prefab = authoring.Settings.EnemyPrefab == null
                ? Entity.Null
                : GetEntity(authoring.Settings.EnemyPrefab, TransformUsageFlags.Dynamic);
            Entity projectilePrefab = authoring.Settings.RangedProjectilePrefab == null
                ? Entity.Null
                : GetEntity(authoring.Settings.RangedProjectilePrefab, TransformUsageFlags.Dynamic);

            AddComponent(entity, new SpawnSettings
            {
                EnemyPrefab = prefab,
                RangedProjectilePrefab = projectilePrefab,
                Archetype = authoring.Settings.Archetype switch
                {
                    Configuration.EnemyArchetype.Ranged => EnemyArchetypeKind.Ranged,
                    Configuration.EnemyArchetype.Explosive => EnemyArchetypeKind.Explosive,
                    Configuration.EnemyArchetype.Dasher => EnemyArchetypeKind.Dasher,
                    _ => EnemyArchetypeKind.Baseline
                },
                InitialCount = authoring.Settings.InitialCount,
                SpawnRadius = authoring.Settings.Radius,
                RespawnEnabled = authoring.Settings.RespawnEnabled ? (byte)1 : (byte)0,
                Center = new float3(
                    authoring.transform.position.x,
                    authoring.transform.position.y,
                    authoring.transform.position.z),
                RangedSettings = new RangedEnemySettings
                {
                    ProjectilePrefab = projectilePrefab,
                    PreferredMinimumDistance = authoring.Settings.PreferredMinimumDistance,
                    PreferredMaximumDistance = authoring.Settings.PreferredMaximumDistance,
                    EngagementRange = authoring.Settings.EngagementRange,
                    RetreatSpeed = authoring.Settings.RetreatSpeed,
                    ApproachSpeed = authoring.Settings.ApproachSpeed,
                    InitialAttackDelay = authoring.Settings.InitialAttackDelay,
                    InitialDelayVariation = authoring.Settings.InitialDelayVariation,
                    WindUpDuration = authoring.Settings.WindUpDuration,
                    Cooldown = authoring.Settings.Cooldown,
                    CooldownVariation = authoring.Settings.CooldownVariation,
                    ProjectileDamage = authoring.Settings.ProjectileDamage,
                    PlayerInvincibilitySeconds = authoring.Settings.PlayerInvincibilitySeconds,
                    ProjectileSpeed = authoring.Settings.ProjectileSpeed,
                    ProjectileAimSpreadRadius = authoring.Settings.ProjectileAimSpreadRadius,
                    ProjectileAimTargetYOffset = authoring.Settings.ProjectileAimTargetYOffset,
                    ProjectileArcHeight = authoring.Settings.ProjectileArcHeight,
                    ProjectileMinimumAltitude = authoring.Settings.ProjectileMinimumAltitude,
                    ProjectileLifetime = authoring.Settings.ProjectileLifetime,
                    ProjectileRadius = authoring.Settings.ProjectileRadius,
                    ProjectilePlayerLayers = authoring.Settings.ProjectilePlayerLayers
                },
                DasherSettings = authoring.Settings.DasherSettings,
                ExplosiveSettings = new ExplosiveEnemySettings
                {
                    Radius = authoring.Settings.ExplosionRadius,
                    Damage = authoring.Settings.ExplosionDamage,
                    NormalEnemyKnockbackForce = authoring.Settings.NormalEnemyKnockbackForce,
                    PlayerEliteKnockbackForce = authoring.Settings.PlayerEliteKnockbackForce,
                    BossKnockbackForce = authoring.Settings.BossKnockbackForce,
                    PlayerInvincibilitySeconds = authoring.Settings.ExplosionPlayerInvincibilitySeconds,
                    VisualDuration = authoring.Settings.ExplosionVisualDuration,
                    VisualSizeMultiplier = authoring.Settings.ExplosionVisualSizeMultiplier
                }
            });
        }
    }
}
