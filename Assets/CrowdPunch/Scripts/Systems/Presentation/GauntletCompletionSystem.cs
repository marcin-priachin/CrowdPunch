using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Systems.Groups;
using Unity.Entities;

namespace CrowdPunch.Systems.Presentation
{
    /// <summary>Reports completion once when every wave sequence in the loaded gauntlet is complete.</summary>
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial class GauntletCompletionSystem : SystemBase
    {
        private EntityQuery allSequences;
        private EntityQuery completedSequences;
        private bool completionReported;
        private bool failureReported;

        protected override void OnCreate()
        {
            allSequences = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<EnemyWaveSequence>(),
                    ComponentType.ReadOnly<EnemyWaveEncounterComplete>()
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            });
            completedSequences = GetEntityQuery(
                ComponentType.ReadOnly<EnemyWaveSequence>(),
                ComponentType.ReadOnly<EnemyWaveEncounterComplete>());
        }

        protected override void OnUpdate()
        {
            bool failed = SystemAPI.HasSingleton<ProtectedPoint>() && SystemAPI.GetSingleton<ProtectedPoint>().Failed;
            if (failed)
            {
                if (!failureReported) GauntletFailureRegistry.ReportFailure();
                failureReported = true;
                completionReported = false;
                return;
            }
            failureReported = false;
            int sequenceCount = allSequences.CalculateEntityCount();
            bool isComplete = sequenceCount > 0
                && completedSequences.CalculateEntityCount() == sequenceCount;
            // BOSS-007: head defeat is authoritative even if supporting waves are cleared or mis-signalled.
            foreach (var boss in SystemAPI.Query<RefRO<BossEncounter>>())
                isComplete = boss.ValueRO.Cycle == BossCycle.Defeated;
            foreach (var boss in SystemAPI.Query<RefRO<ChickenBoss>>())
                isComplete = boss.ValueRO.Phase == ChickenPhase.Defeated;
            foreach (var boss in SystemAPI.Query<RefRO<RollingBoss>>())
                isComplete = boss.ValueRO.Phase == RollingPhase.Defeated;

            // BARRICADE-001: surviving enemies are irrelevant; reaching the exposed exit is required.
            foreach (var wall in SystemAPI.Query<RefRO<Barricade>>())
            {
                var player = SystemAPI.HasSingleton<PlayerSnapshot>() ? SystemAPI.GetSingleton<PlayerSnapshot>() : default;
                isComplete = wall.ValueRO.HitsRemaining == 0 && (wall.ValueRO.CompleteOnDestruction != 0 || player.IsAvailable
                    && Unity.Mathematics.math.distancesq(player.Position.xz, wall.ValueRO.ExitPosition.xz)
                        <= wall.ValueRO.ExitRadius * wall.ValueRO.ExitRadius);
            }

            if (isComplete && !completionReported)
            {
                completionReported = true;
                GauntletCompletionRegistry.ReportCompletion();
            }
            else if (!isComplete)
            {
                completionReported = false;
            }
        }
    }
}
