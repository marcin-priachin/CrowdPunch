using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Groups;
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
    [UpdateBefore(typeof(Combat.PunchDetectionSystem))]
    public partial struct DasherMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach ((RefRW<DasherState> dash, RefRO<DasherSettings> settings, RefRO<EnemyLaunchState> launch,
                         RefRW<LocalTransform> transform, RefRW<PhysicsVelocity> velocity) in
                     SystemAPI.Query<RefRW<DasherState>, RefRO<DasherSettings>, RefRO<EnemyLaunchState>, RefRW<LocalTransform>, RefRW<PhysicsVelocity>>()
                         .WithAll<Enemy>().WithNone<RespawnRequest>())
            {
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Active) continue;
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
                    float travelled = math.distance(transform.ValueRO.Position.xz, dash.ValueRO.DashStartPosition.xz);
                    if (travelled >= math.max(0f, settings.ValueRO.MaximumDistance))
                    {
                        dash.ValueRW.Phase = DasherPhase.Recovering;
                        dash.ValueRW.SecondsRemaining = math.max(0f, settings.ValueRO.RecoveryDuration);
                        velocity.ValueRW.Linear.xz = float2.zero;
                    }
                    else velocity.ValueRW.Linear.xz = dash.ValueRO.LockedDirection.xz * math.max(0f, settings.ValueRO.DashSpeed);
                }
            }
        }
    }
}
