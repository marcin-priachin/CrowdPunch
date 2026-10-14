using CrowdPunch.Components;
using CrowdPunch.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Initialization
{
    internal static class LinkedPairWaveSpawning
    {
        public static bool TrySpawn(EntityCommandBuffer commands, EntityManager em, EnemyWaveDefinition wave,
            DynamicBuffer<EnemyWaveSpawnRange> ranges, NavigationGrid grid, PhysicsWorldSingleton world,
            PlayerSnapshot player, NativeList<float4> occupied, NativeList<EnemySpawnSpacing.Occupant> spacing,
            Entity sequenceEntity, ref EnemyWaveSequence sequence, ref Random random)
        {
            var profile = wave.LinkedPairProfile;
            if (!em.HasComponent<LinkedPairTuning>(profile.EnemyPrefab)) return false;
            var tuning = em.GetComponentData<LinkedPairTuning>(profile.EnemyPrefab);
            float separation = math.max(profile.SpawnClearance * 2 + .1f, EnemySpawnSpacing.Minimum(profile.MovementSettings));
            if (separation >= tuning.MaximumLength) return false;
            using var accepted = new NativeList<float4>(Allocator.Temp);
            using var empty = new NativeList<float4>(Allocator.Temp);
            using var plane = em.CreateEntityQuery(ComponentType.ReadOnly<ArenaGroundPlane>());
            float floor = plane.CalculateEntityCount() == 1 ? plane.GetSingleton<ArenaGroundPlane>().SurfaceHeight : -1;
            for (int attempt = 0; attempt < sequence.PlacementAttemptsPerEnemy; attempt++)
            {
                float3 a = EnemyWaveSpawnSystem.SelectPosition(ref random, wave, ranges);
                float angle = random.NextFloat(0, math.PI * 2);
                float3 b = a + new float3(math.cos(angle), 0, math.sin(angle)) * separation;
                accepted.Clear(); accepted.Add(new float4(a, profile.SpawnClearance));
                if (!Clear(a) || !Clear(b) || !EnemyWaveSpawnSystem.IsSafe(world, player, a, profile.SpawnClearance,
                    sequence.MinimumPlayerDistance, occupied, empty)) continue;
                if (!EnemyWaveSpawnSystem.IsSafe(world, player, b, profile.SpawnClearance, sequence.MinimumPlayerDistance, occupied, accepted)) continue;
                // Both reservations must succeed before any structural work or accounting is queued.
                var first = EnemySpawnInitialization.Create(commands, em, profile, a, ref random);
                var second = EnemySpawnInitialization.Create(commands, em, profile, b, ref random);
                if (first == Entity.Null || second == Entity.Null) throw new System.InvalidOperationException("Validated pair prefab failed spawning.");
                var record = commands.CreateEntity();
                commands.AddComponent(record, new LinkedPair { A = first, B = second, Leader = first,
                    Sequence = sequenceEntity, RunGeneration = sequence.RunGeneration, LifetimeA = 1, LifetimeB = 1,
                    PreviousA = a, PreviousB = b, LastPunchAt = -1 });
                commands.AddComponent(record, tuning); commands.AddBuffer<LinkedPairClock>(record);
                var owner = new EnemyWaveOwnership { Sequence = sequenceEntity, RunGeneration = sequence.RunGeneration, WaveIndex = sequence.CurrentWaveIndex };
                commands.SetComponent(first, new LinkedPairEndpoint { Pair = record });
                commands.SetComponent(second, new LinkedPairEndpoint { Pair = record });
                commands.AddComponent(first, owner); commands.AddComponent(second, owner);
                commands.SetComponent(first, new EnemyRespawnSettings()); commands.SetComponent(second, new EnemyRespawnSettings());
                occupied.Add(new float4(a, profile.SpawnClearance)); occupied.Add(new float4(b, profile.SpawnClearance));
                spacing.Add(new EnemySpawnSpacing.Occupant { Position = a, MinimumDistance = separation });
                spacing.Add(new EnemySpawnSpacing.Occupant { Position = b, MinimumDistance = separation });
                sequence.LinkedPairsSpawned++; sequence.SpawnedCount += 2; sequence.UndefeatedCount += 2;
                return true;

                bool Clear(float3 p) => NavigationGeometry.SpawnAllowed(grid, p.xz, profile.NavigationRadius)
                    && EnemyGroundSpawnClearance.IsClear(world.CollisionWorld, p, profile.NavigationRadius, floor)
                    && GroundHazardSpawnClearance.Allowed(em, p.xz, profile.NavigationRadius)
                    && EnemySpawnSpacing.IsClear(p, separation, spacing);
            }
            return false;
        }
    }
}
