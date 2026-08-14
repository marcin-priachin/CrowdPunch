using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Systems.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemyChaseSystem))]
    [UpdateBefore(typeof(Movement.EnemyMovementSystem))]
    public partial struct DasherDecisionSystem : ISystem
    {
        [BurstCompile] public void OnCreate(ref SystemState state) => state.RequireForUpdate<PlayerSnapshot>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            PlayerSnapshot player = SystemAPI.GetSingleton<PlayerSnapshot>();
            NativeList<EnemySeparationNeighbor> enemies = new NativeList<EnemySeparationNeighbor>(Allocator.TempJob);
            foreach ((RefRO<LocalTransform> transform, RefRO<EnemyLaunchState> launch, RefRO<EnemyArchetype> archetype) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLaunchState>, RefRO<EnemyArchetype>>().WithAll<Enemy>().WithNone<RespawnRequest>())
                if (launch.ValueRO.Phase == EnemyLaunchPhase.Active)
                    enemies.Add(new EnemySeparationNeighbor
                    {
                        Position = transform.ValueRO.Position,
                        Archetype = archetype.ValueRO.Value
                    });

            state.Dependency = new DecisionJob { Player = player, Enemies = enemies.AsDeferredJobArray(), DeltaTime = SystemAPI.Time.DeltaTime }
                .ScheduleParallel(state.Dependency);
            state.Dependency = enemies.Dispose(state.Dependency);
        }

        [BurstCompile, WithAll(typeof(Enemy)), WithNone(typeof(RespawnRequest))]
        private partial struct DecisionJob : IJobEntity
        {
            public PlayerSnapshot Player;
            [ReadOnly] public NativeArray<EnemySeparationNeighbor> Enemies;
            public float DeltaTime;

            private void Execute(ref DesiredMovement movement, ref DasherState state, in DasherSettings settings,
                in EnemyMovementSettings movementSettings, in EnemySeparationDistance separationDistance,
                in EnemyArchetypeSeparationDistances archetypeSeparationDistances,
                in EnemyLaunchState launch, in LocalTransform transform)
            {
                if (launch.Phase != EnemyLaunchPhase.Active || !Player.IsAvailable)
                {
                    movement = default;
                    state.Phase = DasherPhase.Positioning;
                    state.SecondsRemaining = 0f;
                    return;
                }

                float3 toPlayer = Player.Position - transform.Position; toPlayer.y = 0f;
                float distance = math.length(toPlayer);
                float3 toward = math.normalizesafe(toPlayer);
                if (state.Phase == DasherPhase.Positioning)
                {
                    float prepMin = math.min(settings.PreparationMinimumDistance, settings.PreparationMaximumDistance);
                    float prepMax = math.max(settings.PreparationMinimumDistance, settings.PreparationMaximumDistance);
                    if (distance >= prepMin && distance <= prepMax && IsPathSuitable(transform.Position, toward, distance, settings))
                    {
                        state.Phase = DasherPhase.Preparing;
                        state.SecondsRemaining = math.max(0f, settings.TelegraphDuration);
                        state.LockedDirection = toward; // presentation aim only; resampled on commitment below
                        state.HasLockedRotation = 0;
                        movement = default;
                        return;
                    }

                    float preferredMin = math.min(settings.PreferredMinimumDistance, settings.PreferredMaximumDistance);
                    float preferredMax = math.max(settings.PreferredMinimumDistance, settings.PreferredMaximumDistance);
                    float3 primaryDirection = distance < preferredMin ? -toward : distance > preferredMax ? toward : float3.zero;
                    float3 separation = GetSeparation(
                        transform.Position,
                        separationDistance.Value,
                        movementSettings.SeparationWeight,
                        archetypeSeparationDistances);
                    movement.Direction = math.normalizesafe(
                        primaryDirection + separation);
                    movement.Speed = distance < preferredMin ? settings.RetreatSpeed : distance > preferredMax ? settings.ApproachSpeed : 0f;
                    if (movement.Direction.Equals(float3.zero)) movement.Speed = 0f;
                    else if (movement.Speed <= 0f) movement.Speed = movementSettings.WanderSpeed;
                    return;
                }

                movement = default;
                if (state.Phase == DasherPhase.Preparing)
                {
                    state.LockedDirection = toward;
                    state.SecondsRemaining -= DeltaTime;
                    if (state.SecondsRemaining <= 0f)
                    {
                        state.Phase = DasherPhase.Dashing;
                        state.LockedDirection = toward; // sampled at commitment, never during telegraph
                        state.LockedRotation = quaternion.LookRotationSafe(toward, math.up());
                        state.HasLockedRotation = 1;
                        state.DashStartPosition = transform.Position;
                        state.DashSequence++;
                        state.HasHitPlayer = 0;
                    }
                }
                else if (state.Phase == DasherPhase.Recovering)
                {
                    state.SecondsRemaining -= DeltaTime;
                    if (state.SecondsRemaining <= 0f)
                    {
                        state.Phase = DasherPhase.Positioning;
                        state.HasLockedRotation = 0;
                    }
                }
            }

            private bool IsPathSuitable(float3 origin, float3 direction, float playerDistance, DasherSettings settings)
            {
                if (settings.AvoidancePolicy == DasherAvoidancePolicy.None) return true;
                float end = playerDistance + (settings.AvoidancePolicy == DasherAvoidancePolicy.BetweenAndBehindPlayer
                    ? math.max(0f, settings.BehindPlayerDistance) : 0f);
                float widthSq = settings.CorridorWidth * settings.CorridorWidth;
                for (int i = 0; i < Enemies.Length; i++)
                {
                    float3 offset = Enemies[i].Position - origin; offset.y = 0f;
                    if (math.lengthsq(offset) < 0.0001f) continue;
                    float along = math.dot(offset, direction);
                    if (along > 0f && along < end && math.lengthsq(offset - direction * along) <= widthSq) return false;
                }
                return true;
            }

            private float3 GetSeparation(
                float3 position,
                float defaultDistance,
                float defaultWeight,
                EnemyArchetypeSeparationDistances archetypeDistances)
            {
                float3 result = float3.zero;
                float strongestWeight = 0f;
                for (int index = 0; index < Enemies.Length; index++)
                {
                    float distanceLimit = math.max(0f, archetypeDistances.GetDistance(
                        Enemies[index].Archetype,
                        defaultDistance));
                    float distanceLimitSq = distanceLimit * distanceLimit;
                    float separationWeight = math.max(0f, archetypeDistances.GetWeight(
                        Enemies[index].Archetype,
                        defaultWeight));
                    float3 away = position - Enemies[index].Position;
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
