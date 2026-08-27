using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using MathematicsRandom = Unity.Mathematics.Random;

namespace CrowdPunch.Systems.Initialization
{
    /// <summary>Owns wave timing, deterministic allocation, safe placement, and sequence progression.</summary>
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    public partial struct EnemyWaveSpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyWaveSequence>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            double now = SystemAPI.Time.ElapsedTime;
            PhysicsWorldSingleton physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            PlayerSnapshot player = SystemAPI.HasSingleton<PlayerSnapshot>() ? SystemAPI.GetSingleton<PlayerSnapshot>() : default;
            NativeList<float4> occupiedEnemies = new NativeList<float4>(Allocator.Temp);
            EntityCommandBuffer commands = new EntityCommandBuffer(Allocator.Temp);
            foreach ((RefRO<Unity.Transforms.LocalTransform> transform, RefRO<EnemySpawnClearance> clearance) in
                     SystemAPI.Query<RefRO<Unity.Transforms.LocalTransform>, RefRO<EnemySpawnClearance>>().WithAll<Enemy>())
                occupiedEnemies.Add(new float4(transform.ValueRO.Position, clearance.ValueRO.Value));

            foreach ((RefRW<EnemyWaveSequence> sequenceReference,
                         DynamicBuffer<EnemyWaveDefinition> waves,
                         DynamicBuffer<EnemyWaveProfile> profiles,
                         DynamicBuffer<EnemyWaveEliteProfile> eliteProfiles,
                         DynamicBuffer<EnemyWaveSpawnRange> ranges,
                         Entity sequenceEntity) in
                     SystemAPI.Query<RefRW<EnemyWaveSequence>, DynamicBuffer<EnemyWaveDefinition>,
                             DynamicBuffer<EnemyWaveProfile>, DynamicBuffer<EnemyWaveEliteProfile>, DynamicBuffer<EnemyWaveSpawnRange>>()
                         .WithEntityAccess())
            {
                ref EnemyWaveSequence sequence = ref sequenceReference.ValueRW;
                if (sequence.Initialized == 0)
                {
                    InitializeSequence(commands, sequenceEntity, waves, ref sequence, now);
                    continue;
                }

                if (sequence.Phase == EnemyWaveRuntimePhase.Complete || sequence.Phase == EnemyWaveRuntimePhase.Invalid)
                    continue;

                EnemyWaveDefinition wave = waves[sequence.CurrentWaveIndex];
                if (sequence.Phase == EnemyWaveRuntimePhase.AwaitingActivation)
                {
                    bool shouldAdvance = wave.ActivationMode == (byte)EnemyWaveActivationMode.DurationElapsed
                        ? now >= sequence.NextActionAt
                        : sequence.DefeatedCount >= wave.TotalEnemyCount + wave.TotalEliteCount;
                    if (shouldAdvance)
                        Advance(commands, sequenceEntity, waves, ref sequence, now);
                    continue;
                }

                if (sequence.Phase == EnemyWaveRuntimePhase.PreWaveDelay)
                {
                    if (now < sequence.NextActionAt) continue;
                    if (wave.IsValid == 0)
                    {
                        Debug.LogError($"Wave sequence entity {sequenceEntity.Index}, wave {sequence.CurrentWaveIndex} is invalid and has stopped without affecting other spawners.");
                        sequence.Phase = EnemyWaveRuntimePhase.Invalid;
                        continue;
                    }
                    if (wave.TotalEnemyCount == 0 && wave.TotalEliteCount == 0)
                    {
                        BeginAwaitingActivation(ref sequence, wave, now);
                    }
                    else
                    {
                        sequence.Phase = EnemyWaveRuntimePhase.Spawning;
                    }
                }

                if (sequence.Phase != EnemyWaveRuntimePhase.Spawning || now < sequence.NextActionAt) continue;
                int remainingElites = wave.TotalEliteCount - sequence.EliteSpawnedCount;
                int spawnedNormals = sequence.SpawnedCount - sequence.EliteSpawnedCount;
                int remainingNormals = math.max(0, wave.TotalEnemyCount - spawnedNormals);
                int remaining = remainingElites + remainingNormals;
                if (remaining <= 0) continue;
                int requested = wave.SpawnMode == (byte)EnemyWaveSpawnMode.AllAtOnce
                    ? remaining
                    : math.min(remaining, math.max(1, wave.BatchSize));
                int spawned = SpawnBatch(commands, state.EntityManager, physicsWorld, player, sequenceEntity, wave, profiles, eliteProfiles, ranges,
                    occupiedEnemies, ref sequence, requested);

                if (sequence.SpawnedCount >= wave.TotalEnemyCount + wave.TotalEliteCount)
                {
                    BeginAwaitingActivation(ref sequence, wave, now);
                }
                else if (spawned > 0 && wave.SpawnMode == (byte)EnemyWaveSpawnMode.Batched)
                {
                    sequence.NextActionAt = now + math.max(0f, wave.BatchInterval);
                }
                else if (spawned == 0 && now >= sequence.NextPlacementWarningAt)
                {
                    Debug.LogWarning($"Wave {sequence.CurrentWaveIndex} could not find safe placement after bounded retries; pending enemies will retry.");
                    sequence.NextPlacementWarningAt = now + 5d;
                }
            }
            commands.Playback(state.EntityManager);
            commands.Dispose();
            occupiedEnemies.Dispose();
        }

        private static void BeginAwaitingActivation(ref EnemyWaveSequence sequence, EnemyWaveDefinition wave, double now)
        {
            sequence.Phase = EnemyWaveRuntimePhase.AwaitingActivation;
            if (wave.ActivationMode == (byte)EnemyWaveActivationMode.DurationElapsed)
                sequence.NextActionAt = now + math.max(0f, wave.Duration);
        }

        private static void InitializeSequence(EntityCommandBuffer commands, Entity entity,
            DynamicBuffer<EnemyWaveDefinition> waves, ref EnemyWaveSequence sequence, double now)
        {
            sequence.Initialized = 1;
            sequence.CurrentWaveIndex = 0;
            sequence.SpawnedCount = 0;
            sequence.DefeatedCount = 0;
            sequence.EliteSpawnedCount = 0;
            sequence.EliteProfileCursor = 0;
            sequence.EliteProfileSpawnedInEntry = 0;
            sequence.RandomState = sequence.InitialSeed == 0 ? 1u : sequence.InitialSeed;
            if (waves.Length == 0)
            {
                sequence.Phase = EnemyWaveRuntimePhase.Complete;
                commands.SetComponentEnabled<EnemyWaveEncounterComplete>(entity, true);
                return;
            }
            sequence.Phase = EnemyWaveRuntimePhase.PreWaveDelay;
            sequence.NextActionAt = now + waves[0].DelayBeforeWave;
            commands.SetComponentEnabled<EnemyWaveEncounterComplete>(entity, false);
        }

        private static void Advance(EntityCommandBuffer commands, Entity entity, DynamicBuffer<EnemyWaveDefinition> waves,
            ref EnemyWaveSequence sequence, double now)
        {
            sequence.CurrentWaveIndex++;
            sequence.SpawnedCount = 0;
            sequence.DefeatedCount = 0;
            sequence.EliteSpawnedCount = 0;
            sequence.EliteProfileCursor = 0;
            sequence.EliteProfileSpawnedInEntry = 0;
            if (sequence.CurrentWaveIndex >= waves.Length)
            {
                sequence.Phase = EnemyWaveRuntimePhase.Complete;
                commands.SetComponentEnabled<EnemyWaveEncounterComplete>(entity, true);
                return;
            }
            sequence.Phase = EnemyWaveRuntimePhase.PreWaveDelay;
            sequence.NextActionAt = now + waves[sequence.CurrentWaveIndex].DelayBeforeWave;
        }

        private static int SpawnBatch(EntityCommandBuffer commands, EntityManager entityManager,
            PhysicsWorldSingleton physicsWorld, PlayerSnapshot player,
            Entity sequenceEntity, EnemyWaveDefinition wave, DynamicBuffer<EnemyWaveProfile> profiles,
            DynamicBuffer<EnemyWaveEliteProfile> eliteProfiles,
            DynamicBuffer<EnemyWaveSpawnRange> ranges, NativeList<float4> occupiedEnemies,
            ref EnemyWaveSequence sequence, int requested)
        {
            MathematicsRandom random = new MathematicsRandom(sequence.RandomState == 0 ? 1u : sequence.RandomState);
            NativeList<float4> accepted = new NativeList<float4>(Allocator.Temp);
            int spawned = 0;
            for (int index = 0; index < requested; index++)
            {
                bool spawningElite = sequence.EliteSpawnedCount < wave.TotalEliteCount;
                EnemySpawnProfile selectedProfile;
                if (spawningElite)
                {
                    EnemyWaveEliteProfile selectedElite = eliteProfiles[wave.EliteProfileStart + sequence.EliteProfileCursor];
                    selectedProfile = selectedElite.Profile;
                }
                else
                {
                    selectedProfile = SelectProfile(ref random, wave, profiles).Profile;
                }
                bool found = false;
                float3 position = default;
                for (int attempt = 0; attempt < math.max(1, sequence.PlacementAttemptsPerEnemy); attempt++)
                {
                    position = SelectPosition(ref random, wave, ranges);
                    if (IsSafe(physicsWorld, player, position, selectedProfile.SpawnClearance,
                            sequence.MinimumPlayerDistance, occupiedEnemies, accepted))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    if (spawningElite) break;
                    continue;
                }

                Entity enemy = EnemySpawnInitialization.Create(commands, entityManager, selectedProfile, position, ref random);
                if (enemy == Entity.Null)
                {
                    if (spawningElite) break;
                    continue;
                }
                commands.SetComponent(enemy, new EnemyRespawnSettings { Enabled = 0 });
                commands.AddComponent(enemy, new EnemyWaveOwnership
                {
                    Sequence = sequenceEntity,
                    RunGeneration = sequence.RunGeneration,
                    WaveIndex = sequence.CurrentWaveIndex
                });
                accepted.Add(new float4(position, selectedProfile.SpawnClearance));
                spawned++;
                sequence.SpawnedCount++;
                if (spawningElite)
                {
                    sequence.EliteSpawnedCount++;
                    sequence.EliteProfileSpawnedInEntry++;
                    EnemyWaveEliteProfile current = eliteProfiles[wave.EliteProfileStart + sequence.EliteProfileCursor];
                    if (sequence.EliteProfileSpawnedInEntry >= current.Count)
                    {
                        sequence.EliteProfileCursor++;
                        sequence.EliteProfileSpawnedInEntry = 0;
                    }
                }
            }
            sequence.RandomState = random.state;
            accepted.Dispose();
            return spawned;
        }

        internal static EnemyWaveProfile SelectProfile(ref MathematicsRandom random, EnemyWaveDefinition wave,
            DynamicBuffer<EnemyWaveProfile> profiles)
        {
            float selection = random.NextFloat(0f, wave.TotalProfileWeight);
            for (int i = 0; i < wave.ProfileCount; i++)
            {
                EnemyWaveProfile profile = profiles[wave.ProfileStart + i];
                selection -= profile.Weight;
                if (selection <= 0f) return profile;
            }
            return profiles[wave.ProfileStart + wave.ProfileCount - 1];
        }

        internal static float3 SelectPosition(ref MathematicsRandom random, EnemyWaveDefinition wave,
            DynamicBuffer<EnemyWaveSpawnRange> ranges)
        {
            float selection = random.NextFloat(0f, wave.TotalRangeArea);
            EnemyWaveSpawnRange selected = ranges[wave.RangeStart + wave.RangeCount - 1];
            for (int i = 0; i < wave.RangeCount; i++)
            {
                selected = ranges[wave.RangeStart + i];
                selection -= selected.Area;
                if (selection <= 0f) break;
            }
            return selected.Center + new float3(
                random.NextFloat(-selected.Width * 0.5f, selected.Width * 0.5f), 0f,
                random.NextFloat(-selected.Depth * 0.5f, selected.Depth * 0.5f));
        }

        private static bool IsSafe(PhysicsWorldSingleton world, PlayerSnapshot player, float3 position,
            float clearance, float minimumPlayerDistance, NativeList<float4> occupiedEnemies,
            NativeList<float4> accepted)
        {
            if (clearance <= 0f) return false;
            if (player.IsAvailable)
            {
                float required = clearance + math.max(0f, player.Radius) + math.max(0f, minimumPlayerDistance);
                if (math.distancesq(position, player.Position) < required * required) return false;
            }
            if (Overlaps(position, clearance, occupiedEnemies)) return false;
            if (Overlaps(position, clearance, accepted)) return false;
            return !world.CalculateDistance(new PointDistanceInput
            {
                Position = position,
                MaxDistance = clearance,
                Filter = CollisionFilter.Default
            });
        }

        private static bool Overlaps(float3 position, float clearance, NativeList<float4> occupied)
        {
            for (int i = 0; i < occupied.Length; i++)
            {
                float4 other = occupied[i];
                float required = clearance + other.w;
                if (math.distancesq(position, other.xyz) < required * required) return true;
            }
            return false;
        }
    }
}
