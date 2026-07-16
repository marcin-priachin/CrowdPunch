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
            state.RequireForUpdate<ArenaBounds>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            PlayerSnapshot playerSnapshot = SystemAPI.GetSingleton<PlayerSnapshot>();
            ArenaBounds arenaBounds = SystemAPI.GetSingleton<ArenaBounds>();

            new EnemyChaseJob
            {
                PlayerSnapshot = playerSnapshot,
                ArenaBounds = arenaBounds
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Enemy))]
        private partial struct EnemyChaseJob : IJobEntity
        {
            public PlayerSnapshot PlayerSnapshot;
            public ArenaBounds ArenaBounds;

            private void Execute(
                Entity entity,
                ref DesiredMovement desiredMovement,
                ref WanderDestination wanderDestination,
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

                desiredMovement.Direction = GetWanderDirection(
                    entity,
                    transform.Position,
                    movementSettings,
                    ref wanderDestination);
                desiredMovement.Speed = movementSettings.WanderSpeed;
            }

            private float3 GetWanderDirection(
                Entity entity,
                float3 position,
                EnemyMovementSettings movementSettings,
                ref WanderDestination wanderDestination)
            {
                float2 positionXZ = position.xz;
                float arrivalDistance = math.max(0.75f, movementSettings.WanderSpeed * 0.35f);

                if (wanderDestination.IsAssigned == 0
                    || math.distancesq(positionXZ, wanderDestination.Position.xz) <= arrivalDistance * arrivalDistance)
                {
                    wanderDestination.Position = GetNextWanderDestination(entity, position.y, movementSettings, ref wanderDestination.SequenceIndex);
                    wanderDestination.IsAssigned = 1;
                }

                float3 toDestination = wanderDestination.Position - position;
                toDestination.y = 0f;

                return math.normalizesafe(toDestination);
            }

            private float3 GetNextWanderDestination(
                Entity entity,
                float y,
                EnemyMovementSettings movementSettings,
                ref int sequenceIndex)
            {
                sequenceIndex++;

                float2 center = ArenaBounds.Center.xz;
                float2 extents = math.max(ArenaBounds.Extents.xz, new float2(0f));
                float margin = GetWanderMargin(extents, movementSettings);
                float2 usableExtents = math.max(extents - margin, new float2(0f));
                int sampleIndex = math.max(1, entity.Index + 1 + sequenceIndex * 4099);
                float2 sample = new float2(Halton(sampleIndex, 2), Halton(sampleIndex, 3));
                float2 position = center + (sample * 2f - 1f) * usableExtents;

                return new float3(position.x, y, position.y);
            }

            private static float GetWanderMargin(float2 extents, EnemyMovementSettings movementSettings)
            {
                float acceleration = math.max(0.0001f, movementSettings.Acceleration);
                float stoppingDistance = movementSettings.WanderSpeed * movementSettings.WanderSpeed / (2f * acceleration);
                float desiredMargin = math.max(1f, stoppingDistance + 0.5f);
                float maxMargin = math.cmin(extents) * 0.45f;

                return math.min(desiredMargin, maxMargin);
            }

            private static float Halton(int index, int baseValue)
            {
                float result = 0f;
                float fraction = 1f;
                int remaining = math.max(1, index);

                while (remaining > 0)
                {
                    fraction /= baseValue;
                    result += fraction * (remaining % baseValue);
                    remaining /= baseValue;
                }

                return result;
            }
        }
    }
}
