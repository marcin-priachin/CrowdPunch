using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Combat
{
    internal static class SporellaShotGeometry
    {
        private const float MuzzleForward = 1.24f; // Grounded frame 10 at 2x model scale.
        private const float LaunchAimDistance = 10f;

        internal static SporellaSweep Aim(float3 position, float3 target, float3 fallback, float shotHeight,
            in SporellaSettings settings)
        {
            float3 forward = MushroomShotGeometry.Horizontal(target - position, fallback);
            float distance = math.distance(target.xz, position.xz);
            float nozzle = math.min(MuzzleForward, distance);
            position.y = shotHeight;
            return CreateSweep(position + forward * nozzle, forward, distance - nozzle, settings);
        }

        internal static SporellaSweep AimAlongLaunch(float3 position, float3 direction, float shotHeight,
            in SporellaSettings settings)
        {
            position.y = shotHeight;
            return CreateSweep(position, direction, LaunchAimDistance, settings);
        }

        private static SporellaSweep CreateSweep(float3 origin, float3 forward, float distance, in SporellaSettings settings)
            => new SporellaSweep
            {
                Origin = origin,
                Forward = forward,
                AimDistance = math.max(.01f, distance),
                HalfWidth = math.max(0f, settings.SweepHalfWidth),
                ShotsPerPass = math.max(1, settings.ShotsPerPass),
                Passes = math.max(1, settings.SweepPasses)
            };

        internal static float3 ShotDirection(in SporellaSweep sweep, int shotIndex, out float distance)
        {
            int lane = shotIndex % sweep.ShotsPerPass;
            float lateral = sweep.ShotsPerPass == 1 ? 0f
                : math.lerp(-sweep.HalfWidth, sweep.HalfWidth, (float)lane / (sweep.ShotsPerPass - 1));
            float3 target = sweep.Forward * sweep.AimDistance + math.cross(math.up(), sweep.Forward) * lateral;
            distance = math.length(target);
            return target / distance;
        }

        internal static float Height(float ground, float radius, in SporellaSettings settings)
            => ground + math.max(radius + .02f, settings.Height);

        internal static bool HasClearShot(in CollisionWorld world, ComponentLookup<Enemy> enemies, in SporellaSweep sweep,
            float range, float radius, float ground)
        {
            // Every pass repeats the same lanes. Partial cover can still intercept individual shots.
            for (int i = 0; i < sweep.ShotsPerPass; i++)
            {
                float3 direction = ShotDirection(sweep, i, out float distance);
                if (distance <= range && !MushroomShotGeometry.FindCover(world, enemies, sweep.Origin,
                    direction, distance, radius, ground, out _)) return true;
            }

            return false;
        }

        internal static bool FindCover(in CollisionWorld world, ComponentLookup<Enemy> enemies, float3 start, float3 end,
            float radius, float ground, out ColliderCastHit hit)
        {
            float3 delta = end - start;
            float length = math.length(delta);
            return MushroomShotGeometry.FindCover(world, enemies, start, delta / math.max(.00001f, length),
                length, radius, ground, out hit);
        }
    }
}
