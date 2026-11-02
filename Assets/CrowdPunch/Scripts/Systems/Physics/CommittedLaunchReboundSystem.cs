using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(EnemyLaunchCollisionSystem))]
    [UpdateAfter(typeof(BossHeadBounceSystem))]
    [UpdateAfter(typeof(DasherEnemyImpactSystem))]
    [UpdateBefore(typeof(EnemyRicochetAimSystem))]
    [UpdateBefore(typeof(EnemyRecoverySystem))]
    public partial struct CommittedLaunchReboundSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyLaunchSettings>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var settings = SystemAPI.GetSingleton<EnemyLaunchSettings>();
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            using var storage = new NativeList<RaycastHit>(Allocator.Temp);
            var rays = storage;
            foreach (var (flight, launch, velocity, pose, rebounds, source) in
                SystemAPI.Query<RefRW<CommittedLaunchFlight>, RefRW<EnemyLaunchState>, RefRW<PhysicsVelocity>,
                    RefRO<LocalTransform>, DynamicBuffer<CommittedLaunchRebound>>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                if (flight.ValueRO.LaunchSequence != launch.ValueRO.LaunchSequence || launch.ValueRO.Phase != EnemyLaunchPhase.Launched
                    || launch.ValueRO.Owner != EnemyLaunchOwner.Player) continue;
                Entity surface = flight.ValueRO.Contact;
                float3 normal = flight.ValueRO.ContactNormal;
                flight.ValueRW.Contact = Entity.Null;
                if (flight.ValueRO.Cancelled != 0) { LaunchAimPrediction.StopHoming(ref launch.ValueRW); continue; }
                if (surface == Entity.Null) continue;
                if (flight.ValueRO.NextRebound >= rebounds.Length)
                { CommittedLaunchPlan.Cancel(ref flight.ValueRW, ref launch.ValueRW); continue; }
                var planned = rebounds[flight.ValueRO.NextRebound].Value;
                bool valid = CommittedLaunchPlan.Align(velocity.ValueRO.Linear, planned, surface, normal,
                    settings.PropagationAimMaximumAngleDegrees, out float3 aligned);
                valid &= settings.PropagationAimCorrectionRadius > 0f;
                if (valid && planned.HasAimPoint != 0)
                    valid = CommittedLaunchPlan.ValidTarget(state.EntityManager, source, planned.Target, planned.TargetGeneration)
                        && !CommittedLaunchPlan.Blocked(state.EntityManager, world, source, planned.Target,
                            surface, pose.ValueRO.Position, planned.AimPoint, ref rays);
                if (valid && SystemAPI.TryGetSingleton<PlayerSnapshot>(out var player) && player.IsAvailable)
                {
                    float3 safe = aligned;
                    if (SystemAPI.HasComponent<FinaleBoss>(surface))
                        safe = LaunchContactResponse.FinaleSafety(aligned, pose.ValueRO.Position, player.Position, normal,
                            SystemAPI.GetComponent<FinaleTuning>(surface).ReboundSafetyDegrees);
                    else if (SystemAPI.HasComponent<ChickenBoss>(surface) || SystemAPI.HasComponent<RollingBoss>(surface)
                        || SystemAPI.HasComponent<BossPart>(surface))
                        safe = BossHeadBounceSystem.Redirect(aligned, pose.ValueRO.Position,
                            SystemAPI.GetComponent<LocalTransform>(surface).Position, player.Position, source.Index);
                    valid = math.distancesq(safe, aligned) < .000001f;
                }
                if (!valid) { CommittedLaunchPlan.Cancel(ref flight.ValueRW, ref launch.ValueRW); continue; }
                velocity.ValueRW.Linear = aligned;
                flight.ValueRW.NextRebound++;
                flight.ValueRW.AimPoint = planned.AimPoint;
                flight.ValueRW.Target = planned.Target;
                flight.ValueRW.TargetGeneration = planned.TargetGeneration;
                flight.ValueRW.HasAimPoint = planned.HasAimPoint;
                launch.ValueRW.HomingTarget = planned.HasAimPoint != 0 ? planned.Target : Entity.Null;
                launch.ValueRW.HomingSecondsRemaining = planned.HasAimPoint != 0 ? settings.LaunchHomingDuration : 0f;
            }
        }
    }
}
