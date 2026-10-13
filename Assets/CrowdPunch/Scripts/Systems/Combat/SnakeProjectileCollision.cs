using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Combat
{
    internal static class SnakeProjectileCollision
    {
        public static bool IgnoresOwner(EntityManager em, Entity source, Entity other)
        {
            if (!em.HasComponent<SnakeSegment>(source)) return false;
            var segment = em.GetComponentData<SnakeSegment>(source);
            return segment.BouncedOutsideOwner == 0 && (other == segment.Head
                || em.HasComponent<SnakeSegment>(other) && em.GetComponentData<SnakeSegment>(other).Head == segment.Head
                    && em.GetComponentData<SnakeSegment>(other).Generation == segment.Generation);
        }

        // Swept barricade/cover rebounds can happen without a discrete solver event.
        public static void RecordRebound(EntityManager em, Entity source, float3 incoming, float3 normal)
        {
            if (!em.HasComponent<SnakeSegment>(source) || !em.HasComponent<PhysicsMass>(source)) return;
            var segment = em.GetComponentData<SnakeSegment>(source);
            float inverseMass = em.GetComponentData<PhysicsMass>(source).InverseMass;
            float impulse = inverseMass > .0001f ? math.abs(math.dot(incoming,normal)) / inverseMass : 0;
            RecordExternalBounce(ref segment,incoming,normal,impulse);
            em.SetComponentData(source,segment);
        }

        // SNAKE-003: departure distance and resting floor contacts cannot arm a return shot.
        public static void RecordExternalBounce(ref SnakeSegment segment, float3 incomingVelocity,
            float3 normal, float impulse)
        {
            if (segment.Detached == 0 || segment.BouncedOutsideOwner != 0) return;
            if (impulse > .0001f && math.abs(math.dot(incomingVelocity.xz, normal.xz)) > .1f)
                segment.BouncedOutsideOwner = 1;
        }
    }
}
