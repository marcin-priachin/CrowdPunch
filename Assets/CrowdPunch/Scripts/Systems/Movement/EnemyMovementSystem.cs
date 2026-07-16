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
            new EnemyMovementJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Enemy))]
        [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
        private partial struct EnemyMovementJob : IJobEntity
        {
            public float DeltaTime;

            private void Execute(
                ref PhysicsVelocity physicsVelocity,
                ref PhysicsMass physicsMass,
                EnabledRefRO<KnockbackRecovery> knockbackRecovery,
                in DesiredMovement desiredMovement,
                in EnemyMovementSettings movementSettings)
            {
                physicsMass.InverseInertia.x = 0f;
                physicsMass.InverseInertia.z = 0f;
                physicsVelocity.Angular.x = 0f;
                physicsVelocity.Angular.z = 0f;

                if (knockbackRecovery.ValueRO)
                {
                    return;
                }

                float2 currentVelocity = physicsVelocity.Linear.xz;
                float2 targetVelocity;
                float acceleration;

                if (desiredMovement.Speed <= 0f || math.lengthsq(desiredMovement.Direction) <= 0.0001f)
                {
                    targetVelocity = float2.zero;
                    acceleration = movementSettings.BrakingAcceleration;
                }
                else
                {
                    float3 direction = math.normalizesafe(desiredMovement.Direction);
                    targetVelocity = direction.xz * desiredMovement.Speed;
                    acceleration = movementSettings.Acceleration;
                }

                physicsVelocity.Linear.xz = MoveTowards(
                    currentVelocity,
                    targetVelocity,
                    math.max(0f, acceleration) * DeltaTime);
            }

            private static float2 MoveTowards(float2 current, float2 target, float maxDelta)
            {
                float2 delta = target - current;
                float distance = math.length(delta);

                if (distance <= maxDelta || distance <= 0.0001f)
                {
                    return target;
                }

                return current + delta / distance * maxDelta;
            }
        }
    }
}
