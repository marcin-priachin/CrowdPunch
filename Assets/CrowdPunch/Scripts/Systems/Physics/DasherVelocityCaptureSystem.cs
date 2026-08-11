using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile, UpdateInGroup(typeof(GamePrePhysicsGroup)), UpdateAfter(typeof(ApplyImpulseSystem))]
    public partial struct DasherVelocityCaptureSystem : ISystem
    {
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            foreach ((RefRW<DasherState> dash, RefRO<DasherSettings> settings, RefRO<EnemyLaunchState> launch, RefRW<PhysicsVelocity> velocity,
                         RefRW<LocalTransform> transform) in
                     SystemAPI.Query<RefRW<DasherState>, RefRO<DasherSettings>, RefRO<EnemyLaunchState>, RefRW<PhysicsVelocity>, RefRW<LocalTransform>>())
            {
                dash.ValueRW.PreviousPosition = transform.ValueRO.Position;
                if (launch.ValueRO.Phase == EnemyLaunchPhase.Launched)
                {
                    if (dash.ValueRO.NormalizedLaunchSequence != launch.ValueRO.LaunchSequence)
                    {
                        float2 fallback = math.normalizesafe(dash.ValueRO.LockedDirection.xz, new float2(0f, 1f));
                        float2 direction = math.normalizesafe(velocity.ValueRO.Linear.xz, fallback);
                        velocity.ValueRW.Linear.xz = direction * math.max(0f, settings.ValueRO.DashSpeed);
                        dash.ValueRW.NormalizedLaunchSequence = launch.ValueRO.LaunchSequence;
                    }
                    if (dash.ValueRO.HasLockedRotation == 0)
                    {
                        float3 direction = math.normalizesafe(velocity.ValueRO.Linear, math.forward());
                        direction.y = 0f;
                        dash.ValueRW.LockedRotation = quaternion.LookRotationSafe(
                            math.normalizesafe(direction, math.forward()), math.up());
                        dash.ValueRW.HasLockedRotation = 1;
                    }
                    transform.ValueRW.Rotation = dash.ValueRO.LockedRotation;
                    dash.ValueRW.PreservedLaunchedVelocity = velocity.ValueRO.Linear;
                    dash.ValueRW.PreservedLaunchedAngularVelocity = velocity.ValueRO.Angular;
                }
                else if (dash.ValueRO.Phase != DasherPhase.Dashing)
                {
                    dash.ValueRW.HasLockedRotation = 0;
                }
            }
        }
    }
}
