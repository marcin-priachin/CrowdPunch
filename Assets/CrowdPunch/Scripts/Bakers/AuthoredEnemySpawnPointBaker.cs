using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CrowdPunch.Bakers
{
    /// <summary>Converts one child spawn point into one exact-position ECS spawn request.</summary>
    public sealed class AuthoredEnemySpawnPointBaker : Baker<AuthoredEnemySpawnPointAuthoring>
    {
        public override void Bake(AuthoredEnemySpawnPointAuthoring authoring)
        {
            AuthoredEnemyGroupAuthoring group = GetComponentInParent<AuthoredEnemyGroupAuthoring>();
            if (group == null)
            {
                Debug.LogWarning(
                    $"{nameof(AuthoredEnemySpawnPointAuthoring)} '{authoring.name}' must be a child of an "
                    + $"{nameof(AuthoredEnemyGroupAuthoring)} and will not spawn an enemy.",
                    authoring);
                return;
            }

            if (authoring.Settings == null)
            {
                Debug.LogWarning(
                    $"{nameof(AuthoredEnemySpawnPointAuthoring)} '{authoring.name}' has no spawn settings and will be skipped.",
                    authoring);
                return;
            }

            if (authoring.Settings.EnemyPrefab == null)
            {
                DependsOn(authoring.Settings);
                Debug.LogWarning(
                    $"{nameof(AuthoredEnemySpawnPointAuthoring)} '{authoring.name}' references settings without an enemy prefab and will be skipped.",
                    authoring);
                return;
            }

            if (!EnemySpawnProfileBaking.TryCreate(this, authoring.Settings, out EnemySpawnProfile profile))
            {
                return;
            }

            DependsOn(group);
            Entity entity = GetEntity(TransformUsageFlags.WorldSpace);
            Transform spawnTransform = GetComponent<Transform>();
            float3 position = new float3(
                spawnTransform.position.x,
                spawnTransform.position.y,
                spawnTransform.position.z);
            uint seed = math.hash(new uint4(math.asuint(position), (uint)profile.Archetype));

            AddComponent(entity, new AuthoredEnemySpawnPoint
            {
                Profile = profile,
                Position = position,
                RandomSeed = seed
            });
        }
    }
}
