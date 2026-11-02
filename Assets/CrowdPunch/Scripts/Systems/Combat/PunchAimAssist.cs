using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>Validates locked targets and supplies the angle-based fallback used at aim start.</summary>
    internal static class PunchAimAssist
    {
        public static bool TryGetFallbackTarget(EntityManager manager, Entity source, float3 sourcePosition,
            float3 committedDirection, float range, float maximumAngleDegrees,
            NativeArray<Entity> candidates, out Entity target)
        {
            target = Entity.Null;
            float maximumDistance = math.max(0f, range);
            float3 forward = committedDirection;
            forward.y = 0f;
            forward = math.normalizesafe(forward);
            if (maximumDistance <= 0f || math.lengthsq(forward) <= 0f) return false;

            float bestDot = float.MinValue;
            float bestDistanceSq = float.MaxValue;
            float maximumDistanceSq = maximumDistance * maximumDistance;
            float minimumDot = math.cos(math.radians(math.clamp(maximumAngleDegrees, 0f, 180f)));
            for (int i = 0; i < candidates.Length; i++)
            {
                Entity candidate = candidates[i];
                if (!IsValidTarget(manager, source, candidate)) continue;
                float3 offset = manager.GetComponentData<LocalTransform>(candidate).Position - sourcePosition;
                offset.y = 0f;
                float distanceSq = math.lengthsq(offset);
                if (distanceSq <= 0.000001f || distanceSq > maximumDistanceSq) continue;

                float dot = math.dot(forward, offset * math.rsqrt(distanceSq));
                if (dot < minimumDot) continue;
                if (dot > bestDot
                    || dot == bestDot && distanceSq < bestDistanceSq
                    || dot == bestDot && distanceSq == bestDistanceSq
                    && (target == Entity.Null || candidate.Index < target.Index))
                {
                    target = candidate;
                    bestDot = dot;
                    bestDistanceSq = distanceSq;
                }
            }
            return target != Entity.Null;
        }

        public static bool TryGetLockedDirection(EntityManager manager, Entity source, float3 sourcePosition,
            out float3 direction)
        {
            direction = default;
            if (!manager.HasComponent<PunchAimAssistTarget>(source)) return false;
            var aim = manager.GetComponentData<PunchAimAssistTarget>(source);
            Entity target = aim.Target;
            if (!IsValidTarget(manager, source, target)) return false;
            float3 targetPosition = manager.GetComponentData<LocalTransform>(target).Position;
            float3 velocity = manager.HasComponent<PhysicsVelocity>(target)
                ? manager.GetComponentData<PhysicsVelocity>(target).Linear : float3.zero;
            float3 offset = LaunchAimPrediction.Point(sourcePosition, targetPosition, velocity,
                aim.LaunchSpeed, aim.LeadSeconds, aim.LeadMaximumDistance) - sourcePosition;
            offset.y = 0f;
            direction = math.normalizesafe(offset);
            if (aim.DirectionLimitDegrees > 0f && math.lengthsq(aim.Facing.xz) > 0f)
                direction = CrowdPunch.Systems.Physics.EnemyLaunchHoming.RotateHorizontalVelocity(
                    math.normalizesafe(new float3(aim.Facing.x, 0, aim.Facing.z)), direction,
                    math.radians(aim.DirectionLimitDegrees));
            return math.lengthsq(direction) > 0f;
        }

        public static bool TryGetLockedPoint(EntityManager em, Entity source, float3 position, out float3 point)
        {
            point = default;
            if (!em.HasComponent<PunchAimAssistTarget>(source)) return false;
            var aim = em.GetComponentData<PunchAimAssistTarget>(source);
            if (!IsValidTarget(em, source, aim.Target)) return false;
            float3 velocity = em.HasComponent<PhysicsVelocity>(aim.Target) ? em.GetComponentData<PhysicsVelocity>(aim.Target).Linear : default;
            point = LaunchAimPrediction.Point(position, em.GetComponentData<LocalTransform>(aim.Target).Position,
                velocity, aim.LaunchSpeed, aim.LeadSeconds, aim.LeadMaximumDistance);
            return true;
        }

        public static bool TryGetCommittedDirection(EntityManager em, Entity source, uint revision,
            out float3 direction, out Entity target)
        {
            direction = default; target = Entity.Null;
            if (revision == 0 || !em.HasComponent<PunchAimAssistTarget>(source)) return false;
            var aim = em.GetComponentData<PunchAimAssistTarget>(source);
            bool hasPath = aim.PreviewHasAuthoritativePath != 0;
            if (aim.PreviewRevision != revision || math.lengthsq(aim.PreviewDirection) < .001f
                || !hasPath && aim.PreviewTarget != Entity.Null && (!IsValidTarget(em, source, aim.PreviewTarget)
                    || CommittedLaunchPlan.Generation(em, aim.PreviewTarget) != aim.PreviewTargetGeneration)) return false;
            direction = aim.PreviewDirection;
            target = IsValidTarget(em, source, aim.PreviewTarget) ? aim.PreviewTarget : Entity.Null;
            return true;
        }

        public static bool IsValidTarget(EntityManager manager, Entity source, Entity target)
        {
            if (target == Entity.Null || !manager.Exists(target)) return false;
            if (SnakeProjectileCollision.IgnoresOwner(manager,source,target)) return false;
            if (target != Entity.Null && manager.HasComponent<DinoBoss>(target)) return false;
            if (target != Entity.Null && manager.HasComponent<FallingPillar>(target))
                return manager.GetComponentData<FallingPillar>(target).Phase == PillarPhase.Upright;
            if (target != Entity.Null && manager.HasComponent<RollingBoss>(target))
                return RollingDamageResolution.CanTarget(manager.GetComponentData<RollingBoss>(target),manager.GetComponentData<RollingTuning>(target));
            if (target != Entity.Null && manager.HasComponent<ChickenBoss>(target))
                return manager.GetComponentData<ChickenBoss>(target).Phase != ChickenPhase.Defeated;
            if (target != Entity.Null && manager.HasComponent<Barricade>(target))
                return manager.GetComponentData<Barricade>(target).HitsRemaining > 0
                    && FinaleHitResolution.Active(manager,target)
                    && (!manager.HasComponent<FinaleBoss>(target) || manager.GetComponentData<FinaleBoss>(target).Phase==FinalePhase.Downed);
            if (target != Entity.Null && manager.HasComponent<BossEncounter>(target))
                return manager.GetComponentData<BossEncounter>(target).Cycle != BossCycle.Defeated;
            if (target == Entity.Null || target == source || !manager.Exists(target)
                || !manager.HasComponent<LocalTransform>(target)
                || !manager.HasComponent<EnemyLaunchState>(target)
                || !manager.HasComponent<Health>(target)
                || manager.HasComponent<RespawnRequest>(target)
                && manager.IsComponentEnabled<RespawnRequest>(target)) return false;
            if (SnakeHitRules.Protected(manager, target)) return false;
            return EnemyLaunchTransition.CanReceivePlayerPunch(
                manager.GetComponentData<EnemyLaunchState>(target),
                manager.GetComponentData<Health>(target));
        }

        public static bool IsWithinAssistLimits(EntityManager manager, Entity target, float3 sourcePosition,
            float3 committedDirection, float range, float maximumAngleDegrees)
        {
            if (target == Entity.Null || !manager.Exists(target)
                || !manager.HasComponent<LocalTransform>(target)) return false;
            float2 forward = math.normalizesafe(committedDirection.xz);
            float2 offset = manager.GetComponentData<LocalTransform>(target).Position.xz - sourcePosition.xz;
            float distanceSq = math.lengthsq(offset);
            float maximumDistance = math.max(0f, range);
            if (math.lengthsq(forward) <= 0f || distanceSq <= 0.000001f
                || distanceSq > maximumDistance * maximumDistance) return false;
            float minimumDot = math.cos(math.radians(math.clamp(maximumAngleDegrees, 0f, 180f)));
            return math.dot(forward, offset * math.rsqrt(distanceSq)) >= minimumDot;
        }
    }
}
