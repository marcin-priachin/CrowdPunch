using CrowdPunch.Components;
using CrowdPunch.Configuration;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

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
                SpawnClearance = GetSpawnClearance(settings.EnemyPrefab),
                Archetype = settings.Archetype switch
                {
                    Configuration.EnemyArchetype.Ranged => EnemyArchetypeKind.Ranged,
                    Configuration.EnemyArchetype.Explosive => EnemyArchetypeKind.Explosive,
                    Configuration.EnemyArchetype.Dasher => EnemyArchetypeKind.Dasher,
                    _ => EnemyArchetypeKind.Baseline
                },
                RespawnEnabled = settings.RespawnEnabled ? (byte)1 : (byte)0,
                MovementSettings = settings.MovementSettings,
                ArchetypeSeparationSettings = settings.ArchetypeSeparationSettings,
                Health = settings.Health,
                ContactDamageSettings = settings.ContactDamageSettings,
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

        private static float GetSpawnClearance(GameObject prefab)
        {
            if (prefab == null)
            {
                return 0f;
            }

            float clearance = 0f;
            foreach (UnityEngine.Collider collider in prefab.GetComponentsInChildren<UnityEngine.Collider>(true))
            {
                if (!TryGetLocalBounds(collider, out Bounds localBounds))
                {
                    continue;
                }

                Vector3 center = localBounds.center;
                Vector3 extents = localBounds.extents;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 prefabLocalCorner = TransformToPrefabLocal(corner, collider.transform, prefab.transform);
                    clearance = math.max(clearance, prefabLocalCorner.magnitude);
                }
            }
            return clearance;
        }

        private static Vector3 TransformToPrefabLocal(Vector3 point, Transform source, Transform prefabRoot)
        {
            Transform current = source;
            while (current != null && current != prefabRoot)
            {
                point = current.localPosition
                    + current.localRotation * Vector3.Scale(current.localScale, point);
                current = current.parent;
            }
            return point;
        }

        private static bool TryGetLocalBounds(UnityEngine.Collider collider, out Bounds bounds)
        {
            switch (collider)
            {
                case BoxCollider box:
                    bounds = new Bounds(box.center, box.size);
                    return true;

                case SphereCollider sphere:
                    bounds = new Bounds(sphere.center, Vector3.one * (sphere.radius * 2f));
                    return true;

                case CapsuleCollider capsule:
                    Vector3 size = Vector3.one * (capsule.radius * 2f);
                    size[capsule.direction] = math.max(capsule.height, capsule.radius * 2f);
                    bounds = new Bounds(capsule.center, size);
                    return true;

                case MeshCollider mesh when mesh.sharedMesh != null:
                    bounds = mesh.sharedMesh.bounds;
                    return true;

                default:
                    bounds = default;
                    return false;
            }
        }
    }
}
