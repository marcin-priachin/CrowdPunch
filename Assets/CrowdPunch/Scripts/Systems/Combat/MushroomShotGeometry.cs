using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

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
            float3 origin, float3 direction, float distance, float radius, out ColliderCastHit hit)
        {
            var collector = new CoverCollector { Enemies = enemies, MaxFraction = 1f };
            if (distance > .00001f)
                world.SphereCastCustom(origin, radius, direction, distance, ref collector, Filter);
            hit = collector.Hit;
            return collector.NumHits != 0;
        }

        internal static float ClearDistance(in CollisionWorld world, ComponentLookup<Enemy> enemies,
            float3 origin, float3 direction, float distance, float radius)
            => FindCover(world, enemies, origin, direction, distance, radius, out var hit)
                ? distance * hit.Fraction : distance;

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
            public ColliderCastHit Hit;
            public bool EarlyOutOnFirstHit => false;
            public float MaxFraction { get; set; }
            public int NumHits { get; private set; }

            public bool AddHit(ColliderCastHit hit)
            {
                if (Enemies.HasComponent(hit.Entity) || !IsSolid(hit)) return false;
                Hit = hit;
                MaxFraction = hit.Fraction;
                NumHits = 1;
                return true;
            }
        }
    }
}
