using CrowdPunch.Components;
using CrowdPunch.Systems.Physics;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    internal static class AlienSpikeProjectilePunch
    {
        internal static float3 Direction(EntityManager em, Entity entity, float3 position,
            in AlienSpikeProjectile shot, in PunchSpecification punch)
        {
            if (shot.Tuning.AimMode == AlienSpikeAimMode.ChickenStyle)
                return ChickenProjectilePunch.Direction(em, entity, position, punch);
            return math.normalizesafe(new float3(punch.Direction.x, 0, punch.Direction.z), new float3(0, 0, 1));
        }

        internal static bool Contains(CollisionWorld world, AlienSpikeShotContact contact,
            in AlienSpikeProjectile shot, float3 position, in PunchSpecification punch)
        {
            // Every shot travels inside the grounded hit band. Apply the same XZ punch footprint
            // to low and high sockets so every rendered lane can be redirected.
            var planarPosition = new float3(position.x, punch.Origin.y, position.z);
            if (!PunchResolution.Contains(planarPosition, punch)) return false;
            // A punch cannot reach a shot through cover, even when both are inside the punch volume.
            var obstructionTest = shot;
            obstructionTest.PlayerOwned = 0;
            float3 origin = new float3(punch.Origin.x, position.y, punch.Origin.z);
            return AlienSpikeShotContact.Sweep(world, contact, obstructionTest, default, origin, position - origin)
                .Kind == AlienSpikeContactKind.None;
        }

        internal static void Redirect(EntityManager em, Entity entity, ref AlienSpikeProjectile shot,
            float3 position, in PunchSpecification punch)
        {
            shot.Velocity = Direction(em, entity, position, shot, punch) * shot.Tuning.Speed;
            shot.HomingTarget = Entity.Null;
            if (shot.Tuning.AimMode == AlienSpikeAimMode.ChickenStyle)
                shot.HomingTarget = PunchAimAssist.TryGetCommittedDirection(em, entity, punch.AimRevision, out _, out var target)
                    ? target : em.GetComponentData<PunchAimAssistTarget>(entity).Target;
            shot.HomingRemaining = shot.HomingTarget != Entity.Null ? punch.HomingDuration : 0;
            shot.Remaining = shot.Tuning.Lifetime;
            shot.Redirected = shot.PlayerOwned = 1;
            shot.Source = entity;
            shot.SourceLifetime = 0;
            shot.LaunchSequence = ++shot.VisualSequence;
            shot.PunchSequence = punch.PunchSequence;
            shot.ChainDepth = 1;
        }

        internal static void Guide(EntityManager em, Entity entity, ref AlienSpikeProjectile shot,
            float3 position, in EnemyLaunchSettings tuning, float dt)
        {
            var target = shot.HomingTarget;
            if (target == Entity.Null) return;
            if (shot.HomingRemaining <= 0 || !PunchAimAssist.IsValidTarget(em, entity, target))
            {
                shot.HomingTarget = Entity.Null;
                return;
            }
            float3 targetPosition = em.GetComponentData<LocalTransform>(target).Position;
            if (!LaunchAimPrediction.WithinAngle(shot.Velocity, targetPosition - position, tuning.LaunchHomingMaximumAngleDegrees))
            {
                shot.HomingTarget = Entity.Null;
                return;
            }
            float3 targetVelocity = em.HasComponent<PhysicsVelocity>(target)
                ? em.GetComponentData<PhysicsVelocity>(target).Linear : float3.zero;
            float3 point = LaunchAimPrediction.Point(position, targetPosition, targetVelocity, shot.Tuning.Speed,
                tuning.AimLeadSeconds, tuning.AimLeadMaximumDistance);
            shot.Velocity = EnemyLaunchHoming.RotateHorizontalVelocity(shot.Velocity, point - position,
                math.radians(tuning.LaunchHomingDegreesPerSecond) * math.min(dt, shot.HomingRemaining));
            shot.HomingRemaining -= dt;
            if (shot.HomingRemaining <= 0) shot.HomingTarget = Entity.Null;
        }
    }
}

