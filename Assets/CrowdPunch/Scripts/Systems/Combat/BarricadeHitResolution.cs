using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    internal static class BarricadeHitResolution
    {
        public static bool Allows(BarricadeLaunchSources sources, EnemyLaunchOwner owner)
        {
            var flag = owner == EnemyLaunchOwner.Player ? BarricadeLaunchSources.Player
                : owner == EnemyLaunchOwner.Enemy ? BarricadeLaunchSources.Enemy
                : owner == EnemyLaunchOwner.Boss ? BarricadeLaunchSources.Boss : BarricadeLaunchSources.Unowned;
            return (sources & flag) != 0;
        }

        // BARRICADE-002: impact and blast share source + launch identity; histories are bounded by source lifetimes.
        public static bool TryHit(EntityManager em, Entity target, Entity source, uint sequence, double now, float3 position)
        {
            var wall = em.GetComponentData<Barricade>(target);
            if (wall.HitsRemaining <= 0) return false;
            var history = em.GetBuffer<BarricadeHitHistory>(target);
            for (int i = 0; i < history.Length; i++)
                if (history[i].Source == source && history[i].LaunchSequence == sequence) return false;
            history.Add(new BarricadeHitHistory { Source = source, LaunchSequence = sequence });
            wall.HitsRemaining--;
            wall.HitSequence++;
            wall.LastHitTime = now;
            wall.LastHitPosition = position;
            em.SetComponentData(target, wall);
            if (wall.HitsRemaining == 0)
                em.SetComponentData(target, new PhysicsCollider { Value = wall.BrokenCollider });
            return true;
        }

        public static float3 ClosestPoint(in Barricade wall, in LocalTransform transform, float3 point)
        {
            float3 local = math.rotate(math.inverse(transform.Rotation), point - transform.Position);
            return transform.Position + math.rotate(transform.Rotation, math.clamp(local, -wall.Size * .5f, wall.Size * .5f));
        }

        public static bool ContainsPunch(in Barricade wall, in LocalTransform transform, in PunchSpecification punch)
        {
            if (wall.HitsRemaining <= 0 || math.abs(transform.Position.y - punch.Origin.y) > wall.Size.y * .5f + punch.Radius)
                return false;
            float2 forward = math.normalizesafe(punch.Direction.xz);
            if (math.lengthsq(forward) < .5f) return false;
            float2 side = new float2(forward.y, -forward.x);
            float2 right = math.rotate(transform.Rotation, new float3(1, 0, 0)).xz;
            float2 depth = math.rotate(transform.Rotation, new float3(0, 0, 1)).xz;
            float2 offset = transform.Position.xz - (punch.Origin.xz + forward * punch.Range * .5f);
            return Overlap(forward, offset, forward, side, right, depth, punch, wall)
                && Overlap(side, offset, forward, side, right, depth, punch, wall)
                && Overlap(right, offset, forward, side, right, depth, punch, wall)
                && Overlap(depth, offset, forward, side, right, depth, punch, wall);
        }

        private static bool Overlap(float2 axis, float2 offset, float2 forward, float2 side, float2 right, float2 depth,
            in PunchSpecification punch, in Barricade wall)
            => math.abs(math.dot(offset, axis)) <= math.abs(math.dot(forward, axis)) * punch.Range * .5f
                + math.abs(math.dot(side, axis)) * punch.Radius + math.abs(math.dot(right, axis)) * wall.Size.x * .5f
                + math.abs(math.dot(depth, axis)) * wall.Size.z * .5f;
    }
}
