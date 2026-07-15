using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Movement
{
    /// <summary>
    /// Applies enemy movement intent to Unity Physics velocity.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemyChaseSystem))]
    [UpdateBefore(typeof(PunchDetectionSystem))]
    public partial struct EnemyMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Enemy>();
            state.RequireForUpdate<PhysicsVelocity>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new EnemyMovementJob().ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Enemy))]
        [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
        private partial struct EnemyMovementJob : IJobEntity
        {
            private void Execute(
                ref PhysicsVelocity physicsVelocity,
                ref PhysicsMass physicsMass,
                EnabledRefRO<KnockbackRecovery> knockbackRecovery,
                in DesiredMovement desiredMovement)
            {
                physicsMass.InverseInertia.x = 0f;
                physicsMass.InverseInertia.z = 0f;
                physicsVelocity.Angular.x = 0f;
                physicsVelocity.Angular.z = 0f;

                if (knockbackRecovery.ValueRO)
                {
                    return;
                }

                if (desiredMovement.Speed <= 0f || math.lengthsq(desiredMovement.Direction) <= 0.0001f)
                {
                    physicsVelocity.Linear.xz = float2.zero;
                    return;
                }

                float3 direction = math.normalizesafe(desiredMovement.Direction);
                float3 targetVelocity = direction * desiredMovement.Speed;

                physicsVelocity.Linear.x = targetVelocity.x;
                physicsVelocity.Linear.z = targetVelocity.z;
            }
        }
    }
}
