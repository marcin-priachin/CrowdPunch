using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Utilities
{
    /// <summary>Returns a physically clear body from a conservative navigation envelope to a valid route.</summary>
    public static class NavigationPhysicalEscape
    {
        public static bool TryFind(ref NavigationGridBlob grid, CollisionWorld world, Entity enemy,
            LocalTransform pose, PhysicsCollider collider, ComponentLookup<Enemy> enemies, int clearance, out float2 goal)
        {
            goal = pose.Position.xz;
            if (clearance < 0 || !collider.IsValid) return false;
            int2 center = math.clamp((int2)math.floor((goal - grid.Minimum) / grid.CellSize), int2.zero, grid.Size - 1);
            float best = float.MaxValue;
            // Large rock cutouts can be several cells deep. Search bounded rings rather than
            // requiring the body to already have a valid grid anchor or using a teleport.
            for (int ring = 0; ring <= 16; ring++)
            {
                for (int z = -ring; z <= ring; z++)
                for (int x = -ring; x <= ring; x++)
                {
                    if (math.max(math.abs(x), math.abs(z)) != ring) continue;
                    int2 cell = center + new int2(x, z);
                    if (math.any(cell < 0) || math.any(cell >= grid.Size)) continue;
                    int index = cell.y * grid.Size.x + cell.x;
                    if (NavigationGeometry.Region(ref grid, index, clearance) == 0) continue;
                    float2 candidate = NavigationGeometry.Center(ref grid, index);
                    float distance = math.distancesq(pose.Position.xz, candidate);
                    if (distance >= best || !Clear(world, enemy, pose, collider, enemies, candidate)) continue;
                    best = distance; goal = candidate;
                }
                // No more distant ring can improve this candidate.
                if (best < math.square(math.max(0, ring - 1) * grid.CellSize)) break;
            }
            return best < float.MaxValue;
        }

        public static bool Clear(CollisionWorld world, Entity enemy, LocalTransform pose,
            PhysicsCollider collider, ComponentLookup<Enemy> enemies, float2 goal)
        {
            if (!collider.IsValid) return false;
            float3 end = new float3(goal.x, pose.Position.y, goal.y);
            var collector = new EnvironmentCollector { Enemies = enemies, Self = enemy, Direction = end - pose.Position };
            return !world.CastCollider(new ColliderCastInput(collider.Value, pose.Position, end, pose.Rotation, pose.Scale), ref collector);
        }

        private struct EnvironmentCollector : ICollector<ColliderCastHit>
        {
            [ReadOnly] public ComponentLookup<Enemy> Enemies;
            public Entity Self;
            public float3 Direction;
            public bool EarlyOutOnFirstHit => true;
            public float MaxFraction => 1;
            public int NumHits { get; private set; }
            public bool AddHit(ColliderCastHit hit)
            {
                if (hit.Entity == Self || Enemies.HasComponent(hit.Entity)
                    || hit.Material.CollisionResponse == CollisionResponsePolicy.RaiseTriggerEvents) return false;
                // Resting floor/side contacts must allow tangent or outward movement. A contact
                // farther along the sweep still blocks, including another face of the same rock.
                if (hit.Fraction <= .0001f && math.dot(Direction, hit.SurfaceNormal) >= -1e-5f) return false;
                NumHits++; return true;
            }
        }
    }
}
