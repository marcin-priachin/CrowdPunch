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
            NativeList<float3> positions = new NativeList<float3>(Allocator.TempJob);
            foreach ((RefRO<LocalTransform> transform, RefRO<EnemyLaunchState> launch) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLaunchState>>().WithAll<Enemy>().WithNone<RespawnRequest>())
                if (launch.ValueRO.Phase == EnemyLaunchPhase.Active) positions.Add(transform.ValueRO.Position);

            state.Dependency = new DecisionJob { Player = player, EnemyPositions = positions.AsDeferredJobArray(), DeltaTime = SystemAPI.Time.DeltaTime }
                .ScheduleParallel(state.Dependency);
            state.Dependency = positions.Dispose(state.Dependency);
        }

        [BurstCompile, WithAll(typeof(Enemy)), WithNone(typeof(RespawnRequest))]
        private partial struct DecisionJob : IJobEntity
        {
            public PlayerSnapshot Player;
            [ReadOnly] public NativeArray<float3> EnemyPositions;
            public float DeltaTime;

            private void Execute(ref DesiredMovement movement, ref DasherState state, in DasherSettings settings,
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
                    movement.Direction = distance < preferredMin ? -toward : distance > preferredMax ? toward : float3.zero;
                    movement.Speed = distance < preferredMin ? settings.RetreatSpeed : distance > preferredMax ? settings.ApproachSpeed : 0f;
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
                for (int i = 0; i < EnemyPositions.Length; i++)
                {
                    float3 offset = EnemyPositions[i] - origin; offset.y = 0f;
                    if (math.lengthsq(offset) < 0.0001f) continue;
                    float along = math.dot(offset, direction);
                    if (along > 0f && along < end && math.lengthsq(offset - direction * along) <= widthSq) return false;
                }
                return true;
            }
        }
    }
}
