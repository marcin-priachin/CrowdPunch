using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
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
            NativeList<float3> activeEnemyPositions = new NativeList<float3>(Allocator.TempJob);

            foreach ((RefRO<LocalTransform> transform, EnabledRefRO<RespawnRequest> respawnRequest) in
                     SystemAPI.Query<RefRO<LocalTransform>, EnabledRefRO<RespawnRequest>>()
                         .WithAll<Enemy>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState))
            {
                if (!respawnRequest.ValueRO)
                {
                    activeEnemyPositions.Add(transform.ValueRO.Position);
                }
            }

            JobHandle chaseJob = new EnemyChaseJob
            {
                PlayerSnapshot = playerSnapshot,
                ArenaBounds = arenaBounds,
                EnemyPositions = activeEnemyPositions.AsDeferredJobArray()
            }.ScheduleParallel(state.Dependency);

            state.Dependency = activeEnemyPositions.Dispose(chaseJob);
        }

        [BurstCompile]
        [WithAll(typeof(Enemy))]
        [WithNone(typeof(RespawnRequest))]
        private partial struct EnemyChaseJob : IJobEntity
        {
            public PlayerSnapshot PlayerSnapshot;
            public ArenaBounds ArenaBounds;
            [ReadOnly] public NativeArray<float3> EnemyPositions;

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

                if (distanceToPlayer <= movementSettings.ChargeDistance)
                {
                    float3 surroundTarget = GetSurroundContactTarget(entity, transform.Position.y, movementSettings);
                    float3 toTarget = surroundTarget - transform.Position;
                    toTarget.y = 0f;

                    float3 separation = GetSeparation(transform.Position, movementSettings);
                    float targetDistance = math.length(toTarget);
                    float3 targetDirection = targetDistance <= movementSettings.StoppingDistance
                        ? float3.zero
                        : toTarget / math.max(0.0001f, targetDistance);

                    desiredMovement.Direction = math.normalizesafe(
                        targetDirection + separation * math.max(0f, movementSettings.SeparationWeight),
                        separation);
                    desiredMovement.Speed = desiredMovement.Direction.Equals(float3.zero)
                        ? 0f
                        : movementSettings.MoveSpeed * movementSettings.ChargeSpeedMultiplier;
                    return;
                }

                desiredMovement.Direction = GetWanderDirection(
                    entity,
                    transform.Position,
                    movementSettings,
                    ref wanderDestination);
                desiredMovement.Speed = movementSettings.WanderSpeed;
            }

            private float3 GetSurroundContactTarget(Entity entity, float y, EnemyMovementSettings movementSettings)
            {
                const float goldenAngle = 2.3999631f;

                int slotIndex = math.max(0, entity.Index);
                float angle = slotIndex * goldenAngle;
                float contactRadius = math.max(0f, movementSettings.StoppingDistance);
                int ringIndex = slotIndex % 3;
                float maxLaneOffset = math.max(0f, movementSettings.SurroundDistance - contactRadius);
                float laneOffset = math.min(maxLaneOffset, ringIndex * math.max(0f, movementSettings.SurroundRingSpacing)) * 0.35f;
                float2 offset = new float2(math.cos(angle), math.sin(angle)) * (contactRadius + laneOffset);

                return new float3(
                    PlayerSnapshot.Position.x + offset.x,
                    y,
                    PlayerSnapshot.Position.z + offset.y);
            }

            private float3 GetSeparation(float3 position, EnemyMovementSettings movementSettings)
            {
                float separationDistance = math.max(0f, movementSettings.SeparationDistance);
                float separationDistanceSq = separationDistance * separationDistance;
                float3 separation = float3.zero;

                if (separationDistanceSq <= 0f)
                {
                    return separation;
                }

                for (int index = 0; index < EnemyPositions.Length; index++)
                {
                    float3 away = position - EnemyPositions[index];
                    away.y = 0f;
                    float distanceSq = math.lengthsq(away);

                    if (distanceSq <= 0.0001f || distanceSq >= separationDistanceSq)
                    {
                        continue;
                    }

                    float distance = math.sqrt(distanceSq);
                    float strength = 1f - distance / separationDistance;
                    separation += away / math.max(0.0001f, distance) * strength;
                }

                return math.normalizesafe(separation);
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
