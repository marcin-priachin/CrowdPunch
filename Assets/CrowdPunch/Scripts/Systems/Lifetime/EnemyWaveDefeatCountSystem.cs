using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Entities;

namespace CrowdPunch.Systems.Lifetime
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(EnemyRecoverySystem))]
    [UpdateAfter(typeof(OutOfBoundsSystem))]
    [UpdateBefore(typeof(DefeatedEnemyLifecycleSystem))]
    public partial struct EnemyWaveDefeatCountSystem : ISystem
    {
        private EntityQuery objectiveEncounters;

        public void OnCreate(ref SystemState state)
        {
            Entity score = state.EntityManager.CreateEntity(typeof(LevelClearScore));
            state.EntityManager.AddBuffer<ScoredDefeat>(score);
            objectiveEncounters = state.GetEntityQuery(new EntityQueryDesc
            {
                Any = new[] { ComponentType.ReadOnly<BossEncounter>(), ComponentType.ReadOnly<ChickenBoss>(),
                    ComponentType.ReadOnly<RollingBoss>(), ComponentType.ReadOnly<DinoBoss>(),
                    ComponentType.ReadOnly<FinaleBoss>(), ComponentType.ReadOnly<Barricade>(),
                    ComponentType.ReadOnly<ProtectedPoint>() }
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            bool scoreEncounter = objectiveEncounters.IsEmpty;
            var score = SystemAPI.GetSingletonRW<LevelClearScore>();
            var scoredDefeats = SystemAPI.GetSingletonBuffer<ScoredDefeat>();
            ComponentLookup<EnemyWaveSequence> sequences = SystemAPI.GetComponentLookup<EnemyWaveSequence>();
            foreach ((RefRW<EnemyWaveOwnership> ownership, RefRO<EnemyLaunchState> launchState,
                         EnabledRefRO<RespawnRequest> respawnRequest, Entity enemy) in
                     SystemAPI.Query<RefRW<EnemyWaveOwnership>, RefRO<EnemyLaunchState>, EnabledRefRO<RespawnRequest>>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess())
            {
                // Replenishing normals can pool directly on leaving defeat bounds, without
                // entering Defeated. Count that absence too; the existing return path restores
                // ownership counters if the elite is still alive when the normal returns.
                if (ownership.ValueRO.DefeatCounted != 0
                    || (launchState.ValueRO.Phase != EnemyLaunchPhase.Defeated && !respawnRequest.ValueRO))
                    continue;
                Entity owner = ownership.ValueRO.Sequence;
                if (!sequences.HasComponent(owner)) continue;
                EnemyWaveSequence sequence = sequences[owner];
                if (ownership.ValueRO.RunGeneration != sequence.RunGeneration)
                    continue;
                ownership.ValueRW.DefeatCounted = 1;
                if (sequence.UndefeatedCount > 0)
                    sequence.UndefeatedCount--;
                if (ownership.ValueRO.WaveIndex == sequence.CurrentWaveIndex)
                    sequence.DefeatedCount++;
                sequences[owner] = sequence;
                if (scoreEncounter && launchState.ValueRO.Phase == EnemyLaunchPhase.Defeated)
                    RecordScore(state.EntityManager, enemy, ref score.ValueRW, scoredDefeats);
            }
        }

        private static void RecordScore(EntityManager entityManager, Entity enemy, ref LevelClearScore score,
            DynamicBuffer<ScoredDefeat> scoredDefeats)
        {
            if (!entityManager.HasComponent<EnemyDamageState>(enemy)
                || entityManager.HasComponent<ArmoredAmmunitionReplacement>(enemy)
                || entityManager.HasComponent<EliteWaveReplenishment>(enemy)
                || entityManager.HasComponent<BossCrowdMember>(enemy)
                || entityManager.HasComponent<BarricadeCrowdMember>(enemy)
                || (entityManager.HasComponent<EnemyRespawnSettings>(enemy)
                    && entityManager.GetComponentData<EnemyRespawnSettings>(enemy).Enabled != 0))
                return;

            score.IsScored = true;
            uint punchSequence = entityManager.GetComponentData<EnemyDamageState>(enemy).DefeatPunchSequence;
            if (punchSequence == 0) return;
            score.Defeats++;
            scoredDefeats.Add(new ScoredDefeat { PunchSequence = punchSequence });
        }
    }
}
