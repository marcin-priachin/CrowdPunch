using CrowdPunch.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    internal struct PunchSpecification
    {
        public float3 Origin, Direction;
        public float3 AssistedLaunchDirection;
        public float Range, Radius, Strength, Damage, PositionWeight;
        public EnemyLaunchCause Cause;
        public byte AffectActive, AffectRecovering, AffectLaunched, ApplyDamage, HasAssistedLaunchDirection;
    }

    internal static class PunchResolution
    {
        public static bool Contains(float3 position, in PunchSpecification punch)
        {
            float3 direction = math.normalizesafe(punch.Direction);
            float3 offset = position - punch.Origin;
            float forward = math.dot(offset, direction);
            if (forward < 0f || forward > math.max(0f, punch.Range)) return false;
            float3 closest = punch.Origin + direction * forward;
            return math.lengthsq(position - closest) <= math.max(0f, punch.Radius) * math.max(0f, punch.Radius);
        }

        public static bool IsEligible(in EnemyLaunchState state, in Health health, in PunchSpecification punch)
        {
            if (state.Phase == EnemyLaunchPhase.Defeated) return false;
            if (state.Phase != EnemyLaunchPhase.Launched && health.Current <= 0f) return false;
            return (state.Phase == EnemyLaunchPhase.Active && punch.AffectActive != 0)
                || (state.Phase == EnemyLaunchPhase.Recovering && punch.AffectRecovering != 0)
                || (state.Phase == EnemyLaunchPhase.Launched && punch.AffectLaunched != 0);
        }

        public static bool TryApply(EntityManager manager, Entity target, in PunchSpecification punch)
        {
            if (!manager.Exists(target) || !manager.HasComponent<Enemy>(target)
                || !manager.HasComponent<LocalTransform>(target) || !manager.HasComponent<EnemyTier>(target)
                || !manager.HasComponent<EnemyLaunchState>(target) || !manager.HasComponent<Health>(target)
                || manager.HasComponent<RespawnRequest>(target) && manager.IsComponentEnabled<RespawnRequest>(target)) return false;
            EnemyTier tier = manager.GetComponentData<EnemyTier>(target);
            EnemyLaunchState launch = manager.GetComponentData<EnemyLaunchState>(target);
            Health health = manager.GetComponentData<Health>(target);
            float3 position = manager.GetComponentData<LocalTransform>(target).Position;
            if (!IsEligible(launch, health, punch) || !Contains(position, punch)) return false;
            float3 forward = math.normalizesafe(punch.Direction);
            float3 positionDirection = math.normalizesafe(position - punch.Origin, forward);
            float3 impulseDirection = punch.HasAssistedLaunchDirection != 0
                ? math.normalizesafe(punch.AssistedLaunchDirection, positionDirection)
                : math.normalizesafe(math.lerp(forward, positionDirection, math.saturate(punch.PositionWeight)), positionDirection);
            if (manager.HasComponent<PhysicsVelocity>(target))
            {
                PhysicsVelocity replacementVelocity = manager.GetComponentData<PhysicsVelocity>(target);
                EnemyLaunchVelocity.ResetForPlayerPunchReplacement(
                    ref replacementVelocity,
                    launch.Phase,
                    punch.Cause);
                manager.SetComponentData(target, replacementVelocity);
            }
            if (manager.HasComponent<DasherState>(target))
            {
                DasherState dash = manager.GetComponentData<DasherState>(target);
                dash.Phase = DasherPhase.Positioning; dash.SecondsRemaining = 0f; dash.LockedDirection = impulseDirection;
                dash.LockedRotation = quaternion.LookRotationSafe(impulseDirection, math.up()); dash.HasLockedRotation = 1;
                manager.SetComponentData(target, dash);
                if (manager.HasComponent<PhysicsVelocity>(target))
                {
                    PhysicsVelocity velocity = manager.GetComponentData<PhysicsVelocity>(target); velocity.Linear.xz = float2.zero;
                    manager.SetComponentData(target, velocity);
                }
            }
            manager.SetComponentData(target, new ExternalImpulse { Value = impulseDirection * math.max(0f, punch.Strength) });
            manager.SetComponentEnabled<ExternalImpulse>(target, true);
            if (EnemyLaunchTransition.IsLaunchable(tier))
            {
                EnemyLaunchTransition.Begin(ref launch, punch.Cause, punch.Damage);
                manager.SetComponentData(target, launch);
            }
            if (punch.ApplyDamage != 0 && punch.Damage > 0f)
            {
                DamageRequest damage = manager.IsComponentEnabled<DamageRequest>(target) ? manager.GetComponentData<DamageRequest>(target) : default;
                damage.Amount += punch.Damage; manager.SetComponentData(target, damage); manager.SetComponentEnabled<DamageRequest>(target, true);
            }
            return true;
        }
    }
}
