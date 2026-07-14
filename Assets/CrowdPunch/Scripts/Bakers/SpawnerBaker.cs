using CrowdPunch.Authoring;
using CrowdPunch.Components;
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
            Entity entity = GetEntity(TransformUsageFlags.WorldSpace);
            Entity prefab = authoring.EnemyPrefab == null
                ? Entity.Null
                : GetEntity(authoring.EnemyPrefab, TransformUsageFlags.Dynamic);

            AddComponent(entity, new SpawnSettings
            {
                EnemyPrefab = prefab,
                InitialCount = authoring.InitialCount,
                SpawnRadius = authoring.SpawnRadius,
                Center = new float3(
                    authoring.transform.position.x,
                    authoring.transform.position.y,
                    authoring.transform.position.z)
            });
        }
    }
}
