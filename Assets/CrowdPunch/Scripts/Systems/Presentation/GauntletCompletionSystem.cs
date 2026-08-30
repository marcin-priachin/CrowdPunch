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
            int sequenceCount = allSequences.CalculateEntityCount();
            bool isComplete = sequenceCount > 0
                && completedSequences.CalculateEntityCount() == sequenceCount;

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
