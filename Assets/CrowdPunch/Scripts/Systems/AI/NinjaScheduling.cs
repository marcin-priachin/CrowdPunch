using CrowdPunch.Components;
using Unity.Mathematics;

namespace CrowdPunch.Systems.AI
{
    public static class NinjaScheduling
    {
        public static float RandomDelay(ref NinjaState state, float minimum, float maximum)
        {
            var random = new Random(state.RandomState == 0 ? 1u : state.RandomState);
            float value = random.NextFloat(math.max(0, math.min(minimum, maximum)), math.max(0, math.max(minimum, maximum)));
            state.RandomState = random.state;
            return value;
        }

        public static void RestartCooldown(ref NinjaState state, in NinjaTuning tuning, double now)
        {
            state.ReadyAt = now + math.max(0, tuning.MinimumCooldown);
            float interval = RandomDelay(ref state, tuning.PeriodicInterval - tuning.PeriodicVariation,
                tuning.PeriodicInterval + tuning.PeriodicVariation);
            state.PeriodicAt = now + math.max(tuning.MinimumCooldown, interval);
            state.ProximityReady = 0;
            state.RetryAt = 0;
        }

        public static bool Ready(in NinjaState state, in NinjaTuning tuning, double now)
        {
            bool periodic = tuning.TriggerMode != NinjaTriggerMode.Proximity && now >= state.PeriodicAt;
            bool proximity = tuning.TriggerMode != NinjaTriggerMode.Periodic && state.ProximityReady != 0;
            return now >= state.ReadyAt && now >= state.RetryAt && (periodic || proximity);
        }

        public static void Interrupt(ref NinjaState state, in NinjaTuning tuning, double now)
        {
            state.Phase = NinjaPhase.Positioning;
            state.SecondsRemaining = 0;
            state.HasDashSegment = 0;
            RestartCooldown(ref state, tuning, now);
        }

        public static void Recover(ref NinjaState state, in NinjaTuning tuning, double now)
        {
            state.Phase = NinjaPhase.Recovering;
            state.SecondsRemaining = math.max(0, tuning.RecoveryDuration);
            RestartCooldown(ref state, tuning, now);
        }
    }
}
