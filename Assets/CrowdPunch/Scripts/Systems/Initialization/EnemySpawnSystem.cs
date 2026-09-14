using CrowdPunch.Components;
using CrowdPunch.Utilities;
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

            NavigationGrid navigationGrid = SystemAPI.HasSingleton<NavigationGrid>() ? SystemAPI.GetSingleton<NavigationGrid>() : default;
            int rejected = 0;
            EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            Random random = Random.CreateFromIndex(1);

            foreach (var (spawnSettingsReference, spawner) in SystemAPI.Query<RefRO<SpawnSettings>>().WithEntityAccess())
            {
                commandBuffer.RemoveComponent<SpawnSettings>(spawner);
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
                    int attempt = 0;
                    while (!NavigationGeometry.SpawnAllowed(navigationGrid, position.xz, spawnSettings.Profile.NavigationRadius) && attempt++ < 32)
                        position = GetRandomSpawnPosition(ref random, spawnSettings.Center, spawnSettings.SpawnRadius);
                    if (!NavigationGeometry.SpawnAllowed(navigationGrid, position.xz, spawnSettings.Profile.NavigationRadius)) { rejected++; continue; }
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

            foreach (var (spawnPointReference, point) in
                     SystemAPI.Query<RefRO<AuthoredEnemySpawnPoint>>().WithEntityAccess())
            {
                commandBuffer.RemoveComponent<AuthoredEnemySpawnPoint>(point);
                AuthoredEnemySpawnPoint spawnPoint = spawnPointReference.ValueRO;
                if (!NavigationGeometry.SpawnAllowed(navigationGrid, spawnPoint.Position.xz, spawnPoint.Profile.NavigationRadius)) { rejected++; continue; }
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

            if (rejected > 0)
            {
                UnityEngine.Debug.LogWarning("Initial enemy placement rejected blocked or disconnected positions. Check Navigation Inspector and authored spawn regions.");
                if (SystemAPI.HasSingleton<NavigationDiagnostics>())
                { var diagnostics = SystemAPI.GetSingleton<NavigationDiagnostics>(); diagnostics.RejectedSpawns += rejected; SystemAPI.SetSingleton(diagnostics); }
            }
            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();

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
