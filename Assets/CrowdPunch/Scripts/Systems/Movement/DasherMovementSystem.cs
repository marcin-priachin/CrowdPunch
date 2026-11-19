using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Movement
{
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemyMovementSystem))]
    [UpdateAfter(typeof(ApplyImpulseSystem))]
    public partial struct DasherMovementSystem : ISystem
    {
        internal static bool IsPlayerDash(in DasherState dash, in EnemyLaunchState launch)
            => launch.Phase == EnemyLaunchPhase.Launched && launch.Owner == EnemyLaunchOwner.Player
                && dash.Phase == DasherPhase.Dashing && dash.NormalizedLaunchSequence == launch.LaunchSequence;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var owners = SystemAPI.GetComponentLookup<EnemyWaveOwnership>(true);
            var rooms = SystemAPI.GetComponentLookup<BarricadeRoom>(true);
            float deltaTime = SystemAPI.Time.DeltaTime;
            foreach ((RefRW<DasherState> dash, RefRO<DasherSettings> settings, RefRO<EnemyLaunchState> launch,
                         RefRW<LocalTransform> transform, RefRW<PhysicsVelocity> velocity, Entity entity) in
                     SystemAPI.Query<RefRW<DasherState>, RefRO<DasherSettings>, RefRO<EnemyLaunchState>, RefRW<LocalTransform>, RefRW<PhysicsVelocity>>()
                         .WithAll<Enemy>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                bool playerLaunch = launch.ValueRO.Phase == EnemyLaunchPhase.Launched
                    && launch.ValueRO.Owner == EnemyLaunchOwner.Player;
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Active && !playerLaunch) continue;
                if (playerLaunch && (dash.ValueRO.Phase == DasherPhase.Positioning
                    || dash.ValueRO.NormalizedLaunchSequence != launch.ValueRO.LaunchSequence))
                {
                    float2 direction = math.normalizesafe(velocity.ValueRO.Linear.xz,
                        math.normalizesafe(dash.ValueRO.LockedDirection.xz, new float2(0f, 1f)));
                    BeginDash(ref dash.ValueRW, transform.ValueRO.Position, new float3(direction.x, 0f, direction.y));
                    dash.ValueRW.NormalizedLaunchSequence = launch.ValueRO.LaunchSequence;
                }
                if ((dash.ValueRO.Phase == DasherPhase.Preparing || dash.ValueRO.Phase == DasherPhase.Dashing)
                    && math.lengthsq(dash.ValueRO.LockedDirection) > 0.0001f)
                    transform.ValueRW.Rotation = dash.ValueRO.Phase == DasherPhase.Dashing && dash.ValueRO.HasLockedRotation != 0
                        ? dash.ValueRO.LockedRotation
                        : quaternion.LookRotationSafe(dash.ValueRO.LockedDirection, math.up());
                if (dash.ValueRO.Phase == DasherPhase.Preparing)
                {
                    if (settings.ValueRO.PreparationMovement == DasherPreparationMovement.ImmediateStop)
                        velocity.ValueRW.Linear.xz = float2.zero;
                }
                else if (dash.ValueRO.Phase == DasherPhase.Dashing)
                {
                    dash.ValueRW.DashDistanceTravelled += math.distance(
                        transform.ValueRO.Position.xz,
                        dash.ValueRO.PreviousPosition.xz);
                    float2 dashVelocity = dash.ValueRO.LockedDirection.xz * math.max(0f, settings.ValueRO.DashSpeed);
                    bool leavingRoom = BarricadeRoomMovement.TryGetRoom(entity, owners, rooms, out var room)
                        && LeavesRoom(transform.ValueRO.Position.xz + dashVelocity * deltaTime, room);
                    if (leavingRoom || dash.ValueRO.DashDistanceTravelled >= math.max(0f, settings.ValueRO.MaximumDistance))
                    {
                        dash.ValueRW.Phase = DasherPhase.Recovering;
                        dash.ValueRW.SecondsRemaining = math.max(0f, settings.ValueRO.RecoveryDuration);
                        velocity.ValueRW.Linear.xz = float2.zero;
                    }
                    else velocity.ValueRW.Linear.xz = dashVelocity;
                }
            }
        }

        internal static void BeginDash(ref DasherState dash, float3 position, float3 direction)
        {
            dash.Phase = DasherPhase.Dashing;
            dash.SecondsRemaining = 0f;
            LockDirection(ref dash, direction);
            dash.DashStartPosition = position;
            dash.PreviousPosition = position;
            dash.DashDistanceTravelled = 0f;
            dash.DashSequence++;
            dash.HasHitPlayer = 0;
        }

        internal static void LockDirection(ref DasherState dash, float3 direction)
        {
            dash.LockedDirection = direction;
            dash.LockedRotation = quaternion.LookRotationSafe(direction, math.up());
            dash.HasLockedRotation = 1;
        }

        internal static bool LeavesRoom(float2 next, in BarricadeRoom room)
        {
            var bounds = BarricadeRoomMovement.Bounds(room);
            return math.any(next < bounds.Center.xz - bounds.Extents.xz)
                || math.any(next > bounds.Center.xz + bounds.Extents.xz);
        }
    }
}
