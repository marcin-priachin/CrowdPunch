using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Initialization
{
    /// <summary>
    /// Creates the initial ECS enemy crowd from baked spawn settings.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameInitializationGroup))]
    [UpdateAfter(typeof(BootstrapSystem))]
    public partial struct EnemySpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpawnSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            Random random = Random.CreateFromIndex(1);

            foreach (RefRO<SpawnSettings> spawnSettingsReference in SystemAPI.Query<RefRO<SpawnSettings>>())
            {
                SpawnSettings spawnSettings = spawnSettingsReference.ValueRO;

                if (spawnSettings.EnemyPrefab == Entity.Null || spawnSettings.InitialCount <= 0)
                {
                    continue;
                }

                EnemyMovementSettings movementSettings = SystemAPI.GetComponent<EnemyMovementSettings>(
                    spawnSettings.EnemyPrefab);
                float separationMin = math.max(0f, math.min(
                    movementSettings.SeparationDistanceMin,
                    movementSettings.SeparationDistanceMax));
                float separationMax = math.max(separationMin, math.max(
                    movementSettings.SeparationDistanceMin,
                    movementSettings.SeparationDistanceMax));

                for (int index = 0; index < spawnSettings.InitialCount; index++)
                {
                    Entity enemy = commandBuffer.Instantiate(spawnSettings.EnemyPrefab);
                    float3 position = GetRandomSpawnPosition(ref random, spawnSettings.Center, spawnSettings.SpawnRadius);
                    float separationDistance = random.NextFloat(separationMin, separationMax);

                    commandBuffer.SetComponent(enemy, LocalTransform.FromPosition(position));
                    commandBuffer.SetComponent(enemy, new EnemySeparationDistance
                    {
                        Value = separationDistance
                    });
                }
            }

            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();

            state.Enabled = false;
        }

        private static float3 GetRandomSpawnPosition(ref Random random, float3 center, float radius)
        {
            float angle = random.NextFloat(0f, math.PI * 2f);
            float distance = math.sqrt(random.NextFloat()) * math.max(0f, radius);
            float x = math.cos(angle) * distance;
            float z = math.sin(angle) * distance;

            return center + new float3(x, 0f, z);
        }
    }
}
