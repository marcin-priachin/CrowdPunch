using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    internal static class SnakePunchOcclusion
    {
        public static bool CanReach(EntityManager em, Entity target, float3 origin)
        {
            if (!em.HasComponent<SnakeSegment>(target)) return true;
            var segment = em.GetComponentData<SnakeSegment>(target);
            Entity head = segment.Head;
            if (!em.HasComponent<SnakeHead>(head) || !em.HasComponent<LocalTransform>(head)
                || em.GetComponentData<SnakeHead>(head).Generation != segment.Generation) return true;
            var pose = em.GetComponentData<LocalTransform>(head);
            float3 end = em.GetComponentData<LocalTransform>(target).Position;
            // Player punches use a horizontal footprint. Test the head's body at its mid-height,
            // using current transforms rather than the previous physics world's positions.
            origin.y = end.y = pose.Position.y;
            if (em.HasComponent<PhysicsCollider>(head))
            {
                var collider = em.GetComponentData<PhysicsCollider>(head);
                if (collider.IsValid)
                {
                    var inverse = math.inverse(new RigidTransform(pose.Rotation, pose.Position));
                    return !collider.Value.Value.CastRay(new RaycastInput
                    {
                        Start = math.transform(inverse, origin) / pose.Scale,
                        End = math.transform(inverse, end) / pose.Scale,
                        Filter = CollisionFilter.Default
                    });
                }
            }
            if (!em.HasComponent<EnemyContactDamageSettings>(head)) return true;
            float2 delta = end.xz - origin.xz;
            float t = math.saturate(math.dot(pose.Position.xz - origin.xz, delta)
                / math.max(.000001f, math.lengthsq(delta)));
            float radius = em.GetComponentData<EnemyContactDamageSettings>(head).ContactRadius;
            return math.distancesq(origin.xz + delta * t, pose.Position.xz) > radius * radius;
        }
    }
}
