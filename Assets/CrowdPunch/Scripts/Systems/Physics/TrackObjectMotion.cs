using CrowdPunch.Components;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Physics
{
    public static class TrackObjectMotion
    {
        // TRACK-003: a changed destination restarts easing from the actual pose, including reversal.
        public static void Retarget(in TrackObject track, ref TrackObjectState state, int step)
        {
            state.TargetStep = math.clamp(step, 0, track.RequiredNetHits);
            state.SlideStart = state.Distance;
            state.Elapsed = 0;
            if (state.Moving == 0) state.SlideSequence++;
            state.Moving = math.abs(track.Length * state.TargetStep / track.RequiredNetHits - state.Distance) <= .00001f ? (byte)0 : (byte)1;
            state.NextDistance = state.Distance;
        }

        public static float Next(in TrackObject track, ref TrackObjectState state, float dt)
        {
            if (state.Locked != 0 || state.Moving == 0) return state.Distance;
            state.Elapsed = math.min(track.SlideDuration, state.Elapsed + dt);
            float t = math.saturate(state.Elapsed / track.SlideDuration);
            float target = track.Length * state.TargetStep / track.RequiredNetHits;
            return math.lerp(state.SlideStart, target, t * t * (3 - 2 * t));
        }
    }
}
