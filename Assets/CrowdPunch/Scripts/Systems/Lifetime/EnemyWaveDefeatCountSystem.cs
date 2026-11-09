using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Lifetime
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(EnemyRecoverySystem))]
    [UpdateAfter(typeof(OutOfBoundsSystem))]
    [UpdateBefore(typeof(DefeatedEnemyLifecycleSystem))]
    public partial struct EnemyWaveDefeatCountSystem : ISystem
    {
        private EntityQuery objectiveEncounters;

        public void OnCreate(ref SystemState state)
        {
            Entity score = state.EntityManager.CreateEntity(typeof(LevelClearScore), typeof(LevelClearSubject));
            state.EntityManager.AddBuffer<ScoredDefeat>(score);
            objectiveEncounters = state.GetEntityQuery(new EntityQueryDesc
            {
                Any = new[] { ComponentType.ReadOnly<BossEncounter>(), ComponentType.ReadOnly<ChickenBoss>(),
                    ComponentType.ReadOnly<RollingBoss>(), ComponentType.ReadOnly<DinoBoss>(),
                    ComponentType.ReadOnly<FinaleBoss>(), ComponentType.ReadOnly<Barricade>(),
                    ComponentType.ReadOnly<ProtectedPoint>() }
            });
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            bool scoreEncounter = objectiveEncounters.IsEmpty;
            var score = SystemAPI.GetSingletonRW<LevelClearScore>();
            var subject = SystemAPI.GetSingletonRW<LevelClearSubject>();
            var scoredDefeats = SystemAPI.GetSingletonBuffer<ScoredDefeat>();
            ComponentLookup<EnemyWaveSequence> sequences = SystemAPI.GetComponentLookup<EnemyWaveSequence>();
            var health = SystemAPI.GetComponentLookup<Health>(true);
            var waves = SystemAPI.GetBufferLookup<EnemyWaveDefinition>(true);
            NativeList<Entity> pendingDefeats = default;
            NativeHashMap<Entity, int> pendingBySequence = default;
            foreach ((RefRW<EnemyWaveOwnership> ownership, RefRO<EnemyLaunchState> launchState,
                         EnabledRefRO<RespawnRequest> respawnRequest, Entity enemy) in
                     SystemAPI.Query<RefRW<EnemyWaveOwnership>, RefRO<EnemyLaunchState>, EnabledRefRO<RespawnRequest>>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess())
            {
                if (ownership.ValueRO.DefeatCounted != 0) continue;
                bool defeated = launchState.ValueRO.Phase == EnemyLaunchPhase.Defeated;
                bool deferred = scoreEncounter && launchState.ValueRO.Phase == EnemyLaunchPhase.Launched
                    && health.TryGetComponent(enemy, out var hitPoints) && hitPoints.Current <= 0;
                if (!defeated && !respawnRequest.ValueRO && !deferred) continue;

                Entity owner = ownership.ValueRO.Sequence;
                if (!sequences.TryGetComponent(owner, out var sequence)
                    || ownership.ValueRO.RunGeneration != sequence.RunGeneration) continue;

                // A replenishing normal can pool straight from the defeat bounds. Its return
                // path restores the ownership counters while its elite is still alive.
                if (defeated || respawnRequest.ValueRO)
                {
                    CountDefeat(ref ownership.ValueRW, ref sequence);
                    sequences[owner] = sequence;
                    if (scoreEncounter && defeated)
                        RecordScore(state.EntityManager, enemy, ref score.ValueRW, scoredDefeats, ref subject.ValueRW);
                    continue;
                }

                int wave = sequence.CurrentWaveIndex;
                if (sequence.Phase != EnemyWaveRuntimePhase.AwaitingActivation || wave < 0
                    || !waves.TryGetBuffer(owner, out var definitions) || wave != definitions.Length - 1
                    || definitions[wave].ActivationMode == (byte)EnemyWaveActivationMode.DurationElapsed)
                    continue;

                if (!pendingDefeats.IsCreated)
                {
                    pendingDefeats = new NativeList<Entity>(Allocator.Temp);
                    pendingBySequence = new NativeHashMap<Entity, int>(1, Allocator.Temp);
                }
                pendingDefeats.Add(enemy);
                pendingBySequence.TryGetValue(owner, out int count);
                pendingBySequence[owner] = count + 1;
            }

            if (!pendingDefeats.IsCreated) return;
            var owners = SystemAPI.GetComponentLookup<EnemyWaveOwnership>();
            foreach (Entity enemy in pendingDefeats)
            {
                var ownership = owners[enemy];
                Entity owner = ownership.Sequence;
                var sequence = sequences[owner];
                // Wait for every remaining enemy to reach zero before counting launched bodies.
                // Until then their normal wave accounting and ammunition behavior still apply.
                if (pendingBySequence[owner] < sequence.UndefeatedCount) continue;
                CountDefeat(ref ownership, ref sequence);
                owners[enemy] = ownership;
                sequences[owner] = sequence;
                RecordScore(state.EntityManager, enemy, ref score.ValueRW, scoredDefeats, ref subject.ValueRW);
            }
            pendingBySequence.Dispose();
            pendingDefeats.Dispose();
        }

        private static void CountDefeat(ref EnemyWaveOwnership ownership, ref EnemyWaveSequence sequence)
        {
            ownership.DefeatCounted = 1;
            if (sequence.UndefeatedCount > 0)
                sequence.UndefeatedCount--;
            if (ownership.WaveIndex == sequence.CurrentWaveIndex)
                sequence.DefeatedCount++;
        }

        private static void RecordScore(EntityManager entityManager, Entity enemy, ref LevelClearScore score,
            DynamicBuffer<ScoredDefeat> scoredDefeats, ref LevelClearSubject subject)
        {
            if (!entityManager.HasComponent<EnemyDamageState>(enemy)) return;
            var damage = entityManager.GetComponentData<EnemyDamageState>(enemy);
            // Capture the root before the defeated enemy can be pooled or destroyed.
            float3 position = entityManager.HasComponent<LocalTransform>(enemy)
                ? entityManager.GetComponentData<LocalTransform>(enemy).Position : new float3(float.NaN);
            if (math.all(math.isfinite(position)) && (subject.Entity == Entity.Null || damage.DefeatedAt > subject.DefeatedAt
                || damage.DefeatedAt == subject.DefeatedAt && enemy.Index > subject.Entity.Index))
                subject = new LevelClearSubject { Entity = enemy, Position = position, DefeatedAt = damage.DefeatedAt };

            if (entityManager.HasComponent<ArmoredAmmunitionReplacement>(enemy)
                || entityManager.HasComponent<EliteWaveReplenishment>(enemy)
                || entityManager.HasComponent<BossCrowdMember>(enemy)
                || entityManager.HasComponent<BarricadeCrowdMember>(enemy)
                || (entityManager.HasComponent<EnemyRespawnSettings>(enemy)
                    && entityManager.GetComponentData<EnemyRespawnSettings>(enemy).Enabled != 0))
                return;

            score.IsScored = true;
            if (damage.DefeatPunchSequence == 0) return;
            score.Defeats++;
            scoredDefeats.Add(new ScoredDefeat { PunchSequence = damage.DefeatPunchSequence, WorldPosition = position });
        }
    }
}
