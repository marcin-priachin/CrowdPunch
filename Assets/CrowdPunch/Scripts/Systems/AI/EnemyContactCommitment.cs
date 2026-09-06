using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.AI
{
    /// <summary>Baseline contact cadence: prepare, commit a direction, then return to the surround ring.</summary>
    public static class EnemyContactCommitment
    {
        public static void Cancel(Entity entity, in EnemyContactDamageSettings settings, ref EnemyContactAttemptState state)
        {
            if (state.IsAttempting == 0 && state.IsWindingUp == 0) return;
            state.IsAttempting = 0;
            state.IsWindingUp = 0;
            state.CommittedDirection = float3.zero;
            state.Sequence++;
            state.SecondsRemaining = Interval(entity, state.Sequence, settings);
        }

        public static void Tick(Entity entity, float distance, float3 directionToPlayer, float deltaTime,
            in EnemyContactDamageSettings settings, ref EnemyContactAttemptState state)
        {
            if (state.IsAttempting == 0 && distance > math.max(0f, settings.AttemptDistance))
            {
                Cancel(entity, settings, ref state);
                return;
            }

            state.SecondsRemaining -= math.max(0f, deltaTime);
            if (state.SecondsRemaining > 0f) return;
            if (state.IsAttempting != 0)
            {
                Cancel(entity, settings, ref state);
                return;
            }
            if (state.IsWindingUp == 0 && settings.AttemptWindUpDuration > 0f)
            {
                state.IsWindingUp = 1;
                state.SecondsRemaining = settings.AttemptWindUpDuration;
                return;
            }

            // Sample once at commitment; sidestepping then creates a usable shot window (VISION-002).
            state.IsWindingUp = 0;
            state.IsAttempting = 1;
            state.CommittedDirection = math.normalizesafe(new float3(directionToPlayer.x, 0f, directionToPlayer.z));
            state.SecondsRemaining = math.max(0f, settings.AttemptDuration);
        }

        private static float Interval(Entity entity, uint sequence, in EnemyContactDamageSettings settings)
        {
            float minimum = math.max(0f, math.min(settings.AttemptIntervalMin, settings.AttemptIntervalMax));
            float maximum = math.max(minimum, math.max(settings.AttemptIntervalMin, settings.AttemptIntervalMax));
            uint hash = math.hash(new uint3((uint)math.max(1, entity.Index + 1),
                (uint)math.max(1, entity.Version + 1), sequence + 1u));
            return math.lerp(minimum, maximum, (hash & 0x00ffffffu) / 16777216f);
        }
    }
}
