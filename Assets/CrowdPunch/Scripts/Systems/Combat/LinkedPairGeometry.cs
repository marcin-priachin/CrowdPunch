using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Combat
{
    public static class LinkedPairGeometry
    {
        public static float3 Closest(float3 a, float3 b, float3 p)
        {
            float2 edge = b.xz - a.xz;
            float t = math.saturate(math.dot(p.xz - a.xz, edge) / math.max(.000001f, math.lengthsq(edge)));
            return math.lerp(a, b, t);
        }

        public static bool Blocked(CollisionWorld world, ComponentLookup<Enemy> enemies, float3 a, float3 b)
        {
            float2 delta = b.xz - a.xz; float length = math.length(delta);
            return length > .001f && DragonFlameGeometry.Reach(world, enemies, Entity.Null,
                a.xz, delta / length, length, (a.y + b.y) * .5f) < length - .005f;
        }

        public static bool Sweep(float3 oldA, float3 oldB, float3 a, float3 b, float3 oldVictim, float3 victim,
            float radius, CollisionWorld world, ComponentLookup<Enemy> enemies, out float3 contact, out float3 motion)
        {
            float travel = math.max(math.distance(oldA, a), math.distance(oldB, b)) + math.distance(oldVictim, victim);
            // Conservative capsule inflation bounds the gap between samples, including very fast sweeps.
            int samples = math.clamp((int)math.ceil(travel / math.max(.02f, radius * .25f)), 1, 512);
            float reach = radius + travel / samples * .5f;
            contact = motion = default;
            for (int i = 0; i <= samples; i++) {
                float t = i / (float)samples;
                float3 x = math.lerp(oldA, a, t), y = math.lerp(oldB, b, t), p = math.lerp(oldVictim, victim, t);
                float3 closest = Closest(x, y, p);
                if (math.abs(p.y - closest.y) > radius || math.distancesq(closest.xz, p.xz) > reach * reach || Blocked(world, enemies, x, y)) continue;
                contact = closest;
                float along = math.saturate(math.dot(closest.xz - x.xz, y.xz - x.xz) / math.max(.000001f, math.lengthsq(y.xz - x.xz)));
                motion = math.lerp(a - oldA, b - oldB, along); motion.y = 0;
                return true;
            }
            return false;
        }

        public static bool Tick(DynamicBuffer<LinkedPairClock> clocks, Entity victim, uint lifetime, double now, float interval)
        {
            for (int i = 0; i < clocks.Length; i++) {
                var clock = clocks[i];
                if (clock.Victim != victim || clock.Lifetime != lifetime) continue;
                if (now + 1e-6 < clock.NextHitAt) return false;
                clock.NextHitAt = now + math.max(.01f, interval); clocks[i] = clock; return true;
            }
            clocks.Add(new LinkedPairClock { Victim = victim, Lifetime = lifetime, NextHitAt = now + math.max(.01f, interval) }); return true;
        }
    }
}
