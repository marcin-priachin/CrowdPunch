using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Initialization
{
    /// <summary>Creates initial enemies from both random regions and authored points.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameInitializationGroup))]
    [UpdateAfter(typeof(BootstrapSystem))]
    public partial struct EnemySpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MatchState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityQuery randomSpawnerQuery = SystemAPI.QueryBuilder().WithAll<SpawnSettings>().Build();
            EntityQuery authoredPointQuery = SystemAPI.QueryBuilder().WithAll<AuthoredEnemySpawnPoint>().Build();
            if (randomSpawnerQuery.IsEmptyIgnoreFilter && authoredPointQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            Random random = Random.CreateFromIndex(1);

            foreach (RefRO<SpawnSettings> spawnSettingsReference in SystemAPI.Query<RefRO<SpawnSettings>>())
            {
                SpawnSettings spawnSettings = spawnSettingsReference.ValueRO;
                if (spawnSettings.Profile.EnemyPrefab == Entity.Null || spawnSettings.InitialCount <= 0)
                {
                    continue;
                }

                for (int index = 0; index < spawnSettings.InitialCount; index++)
                {
                    float3 position = GetRandomSpawnPosition(
                        ref random,
                        spawnSettings.Center,
                        spawnSettings.SpawnRadius);
                    Entity enemy = EnemySpawnInitialization.Create(
                        commandBuffer,
                        state.EntityManager,
                        spawnSettings.Profile,
                        position,
                        ref random);
                    if (enemy != Entity.Null)
                    {
                        commandBuffer.AddComponent(enemy, new RandomEnemySpawnRegion
                        {
                            Center = spawnSettings.Center,
                            Radius = spawnSettings.SpawnRadius
                        });
                    }
                }
            }

            foreach (RefRO<AuthoredEnemySpawnPoint> spawnPointReference in
                     SystemAPI.Query<RefRO<AuthoredEnemySpawnPoint>>())
            {
                AuthoredEnemySpawnPoint spawnPoint = spawnPointReference.ValueRO;
                Random pointRandom = Random.CreateFromIndex(spawnPoint.RandomSeed);
                Entity enemy = EnemySpawnInitialization.Create(
                    commandBuffer,
                    state.EntityManager,
                    spawnPoint.Profile,
                    spawnPoint.Position,
                    ref pointRandom);
                if (enemy != Entity.Null)
                {
                    commandBuffer.AddComponent(enemy, new AuthoredEnemyInitialPosition
                    {
                        Value = spawnPoint.Position
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
