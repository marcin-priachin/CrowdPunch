using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Systems.Combat
{
    internal static class SporellaImpactRecording
    {
        internal static bool TryRecord(DynamicBuffer<SporellaImpactHistory> history, Entity emitter,
            uint lifetime, in EnemyLaunchState launch)
        {
            var origin = EnemyLaunchTransition.GetFallbackChain(emitter, lifetime, launch);
            var chain = new SporellaImpactHistory
            {
                PunchSequence = launch.OriginatingPunchSequence, Source = origin.Source,
                SourceLifetime = origin.Lifetime, LaunchSequence = origin.Sequence
            };
            foreach (var previous in history)
            {
                bool same = chain.PunchSequence != 0 ? previous.PunchSequence == chain.PunchSequence
                    : previous.PunchSequence == 0 && previous.Source == chain.Source
                        && previous.SourceLifetime == chain.SourceLifetime && previous.LaunchSequence == chain.LaunchSequence;
                if (same) return false;
            }

            history.Add(chain);
            return true;
        }
    }
}
