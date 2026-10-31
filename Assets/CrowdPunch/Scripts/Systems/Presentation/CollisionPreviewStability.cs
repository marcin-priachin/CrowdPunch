using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Presentation
{
    internal struct CollisionPreviewState
    {
        public Entity Source, Followup;
        public uint SourceLifetime, VictimLifetime, FollowupLifetime, SourceLaunchSequence, Seen;
        public float3 Anchor, DisplayedDirection;
        public double Since;
        public float Alpha, Confidence;
        public byte Confirming;
    }

    internal static class CollisionPreviewStability
    {
        public static bool Update(ref CollisionPreviewState state, in LaunchImpactForecast forecast,
            in CollisionPreviewSettings tuning, double now, float dt, uint seen, out float opacity)
        {
            opacity = 0f;
            bool reused = state.SourceLifetime != forecast.SourceLifetime || state.VictimLifetime != forecast.VictimLifetime
                || state.FollowupLifetime != forecast.FollowupLifetime || state.SourceLaunchSequence != forecast.SourceLaunchSequence;
            if (reused) state.Alpha = 0f;
            bool identityChanged = state.Source != forecast.Source || state.Followup != forecast.Followup
                || state.SourceLifetime != forecast.SourceLifetime || state.VictimLifetime != forecast.VictimLifetime
                || state.FollowupLifetime != forecast.FollowupLifetime
                || state.SourceLaunchSequence != forecast.SourceLaunchSequence;
            float cosine = math.cos(math.radians(math.clamp(tuning.StableAngleDegrees, 0f, 90f)));
            bool bigChange = math.dot(math.normalizesafe(state.DisplayedDirection), forecast.Direction) < cosine - .000001f;
            if (identityChanged || state.Seen == 0 || state.Confirming == 0 && bigChange)
            {
                state.Source = forecast.Source; state.Followup = forecast.Followup;
                state.SourceLifetime = forecast.SourceLifetime; state.VictimLifetime = forecast.VictimLifetime;
                state.FollowupLifetime = forecast.FollowupLifetime;
                state.SourceLaunchSequence = forecast.SourceLaunchSequence;
                state.Anchor = forecast.Direction; state.Since = now; state.Confirming = 1;
                state.Confidence = forecast.Confidence;
            }
            else if (state.Confirming != 0 && math.dot(state.Anchor, forecast.Direction) < cosine - .000001f)
            {
                // Compare against a fixed anchor, so slow continuous drift cannot pass confirmation.
                state.Anchor = forecast.Direction; state.Since = now;
            }
            state.Seen = seen;
            if (state.Confirming != 0)
            {
                state.Alpha = tuning.FadeOutTime <= 0f ? 0f : math.max(0f, state.Alpha - dt / tuning.FadeOutTime);
                if (state.Alpha <= 0f && now - state.Since + .000001 >= tuning.ConfirmationTime)
                {
                    state.DisplayedDirection = forecast.Direction;
                    state.Confirming = 0;
                }
            }
            if (state.Confirming == 0)
            {
                float blend = tuning.SmoothingTime <= 0f ? 1f : 1f - math.exp(-dt / tuning.SmoothingTime);
                state.DisplayedDirection = math.normalizesafe(math.lerp(state.DisplayedDirection, forecast.Direction, blend), forecast.Direction);
                state.Confidence = math.lerp(state.Confidence, forecast.Confidence, blend);
                state.Alpha = tuning.FadeInTime <= 0f ? 1f : math.min(1f, state.Alpha + dt / tuning.FadeInTime);
            }
            opacity = state.Alpha * math.lerp(math.saturate(tuning.MinimumOpacity), 1f, math.saturate(state.Confidence));
            return opacity > .001f;
        }

        public static bool Earlier(in LaunchImpactForecast candidate, in LaunchImpactForecast current)
        {
            if (candidate.ArrivalSeconds != current.ArrivalSeconds) return candidate.ArrivalSeconds < current.ArrivalSeconds;
            if (candidate.Impulse != current.Impulse) return candidate.Impulse > current.Impulse;
            return candidate.Source.Index < current.Source.Index
                || candidate.Source.Index == current.Source.Index && candidate.Source.Version < current.Source.Version;
        }

        public static bool Conflicts(in LaunchImpactForecast first, in LaunchImpactForecast other, in CollisionPreviewSettings tuning)
        {
            return tuning.CompetitionWindow > 0f && other.Victim == first.Victim && other.Source != first.Source
                && other.ArrivalSeconds - first.ArrivalSeconds <= tuning.CompetitionWindow
                && math.clamp(math.dot(first.Direction, other.Direction), -1f, 1f) < math.cos(math.radians(tuning.ConflictAngleDegrees));
        }
    }
}
