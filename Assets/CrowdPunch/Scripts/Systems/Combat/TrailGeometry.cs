using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Combat
{
    public static class TrailGeometry
    {
        public static float2 Closest(float2 start, float2 end, float2 point)
        {
            float2 delta = end - start;
            return start + delta * math.saturate(math.dot(point - start, delta) / math.max(.000001f, math.lengthsq(delta)));
        }

        public static bool Overlaps(in TrailSection section, float3 point, float radius) =>
            math.distancesq(Closest(section.Start.xz, section.End.xz, point.xz), point.xz)
                <= math.pow(math.max(0, section.Width) * .5f + math.max(0, radius), 2);

        public static bool CanDamage(in TrailSection section, Entity target, uint lifetime, bool trailEnemy, bool armored)
        {
            if (section.DamagesEnemies == 0 || armored) return false;
            if (!trailEnemy || section.Immunity == TrailImmunityMode.None) return true;
            return section.Immunity != TrailImmunityMode.AllTrailEnemies &&
                (section.Source != target || section.SourceLifetime != lifetime);
        }
    }
}
