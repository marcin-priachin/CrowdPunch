using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Utilities
{
    public static class NinjaSolidSweep
    {
        // Match the authored 2m capsule, including obstacles between its endpoint spheres.
        public static float Distance(CollisionWorld world, ComponentLookup<Enemy> enemies, Entity self,
            float3 origin, float3 direction, float distance, float radius)
        {
            var hits = new NativeList<ColliderCastHit>(Allocator.Temp);
            float allowed = distance;
            float halfSegment = math.max(0, 1 - radius);
            world.CapsuleCastAll(origin + new float3(0, -halfSegment + .02f, 0),
                origin + new float3(0, halfSegment + .02f, 0), radius,
                direction, distance, ref hits, CollisionFilter.Default, QueryInteraction.IgnoreTriggers);
            foreach (var hit in hits)
            {
                if (hit.Entity == self || enemies.HasComponent(hit.Entity)
                    || hit.SurfaceNormal.y > .5f && hit.Position.y <= origin.y - .95f) continue;
                allowed = math.min(allowed, math.max(0, hit.Fraction * distance - .01f));
            }
            hits.Dispose();
            return allowed;
        }

        public static bool Clear(CollisionWorld world, ComponentLookup<Enemy> enemies, Entity self, float3 position, float radius)
        {
            var hits = new NativeList<DistanceHit>(Allocator.Temp);
            float halfSegment = math.max(0, 1 - radius);
            world.OverlapCapsule(position + new float3(0, -halfSegment + .02f, 0),
                position + new float3(0, halfSegment + .02f, 0), radius, ref hits,
                CollisionFilter.Default, QueryInteraction.IgnoreTriggers);
            foreach (var hit in hits)
                if (hit.Entity != self && !enemies.HasComponent(hit.Entity)
                    && !(hit.SurfaceNormal.y > .5f && hit.Position.y <= position.y - .95f))
                { hits.Dispose(); return false; }
            hits.Dispose();
            return true;
        }
    }
}
