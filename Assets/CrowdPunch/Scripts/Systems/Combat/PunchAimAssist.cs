using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>Validates targets and evaluates launch alignment and deliberate target-switch input.</summary>
    internal static class PunchAimAssist
    {
        public static float AngleDegrees(float3 facing, float3 offset)
        {
            return math.degrees(math.acos(math.clamp(math.dot(math.normalizesafe(facing.xz),
                math.normalizesafe(offset.xz)), -1f, 1f)));
        }

        public static float TargetScore(float3 facing, float3 offset, float acquisitionDegrees,
            float range, float distanceWeight)
        {
            return TargetScore(AngleDegrees(facing, offset), math.length(offset.xz), acquisitionDegrees, range, distanceWeight);
        }

        public static float TargetScore(float angleDegrees, float distance, float acquisitionDegrees,
            float range, float distanceWeight)
        {
            return angleDegrees / math.max(.0001f, acquisitionDegrees)
                + distance / math.max(.0001f, range) * distanceWeight;
        }

        public static void UpdateInputTurn(ref PunchAimAssistTarget aim, double inputDegrees,
            uint resetSequence, double now, float memorySeconds)
        {
            bool hasHistory = aim.IsAiming != 0 && aim.InputResetSequence == resetSequence;
            float delta = hasHistory ? (float)(inputDegrees - aim.InputRotationDegrees) : 0f;
            aim.InputRotationDegrees = inputDegrees;
            aim.InputResetSequence = resetSequence;
            if (!hasHistory || now - aim.LastInputTurnTime > memorySeconds)
                aim.RecentInputTurnDegrees = 0f;
            if (math.abs(delta) <= .0001f) return;
            if (delta * aim.RecentInputTurnDegrees < 0f) aim.RecentInputTurnDegrees = 0f;
            if (aim.RecentInputTurnDegrees == 0f)
                aim.InputTurnStartYawDegrees = math.degrees(math.atan2(aim.Facing.x, aim.Facing.z));
            aim.RecentInputTurnDegrees = math.clamp(aim.RecentInputTurnDegrees + delta, -90f, 90f);
            aim.LastInputTurnTime = now;
        }

        public static bool HasSwitchIntent(in PunchAimAssistTarget aim, float3 facing, float3 offset,
            double now, float centreDegrees, float minimumTurnDegrees, float memorySeconds)
        {
            if (centreDegrees <= 0f || now - aim.LastInputTurnTime > memorySeconds
                || math.abs(aim.RecentInputTurnDegrees) < math.max(.001f, minimumTurnDegrees)) return false;
            float angle = AngleDegrees(facing, offset);
            if (angle > centreDegrees) return false;
            // Hold the current geometry fixed: only the input turn may improve alignment,
            // not a moving source/target or the camera catching up with player translation.
            float3 beforeInput = math.rotate(quaternion.RotateY(math.radians(aim.InputTurnStartYawDegrees)), math.forward());
            float3 afterInput = math.rotate(quaternion.RotateY(math.radians(aim.RecentInputTurnDegrees)), beforeInput);
            return AngleDegrees(beforeInput, offset) - AngleDegrees(afterInput, offset)
                >= math.max(.001f, minimumTurnDegrees) - .0001f;
        }

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
