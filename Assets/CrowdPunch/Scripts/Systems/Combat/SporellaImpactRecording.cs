using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Combat
{
    internal static class SporellaImpactRecording
    {
        internal static bool TryRecord(DynamicBuffer<SporellaImpactHistory> history,
            DynamicBuffer<SporellaImpactContact> contacts, in SporellaSettings settings, bool outgoing,
            in SporellaImpactHistory chain, float3 position, float3 direction, int depth)
        {
            if (!settings.CanTrigger(outgoing)) return false;
            foreach (var previous in history)
            {
                bool same = chain.PunchSequence != 0 ? previous.PunchSequence == chain.PunchSequence
                    : previous.PunchSequence == 0 && previous.Source == chain.Source
                        && previous.SourceLifetime == chain.SourceLifetime && previous.LaunchSequence == chain.LaunchSequence;
                if (same) return false;
            }

            history.Add(chain);
            contacts.Add(new SporellaImpactContact
            {
                Position = position, Direction = direction, PunchSequence = chain.PunchSequence, ChainDepth = depth
            });
            return true;
        }
    }
}
