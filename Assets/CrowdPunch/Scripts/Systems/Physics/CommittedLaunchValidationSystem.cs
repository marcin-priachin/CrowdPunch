using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(ApplyImpulseSystem))]
    [UpdateBefore(typeof(EnemyLaunchHomingSystem))]
    public partial struct CommittedLaunchValidationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<EnemyLaunchSettings>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var settings = SystemAPI.GetSingleton<EnemyLaunchSettings>();
            using var storage = new NativeList<RaycastHit>(Allocator.Temp);
            var rays = storage;
            foreach (var (flight, launch, pose, velocity, source) in
                SystemAPI.Query<RefRW<CommittedLaunchFlight>, RefRW<EnemyLaunchState>, RefRO<LocalTransform>, RefRO<PhysicsVelocity>>().WithEntityAccess())
            {
                if (flight.ValueRO.LaunchSequence != launch.ValueRO.LaunchSequence || launch.ValueRO.Phase != EnemyLaunchPhase.Launched
                    || launch.ValueRO.Owner != EnemyLaunchOwner.Player
                    || SystemAPI.HasComponent<RespawnRequest>(source) && SystemAPI.IsComponentEnabled<RespawnRequest>(source))
                { SystemAPI.SetComponentEnabled<CommittedLaunchFlight>(source, false); continue; }
                if (flight.ValueRO.Cancelled != 0) { LaunchAimPrediction.StopHoming(ref launch.ValueRW); continue; }
                Entity surface = Entity.Null;
                if (SystemAPI.HasBuffer<CommittedLaunchRebound>(source))
                {
                    var rebounds = state.EntityManager.GetBuffer<CommittedLaunchRebound>(source, true);
                    if (flight.ValueRO.NextRebound < rebounds.Length) surface = rebounds[flight.ValueRO.NextRebound].Value.Surface;
                }
                bool invalidSurface = surface != Entity.Null && (!state.EntityManager.Exists(surface)
                    || SystemAPI.HasComponent<Barricade>(surface) && SystemAPI.GetComponent<Barricade>(surface).HitsRemaining <= 0);
                if (flight.ValueRO.SecondsRemaining <= 0f || invalidSurface
                    || flight.ValueRO.HasAimPoint != 0 && (!CommittedLaunchPlan.ValidTarget(state.EntityManager, source,
                        flight.ValueRO.Target, flight.ValueRO.TargetGeneration)
                        || launch.ValueRO.HomingSecondsRemaining > 0f && !LaunchAimPrediction.WithinAngle(velocity.ValueRO.Linear,
                            flight.ValueRO.AimPoint - pose.ValueRO.Position, settings.LaunchHomingMaximumAngleDegrees)
                        || CommittedLaunchPlan.Blocked(state.EntityManager, world, source, flight.ValueRO.Target,
                            surface, pose.ValueRO.Position, flight.ValueRO.AimPoint, ref rays)))
                    CommittedLaunchPlan.Cancel(ref flight.ValueRW, ref launch.ValueRW);
                else flight.ValueRW.SecondsRemaining -= SystemAPI.Time.DeltaTime;
            }
        }
    }
}
