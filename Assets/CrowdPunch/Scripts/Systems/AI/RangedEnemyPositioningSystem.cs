using CrowdPunch.Components;
using CrowdPunch.Utilities;
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
            NavigationGrid grid = SystemAPI.HasSingleton<NavigationGrid>() && SystemAPI.GetSingleton<NavigationRuntimeSettings>().Enabled != 0
                ? SystemAPI.GetSingleton<NavigationGrid>() : default;
            NativeList<EnemySeparationNeighbor> activeEnemies = new NativeList<EnemySeparationNeighbor>(Allocator.TempJob);

            foreach ((RefRO<LocalTransform> transform, RefRO<EnemyLaunchState> launchState,
                         RefRO<EnemyArchetype> archetype) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLaunchState>, RefRO<EnemyArchetype>>()
                         .WithAll<Enemy>()
                         .WithNone<RespawnRequest>())
            {
                if (launchState.ValueRO.Phase == EnemyLaunchPhase.Active)
                {
                    activeEnemies.Add(new EnemySeparationNeighbor
                    {
                        Position = transform.ValueRO.Position,
                        Archetype = archetype.ValueRO.Value
                    });
                }
            }

            state.Dependency = new PositioningJob
            {
                Player = player, Grid = grid,
                ActiveEnemies = activeEnemies.AsDeferredJobArray()
            }.ScheduleParallel(state.Dependency);
            state.Dependency = activeEnemies.Dispose(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(Enemy))]
        [WithNone(typeof(RespawnRequest))]
        private partial struct PositioningJob : IJobEntity
        {
            public PlayerSnapshot Player;
            public NavigationGrid Grid;
            [ReadOnly] public NativeArray<EnemySeparationNeighbor> ActiveEnemies;

            private void Execute(
                ref DesiredMovement movement,
                ref NavigationIntent navigation,
                ref RangedPositioningState positioning,
                in RangedEnemySettings settings,
                in EnemyMovementSettings movementSettings, in NavigationAgent agent,
                in EnemySeparationDistance separationDistance,
                in EnemyArchetypeSeparationDistances archetypeSeparationDistances,
                in EnemyLaunchState launchState,
                in LocalTransform transform)
            {
                navigation = default;
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

                float3 separation = GetSeparation(
                    transform.Position,
                    separationDistance.Value,
                    movementSettings.SeparationWeight,
                    archetypeSeparationDistances);
                float3 combined = primaryDirection + separation;
                movement.Direction = math.normalizesafe(combined);
                movement.Speed = movement.Direction.Equals(float3.zero)
                    ? 0f
                    : math.max(speed, speed <= 0f ? movementSettings.WanderSpeed : 0f);
                float preferred = (minimum + maximum) * .5f;
                float3 destination = NavigationGeometry.DistanceBandDestination(Grid,transform.Position,Player.Position,minimum,maximum,agent.Radius);
                destination.y = transform.Position.y;
                navigation = NavigationIntent.Travel(destination, movement.Speed, .35f, separation, NavigationGoalKind.DistanceBand);
                if (positioning.Mode == RangedPositioningMode.Hold) navigation.Mode = NavigationMode.Hold;
            }

            private float3 GetSeparation(
                float3 position,
                float defaultDistance,
                float defaultWeight,
                EnemyArchetypeSeparationDistances archetypeDistances)
            {
                float3 result = float3.zero;
                float strongestWeight = 0f;
                for (int index = 0; index < ActiveEnemies.Length; index++)
                {
                    float distanceLimit = math.max(0f, archetypeDistances.GetDistance(
                        ActiveEnemies[index].Archetype,
                        defaultDistance));
                    float distanceLimitSq = distanceLimit * distanceLimit;
                    float separationWeight = math.max(0f, archetypeDistances.GetWeight(
                        ActiveEnemies[index].Archetype,
                        defaultWeight));
                    float3 away = position - ActiveEnemies[index].Position;
                    away.y = 0f;
                    float distanceSq = math.lengthsq(away);
                    if (distanceSq <= 0.0001f || distanceSq >= distanceLimitSq)
                    {
                        continue;
                    }

                    float distance = math.sqrt(distanceSq);
                    result += away / distance * (1f - distance / distanceLimit) * separationWeight;
                    strongestWeight = math.max(strongestWeight, separationWeight);
                }

                return math.normalizesafe(result) * strongestWeight;
            }
        }
    }
}
