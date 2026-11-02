using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    // PLAYER-004 / COMBAT-012: reacquire along the outgoing rebound, never through its surface.
    internal static class RicochetAimCorrection
    {
        public static bool TryCorrect(EntityManager em, CollisionWorld world, Entity source, Entity surface,
            float3 position, float3 normal, ref float3 velocity, in EnemyLaunchSettings settings,
            ref NativeList<int> nearby, ref NativeList<RaycastHit> rays, out Entity target,
            float forecastTime = 0f, NativeHashSet<Entity> excluded = default)
            => TryCorrect(em, world, source, surface, position, normal, ref velocity, settings, ref nearby, ref rays,
                out target, out _, forecastTime, excluded);

        public static bool TryCorrect(EntityManager em, CollisionWorld world, Entity source, Entity surface,
            float3 position, float3 normal, ref float3 velocity, in EnemyLaunchSettings settings,
            ref NativeList<int> nearby, ref NativeList<RaycastHit> rays, out Entity target, out float3 aimPoint,
            float forecastTime = 0f, NativeHashSet<Entity> excluded = default)
        {
            target = Entity.Null;
            aimPoint = default;
            float radius = settings.PropagationAimCorrectionRadius;
            float angle = settings.PropagationAimMaximumAngleDegrees;
            if (radius <= 0f || angle <= 0f || math.lengthsq(velocity.xz) < .000001f
                || math.dot(velocity.xz, normal.xz) <= .0001f) return false;
            // Preserve boss-specific player-safety deflections and eligibility rules.
            if (em.HasComponent<BossPart>(surface) || em.HasComponent<ChickenBoss>(surface)
                || em.HasComponent<RollingBoss>(surface) || em.HasComponent<FinaleBoss>(surface)) return false;
            nearby.Clear();
            float expanded = radius + (forecastTime > 0f ? settings.AimLeadMaximumDistance : 0f);
            world.OverlapAabb(new OverlapAabbInput { Aabb = new Aabb {
                Min = position - new float3(expanded), Max = position + new float3(expanded) },
                Filter = CollisionFilter.Default }, ref nearby);
            float bestDot = float.MinValue, bestDistance = float.MaxValue;
            float3 bestVelocity = velocity;
            foreach (int index in nearby)
            {
                Entity candidate = world.Bodies[index].Entity;
                if (candidate == source || candidate == surface || excluded.IsCreated && excluded.Contains(candidate)
                    || !em.HasComponent<Enemy>(candidate) && !em.HasComponent<Barricade>(candidate)
                        && !em.HasComponent<FallingPillar>(candidate) && !em.HasComponent<BossEncounter>(candidate)
                        && !em.HasComponent<ChickenBoss>(candidate) && !em.HasComponent<RollingBoss>(candidate)
                    || !em.HasComponent<LocalTransform>(candidate) || !PunchAimAssist.IsValidTarget(em, source, candidate)) continue;
                if (em.HasComponent<EnemyLaunchState>(candidate))
                {
                    var phase = em.GetComponentData<EnemyLaunchState>(candidate).Phase;
                    if (phase != EnemyLaunchPhase.Active && phase != EnemyLaunchPhase.Recovering) continue;
                }
                float3 motion = em.HasComponent<PhysicsVelocity>(candidate)
                    ? em.GetComponentData<PhysicsVelocity>(candidate).Linear : float3.zero;
                float3 point = em.GetComponentData<LocalTransform>(candidate).Position
                    + LaunchAimPrediction.Displacement(motion, math.min(forecastTime, settings.AimLeadSeconds), settings.AimLeadMaximumDistance);
                if (!LaunchAimPrediction.BetterCorrection(velocity, point - position, radius, angle,
                    candidate.Index, target == Entity.Null ? int.MaxValue : target.Index,
                    bestDot, bestDistance, out float dot, out float distance)) continue;
                float3 corrected = LaunchAimPrediction.CorrectVelocity(velocity, position, point, motion,
                    angle, settings.AimLeadSeconds, settings.AimLeadMaximumDistance);
                if (math.dot(corrected.xz, normal.xz) <= .0001f
                    || Obstructed(em, world, source, candidate, position + normal * .003f, point, ref rays, excluded)) continue;
                target = candidate; bestDot = dot; bestDistance = distance; bestVelocity = corrected;
                aimPoint = LaunchAimPrediction.Point(position, point, motion, math.length(velocity.xz),
                    settings.AimLeadSeconds, settings.AimLeadMaximumDistance);
            }
            if (target == Entity.Null) return false;
            velocity = bestVelocity;
            return true;
        }

        private static bool Obstructed(EntityManager em, CollisionWorld world, Entity source, Entity target,
            float3 start, float3 end, ref NativeList<RaycastHit> rays, NativeHashSet<Entity> removed)
        {
            rays.Clear();
            world.CastRay(new RaycastInput { Start = start, End = end, Filter = CollisionFilter.Default }, ref rays);
            foreach (var hit in rays)
                if (hit.Entity != source && hit.Entity != target && !em.HasComponent<Enemy>(hit.Entity)
                    && (!removed.IsCreated || !removed.Contains(hit.Entity))
                    && hit.Material.CollisionResponse != CollisionResponsePolicy.RaiseTriggerEvents
                    && hit.Material.CollisionResponse != CollisionResponsePolicy.None) return true;
            return false;
        }
    }
}
