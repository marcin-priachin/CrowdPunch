using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    /// <summary>
    /// Produces enemy movement intent from player position.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(InputBridge.PlayerBridgeSystem))]
    public partial struct EnemyChaseSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerSnapshot>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            PlayerSnapshot playerSnapshot = SystemAPI.GetSingleton<PlayerSnapshot>();

            new EnemyChaseJob
            {
                PlayerSnapshot = playerSnapshot,
                ElapsedTime = (float)SystemAPI.Time.ElapsedTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Enemy))]
        private partial struct EnemyChaseJob : IJobEntity
        {
            public PlayerSnapshot PlayerSnapshot;
            public float ElapsedTime;

            private void Execute(
                Entity entity,
                ref DesiredMovement desiredMovement,
                in LocalTransform transform,
                in EnemyMovementSettings movementSettings)
            {
                if (!PlayerSnapshot.IsAvailable)
                {
                    desiredMovement.Direction = float3.zero;
                    desiredMovement.Speed = 0f;
                    return;
                }

                float3 toPlayer = PlayerSnapshot.Position - transform.Position;
                toPlayer.y = 0f;

                float distanceToPlayer = math.length(toPlayer);

                if (distanceToPlayer <= movementSettings.StoppingDistance)
                {
                    desiredMovement.Direction = float3.zero;
                    desiredMovement.Speed = 0f;
                    return;
                }

                if (distanceToPlayer <= movementSettings.ChargeDistance)
                {
                    desiredMovement.Direction = math.normalizesafe(toPlayer);
                    desiredMovement.Speed = movementSettings.MoveSpeed * movementSettings.ChargeSpeedMultiplier;
                    return;
                }

                float wanderAngle = entity.Index * 2.3999631f
                    + math.sin(ElapsedTime * 0.8f + entity.Index * 0.37f) * 1.25f;

                desiredMovement.Direction = new float3(math.cos(wanderAngle), 0f, math.sin(wanderAngle));
                desiredMovement.Speed = movementSettings.WanderSpeed;
            }
        }
    }
}
