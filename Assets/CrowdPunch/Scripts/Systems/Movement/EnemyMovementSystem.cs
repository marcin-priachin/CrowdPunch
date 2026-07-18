using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

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
            state.RequireForUpdate<ArenaBounds>();
            state.RequireForUpdate<PhysicsVelocity>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ArenaBounds arenaBounds = SystemAPI.GetSingleton<ArenaBounds>();

            new EnemyMovementJob
            {
                ArenaBounds = arenaBounds,
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Enemy))]
        [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
        private partial struct EnemyMovementJob : IJobEntity
        {
            public ArenaBounds ArenaBounds;
            public float DeltaTime;

            private void Execute(
                ref PhysicsVelocity physicsVelocity,
                ref PhysicsMass physicsMass,
                EnabledRefRO<KnockbackRecovery> knockbackRecovery,
                EnabledRefRO<RespawnRequest> respawnRequest,
                in RespawnRequest respawnState,
                in LocalTransform transform,
                in DesiredMovement desiredMovement,
                in EnemyMovementSettings movementSettings)
            {
                physicsMass.InverseInertia.x = 0f;
                physicsMass.InverseInertia.z = 0f;
                physicsVelocity.Angular.x = 0f;
                physicsVelocity.Angular.z = 0f;

                if (respawnRequest.ValueRO)
                {
                    if (respawnState.IsPooled != 0)
                    {
                        physicsVelocity.Linear = float3.zero;
                    }

                    return;
                }

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

                if (IsWandering(desiredMovement, movementSettings))
                {
                    physicsVelocity.Linear.xz = RemoveOutwardBoundaryVelocity(transform.Position, physicsVelocity.Linear.xz);
                }
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

            private static bool IsWandering(DesiredMovement desiredMovement, EnemyMovementSettings movementSettings)
            {
                return desiredMovement.Speed > 0f
                    && math.abs(desiredMovement.Speed - movementSettings.WanderSpeed) <= 0.001f;
            }

            private float2 RemoveOutwardBoundaryVelocity(float3 position, float2 velocity)
            {
                float2 positionXZ = position.xz;
                float2 center = ArenaBounds.Center.xz;
                float2 extents = math.max(ArenaBounds.Extents.xz, new float2(0f));
                float2 min = center - extents;
                float2 max = center + extents;

                if (positionXZ.x <= min.x && velocity.x < 0f)
                {
                    velocity.x = 0f;
                }
                else if (positionXZ.x >= max.x && velocity.x > 0f)
                {
                    velocity.x = 0f;
                }

                if (positionXZ.y <= min.y && velocity.y < 0f)
                {
                    velocity.y = 0f;
                }
                else if (positionXZ.y >= max.y && velocity.y > 0f)
                {
                    velocity.y = 0f;
                }

                return velocity;
            }
        }
    }
}
