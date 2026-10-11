using Unity.Collections;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Utilities
{
    internal static class EnemyGroundSpawnClearance
    {
        public static bool IsClear(CollisionWorld world, float3 position, float radius, float surfaceHeight)
        {
            var hits = new NativeList<DistanceHit>(Allocator.Temp);
            world.CalculateDistance(new PointDistanceInput
            {
                Position = new float3(position.x, surfaceHeight + radius + .02f, position.z),
                MaxDistance = radius,
                Filter = CollisionFilter.Default
            }, ref hits);
            bool clear = true;
            foreach (var hit in hits)
                if (hit.RigidBodyIndex >= world.NumDynamicBodies) { clear = false; break; }
            hits.Dispose();
            return clear;
        }
    }
}
