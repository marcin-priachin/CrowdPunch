using CrowdPunch.Authoring;
using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CrowdPunch.Bakers
{
    /// <summary>
    /// Converts a spawner GameObject into ECS spawn settings.
    /// </summary>
    public sealed class SpawnerBaker : Baker<SpawnerAuthoring>
    {
        public override void Bake(SpawnerAuthoring authoring)
        {
            if (!EnemySpawnProfileBaking.TryCreate(this, authoring.Settings, out EnemySpawnProfile profile))
            {
                return;
            }

            Entity entity = GetEntity(TransformUsageFlags.WorldSpace);
            Transform spawnerTransform = GetComponent<Transform>();

            AddComponent(entity, new SpawnSettings
            {
                Profile = profile,
                InitialCount = authoring.Settings.InitialCount,
                SpawnRadius = authoring.Settings.Radius,
                Center = new float3(
                    spawnerTransform.position.x,
                    spawnerTransform.position.y,
                    spawnerTransform.position.z)
            });
        }
    }
}
