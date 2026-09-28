using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateBefore(typeof(BarricadeImpactSystem))]
    public partial struct RotatingCoverSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (cover, motion, transform) in SystemAPI.Query<RefRO<RotatingCover>, RefRW<RotatingCoverState>, RefRW<LocalTransform>>())
            {
                if (!SystemAPI.HasComponent<Barricade>(cover.ValueRO.Target)) continue;
                var target = SystemAPI.GetComponent<Barricade>(cover.ValueRO.Target);
                if (target.HitsRemaining <= 0) continue;
                Advance(cover.ValueRO, ref motion.ValueRW, target.HitSequence, SystemAPI.Time.DeltaTime);
                transform.ValueRW.Rotation = quaternion.RotateY(motion.ValueRO.Angle);
            }
        }

        public static void Advance(in RotatingCover c, ref RotatingCoverState s, uint hits, float dt)
        {
            if (s.ObservedHit != hits && c.HitResponse == CoverHitResponse.Pause)
                s.HitPauseRemaining = c.HitPauseSeconds;
            s.ObservedHit = hits;
            float paused = math.min(dt, s.HitPauseRemaining);
            s.HitPauseRemaining -= paused;
            dt -= paused;
            float speed = c.RadiansPerSecond * (c.HitResponse == CoverHitResponse.Accelerate ? 1 + hits * c.SpeedIncreasePerHit : 1);
            // Integrate across boundaries so modes are independent of the fixed-step size.
            while (dt > .000001f)
            {
                float period = c.Mode == CoverRotationMode.RotateAndPause ? c.RotateSeconds + c.PauseSeconds
                    : c.Mode == CoverRotationMode.ReversePeriodically ? 2 * c.ReverseSeconds : float.MaxValue;
                float boundary = c.Mode == CoverRotationMode.RotateAndPause && s.CycleTime < c.RotateSeconds ? c.RotateSeconds
                    : c.Mode == CoverRotationMode.ReversePeriodically && s.CycleTime < c.ReverseSeconds ? c.ReverseSeconds : period;
                float step = math.min(dt, math.max(.000001f, boundary - s.CycleTime));
                float direction = c.Mode == CoverRotationMode.RotateAndPause && s.CycleTime >= c.RotateSeconds ? 0
                    : c.Mode == CoverRotationMode.ReversePeriodically && s.CycleTime >= c.ReverseSeconds ? -1 : 1;
                s.Angle = math.fmod(s.Angle + direction * speed * step, 2 * math.PI);
                s.CycleTime += step;
                if (s.CycleTime >= period - .000001f) s.CycleTime = 0;
                dt -= step;
            }
        }
    }
}
