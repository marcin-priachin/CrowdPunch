using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Combat
{
    internal static class LaunchAimPrediction
    {
        public static float3 Displacement(float3 velocity, float seconds, float maximumDistance)
        {
            float3 delta = new float3(velocity.x, 0f, velocity.z) * math.max(0f, seconds);
            float length = math.length(delta);
            return delta * math.min(1f, math.max(0f, maximumDistance) / math.max(.0001f, length));
        }

        public static float3 Point(float3 source, float3 target, float3 velocity, float speed,
            float maximumSeconds, float maximumDistance)
        {
            if (speed <= .0001f || maximumSeconds <= 0f) return target;
            float time = math.min(math.distance(source.xz, target.xz) / speed, maximumSeconds);
            // One refinement handles lateral travel without an unstable intercept root for fast targets.
            float3 point = target + Displacement(velocity, time, maximumDistance);
            time = math.min(math.distance(source.xz, point.xz) / speed, maximumSeconds);
            return target + Displacement(velocity, time, maximumDistance);
        }

        public static void UpdateLock(ref PunchAimAssistTarget aim, Entity preferred, bool retain,
            double now, float delay)
        {
            if (!retain)
            {
                aim.Target = preferred;
                aim.Challenger = Entity.Null;
            }
            else if (preferred == Entity.Null || preferred == aim.Target)
                aim.Challenger = Entity.Null;
            else
            {
                if (aim.Challenger != preferred)
                {
                    aim.Challenger = preferred;
                    aim.ChallengerSince = now;
                }
                if (now - aim.ChallengerSince + .000001 >= math.max(0f, delay))
                {
                    aim.Target = preferred;
                    aim.Challenger = Entity.Null;
                }
            }
            aim.IsAiming = 1;
        }

        public static bool WithinAngle(float3 direction, float3 offset, float maximumDegrees)
        {
            return maximumDegrees > 0f && math.lengthsq(offset.xz) > .000001f
                && math.dot(math.normalizesafe(direction.xz), math.normalizesafe(offset.xz))
                    >= math.cos(math.radians(math.clamp(maximumDegrees, 0f, 180f))) - .000001f;
        }

        public static void StopHoming(ref EnemyLaunchState launch)
        {
            launch.HomingTarget = Entity.Null;
            launch.HomingSecondsRemaining = 0f;
        }
    }

    // Other crowd bodies remain real collisions; fixed cover must not attract steering through itself.
    internal struct LaunchObstructionCollector : ICollector<RaycastHit>
    {
        public Entity Source, Target;
        public ComponentLookup<Enemy> Enemies;
        public ComponentLookup<RotatingCover> Covers;
        public ComponentLookup<CoverEnclosure> Enclosures;
        public bool IgnoreMovingCovers;
        public bool EarlyOutOnFirstHit => true;
        public float MaxFraction => 1f;
        public int NumHits { get; private set; }
        public bool AddHit(RaycastHit hit)
        {
            if (hit.Entity == Source || hit.Entity == Target || Enemies.HasComponent(hit.Entity)
                || IgnoreMovingCovers && (Covers.HasComponent(hit.Entity) || Enclosures.HasComponent(hit.Entity))
                || hit.Material.CollisionResponse == CollisionResponsePolicy.RaiseTriggerEvents
                || hit.Material.CollisionResponse == CollisionResponsePolicy.None) return false;
            NumHits = 1;
            return true;
        }
    }
}
