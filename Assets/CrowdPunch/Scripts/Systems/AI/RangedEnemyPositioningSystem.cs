using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    /// <summary>Overrides baseline intent with ranged approach, hold, or retreat intent.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemyChaseSystem))]
    public partial struct RangedEnemyPositioningSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerSnapshot>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            PlayerSnapshot player = SystemAPI.GetSingleton<PlayerSnapshot>();
            NativeList<float3> activePositions = new NativeList<float3>(Allocator.TempJob);

            foreach ((RefRO<LocalTransform> transform, RefRO<EnemyLaunchState> launchState) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLaunchState>>()
                         .WithAll<Enemy>()
                         .WithNone<RespawnRequest>())
            {
                if (launchState.ValueRO.Phase == EnemyLaunchPhase.Active)
                {
                    activePositions.Add(transform.ValueRO.Position);
                }
            }

            state.Dependency = new PositioningJob
            {
                Player = player,
                ActiveEnemyPositions = activePositions.AsDeferredJobArray()
            }.ScheduleParallel(state.Dependency);
            state.Dependency = activePositions.Dispose(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(Enemy))]
        [WithNone(typeof(RespawnRequest))]
        private partial struct PositioningJob : IJobEntity
        {
            public PlayerSnapshot Player;
            [ReadOnly] public NativeArray<float3> ActiveEnemyPositions;

            private void Execute(
                ref DesiredMovement movement,
                ref RangedPositioningState positioning,
                in RangedEnemySettings settings,
                in EnemyMovementSettings movementSettings,
                in EnemySeparationDistance separationDistance,
                in EnemyLaunchState launchState,
                in LocalTransform transform)
            {
                if (launchState.Phase != EnemyLaunchPhase.Active || !Player.IsAvailable)
                {
                    movement = default;
                    positioning.Mode = RangedPositioningMode.Hold;
                    return;
                }

                float3 toPlayer = Player.Position - transform.Position;
                toPlayer.y = 0f;
                float distance = math.length(toPlayer);
                float3 towardPlayer = math.normalizesafe(toPlayer);
                float minimum = math.max(0f, math.min(settings.PreferredMinimumDistance, settings.PreferredMaximumDistance));
                float maximum = math.max(minimum, math.max(settings.PreferredMinimumDistance, settings.PreferredMaximumDistance));
                float3 primaryDirection = float3.zero;
                float speed = 0f;

                if (distance < minimum)
                {
                    positioning.Mode = RangedPositioningMode.Retreat;
                    primaryDirection = -towardPlayer;
                    speed = math.max(0f, settings.RetreatSpeed);
                }
                else if (distance > maximum)
                {
                    positioning.Mode = RangedPositioningMode.Approach;
                    primaryDirection = towardPlayer;
                    speed = math.max(0f, settings.ApproachSpeed);
                }
                else
                {
                    positioning.Mode = RangedPositioningMode.Hold;
                }

                float3 separation = GetSeparation(transform.Position, separationDistance.Value);
                float3 combined = primaryDirection + separation * math.max(0f, movementSettings.SeparationWeight);
                movement.Direction = math.normalizesafe(combined);
                movement.Speed = movement.Direction.Equals(float3.zero)
                    ? 0f
                    : math.max(speed, speed <= 0f ? movementSettings.WanderSpeed : 0f);
            }

            private float3 GetSeparation(float3 position, float preferredDistance)
            {
                float distanceLimit = math.max(0f, preferredDistance);
                float distanceLimitSq = distanceLimit * distanceLimit;
                float3 result = float3.zero;
                for (int index = 0; index < ActiveEnemyPositions.Length; index++)
                {
                    float3 away = position - ActiveEnemyPositions[index];
                    away.y = 0f;
                    float distanceSq = math.lengthsq(away);
                    if (distanceSq <= 0.0001f || distanceSq >= distanceLimitSq)
                    {
                        continue;
                    }

                    float distance = math.sqrt(distanceSq);
                    result += away / distance * (1f - distance / distanceLimit);
                }

                return math.normalizesafe(result);
            }
        }
    }
}
