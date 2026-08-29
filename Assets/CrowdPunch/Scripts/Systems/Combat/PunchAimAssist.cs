using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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
            Entity target = manager.GetComponentData<PunchAimAssistTarget>(source).Target;
            if (!IsValidTarget(manager, source, target)) return false;
            float3 offset = manager.GetComponentData<LocalTransform>(target).Position - sourcePosition;
            offset.y = 0f;
            direction = math.normalizesafe(offset);
            return math.lengthsq(direction) > 0f;
        }

        public static bool IsValidTarget(EntityManager manager, Entity source, Entity target)
        {
            if (target == Entity.Null || target == source || !manager.Exists(target)
                || !manager.HasComponent<LocalTransform>(target)
                || !manager.HasComponent<EnemyLaunchState>(target)
                || !manager.HasComponent<Health>(target)
                || manager.HasComponent<RespawnRequest>(target)
                && manager.IsComponentEnabled<RespawnRequest>(target)) return false;
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
