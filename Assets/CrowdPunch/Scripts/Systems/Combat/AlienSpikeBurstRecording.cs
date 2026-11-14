using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Systems.Combat
{
    internal static class AlienSpikeBurstRecording
    {
        internal static bool TryRecord(DynamicBuffer<AlienSpikeBurstHistory> history, in AlienSpikeBurst contact)
        {
            foreach (var previous in history)
            {
                // Per-body launch counters are local. Propagated launches share the initiating punch.
                if (contact.PunchSequence != 0 ? previous.PunchSequence == contact.PunchSequence
                    : previous.Source == contact.Source && previous.LaunchSequence == contact.LaunchSequence
                        && previous.SourceLifetime == contact.SourceLifetime) return false;
            }
            history.Add(new AlienSpikeBurstHistory
            {
                Source = contact.Source, LaunchSequence = contact.LaunchSequence,
                PunchSequence = contact.PunchSequence, SourceLifetime = contact.SourceLifetime
            });
            return true;
        }
    }
}

