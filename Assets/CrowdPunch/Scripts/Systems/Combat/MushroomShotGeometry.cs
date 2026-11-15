using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    internal static class MushroomShotGeometry
    {
        internal static readonly CollisionFilter Filter = new CollisionFilter
        {
            BelongsTo = uint.MaxValue, CollidesWith = uint.MaxValue
        };

        internal static bool IsSolid(in ColliderCastHit hit)
            => hit.Material.CollisionResponse != CollisionResponsePolicy.None
               && hit.Material.CollisionResponse != CollisionResponsePolicy.RaiseTriggerEvents;

        internal static bool FindCover(in CollisionWorld world, ComponentLookup<Enemy> enemies,
            float3 origin, float3 direction, float distance, float radius, float ground, out ColliderCastHit hit)
        {
            var collector = new CoverCollector { Enemies = enemies, MaxFraction = 1f, Ground = ground };
            if (distance > .00001f)
                world.SphereCastCustom(origin, radius, direction, distance, ref collector, Filter);
            hit = collector.Hit;
            return collector.NumHits != 0;
        }

        internal static float ClearDistance(in CollisionWorld world, ComponentLookup<Enemy> enemies,
            float3 origin, float3 direction, float distance, float radius, float ground)
            => FindCover(world, enemies, origin, direction, distance, radius, ground, out var hit)
                ? distance * hit.Fraction : distance;

        internal static float3 Aim(in LocalTransform pose, float3 target, float fallbackHeight,
            bool hasSource, in MushroomShotSource source, out float3 origin)
        {
            float3 toward = Horizontal(target - pose.Position, math.forward(pose.Rotation));
            origin = pose.Position;
            origin.y = fallbackHeight;
            if (!hasSource) return toward;

            float3 offset = source.LocalPosition * pose.Scale;
            // Align the shot with the target even if the authored source is offset sideways.
            float distance = math.distance(target.xz, pose.Position.xz);
            float lateral = math.clamp(offset.x / math.max(.001f, distance), -1f, 1f);
            float3 direction = toward * math.sqrt(1f - lateral * lateral) - math.cross(math.up(), toward) * lateral;
            origin = pose.Position + math.mul(quaternion.LookRotationSafe(direction, math.up()), offset);
            return direction;
        }

        internal static float3 Horizontal(float3 direction, float3 fallback)
        {
            direction.y = 0f;
            return math.normalizesafe(direction, fallback);
        }

        internal static bool SweepPlayer(float3 start, float3 displacement, float3 center, float radius, out float fraction)
        {
            float3 offset = start - center;
            float c = math.lengthsq(offset) - radius * radius;
            fraction = 0f;
            if (c <= 0f) return true;
            float a = math.lengthsq(displacement), b = math.dot(offset, displacement);
            float discriminant = b * b - a * c;
            if (a < .000001f || discriminant < 0f) return false;
            fraction = (-b - math.sqrt(discriminant)) / a;
            return fraction >= 0f && fraction <= 1f;
        }

        private struct CoverCollector : ICollector<ColliderCastHit>
        {
            public ComponentLookup<Enemy> Enemies;
            public float Ground;
            public ColliderCastHit Hit;
            public bool EarlyOutOnFirstHit => false;
            public float MaxFraction { get; set; }
            public int NumHits { get; private set; }

            public bool AddHit(ColliderCastHit hit)
            {
                // The low muzzle can overlap the floor. Only the floor-facing surface is exempt.
                if (Enemies.HasComponent(hit.Entity) || !IsSolid(hit)
                    || hit.SurfaceNormal.y > .7f && hit.Position.y <= Ground + .015f) return false;
                Hit = hit;
                MaxFraction = hit.Fraction;
                NumHits = 1;
                return true;
            }
        }
    }
}
