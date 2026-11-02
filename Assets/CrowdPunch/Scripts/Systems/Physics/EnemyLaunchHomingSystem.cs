using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    /// <summary>Turns launched horizontal velocity toward the target selected when the launch began.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(ApplyImpulseSystem))]
    [UpdateAfter(typeof(DasherVelocityCaptureSystem))]
    public partial struct EnemyLaunchHomingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyLaunchSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var settings = SystemAPI.GetSingleton<EnemyLaunchSettings>();
            float dt = SystemAPI.Time.DeltaTime;
            SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physics);
            bool hasPhysics = SystemAPI.HasSingleton<PhysicsWorldSingleton>();
            var enemies = SystemAPI.GetComponentLookup<Enemy>(true);
            var velocities = SystemAPI.GetComponentLookup<PhysicsVelocity>(true);
            var commitments = SystemAPI.GetComponentLookup<CommittedLaunchFlight>(true);

            ComponentLookup<LocalTransform> transforms = SystemAPI.GetComponentLookup<LocalTransform>(true);
            ComponentLookup<EnemyLaunchState> launchStates = SystemAPI.GetComponentLookup<EnemyLaunchState>(true);
            ComponentLookup<Health> health = SystemAPI.GetComponentLookup<Health>(true);
            ComponentLookup<RespawnRequest> respawns = SystemAPI.GetComponentLookup<RespawnRequest>(true);
            var segments = SystemAPI.GetComponentLookup<SnakeSegment>(true);

            foreach ((RefRO<LocalTransform> transform, RefRW<EnemyLaunchState> launch,
                         RefRW<PhysicsVelocity> velocity, Entity source) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRW<EnemyLaunchState>, RefRW<PhysicsVelocity>>()
                         .WithAll<Enemy>().WithEntityAccess())
            {
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Launched) continue;
                bool committed = commitments.HasComponent(source) && commitments.IsComponentEnabled(source)
                    && commitments[source].LaunchSequence == launch.ValueRO.LaunchSequence;
                var flight = committed ? commitments[source] : default;
                if (committed && (flight.Cancelled != 0 || flight.HasAimPoint == 0))
                { LaunchAimPrediction.StopHoming(ref launch.ValueRW); continue; }
                Entity target = launch.ValueRO.HomingTarget;
                if (target == Entity.Null) continue;
                if (launch.ValueRO.HomingSecondsRemaining <= 0f || settings.LaunchHomingDegreesPerSecond <= 0f
                    || !transforms.HasComponent(target)
                    || segments.HasComponent(source) && SnakeContactSystem.Excludes(segments[source], target, segments)
                    || Combat.SnakeHitRules.Protected(state.EntityManager, target))
                { LaunchAimPrediction.StopHoming(ref launch.ValueRW); continue; }
                bool livingBoss = SystemAPI.HasComponent<BossEncounter>(target)
                    && SystemAPI.GetComponent<BossEncounter>(target).Cycle != BossCycle.Defeated;
                livingBoss |= SystemAPI.HasComponent<ChickenBoss>(target)
                    && SystemAPI.GetComponent<ChickenBoss>(target).Phase != ChickenPhase.Defeated;
                bool intactBarricade = SystemAPI.HasComponent<Barricade>(target)
                    && SystemAPI.GetComponent<Barricade>(target).HitsRemaining > 0;
                livingBoss |= SystemAPI.HasComponent<RollingBoss>(target)
                    && Combat.RollingDamageResolution.CanTarget(SystemAPI.GetComponent<RollingBoss>(target),SystemAPI.GetComponent<RollingTuning>(target));
                bool uprightPillar = SystemAPI.HasComponent<FallingPillar>(target)
                    && SystemAPI.GetComponent<FallingPillar>(target).Phase == PillarPhase.Upright;
                if (!livingBoss && !intactBarricade && !uprightPillar && !IsValidTarget(target, transforms, launchStates, health, respawns))
                { LaunchAimPrediction.StopHoming(ref launch.ValueRW); continue; }

                float3 offset = (committed ? flight.AimPoint : transforms[target].Position) - transform.ValueRO.Position;
                if (!LaunchAimPrediction.WithinAngle(velocity.ValueRO.Linear, offset, settings.LaunchHomingMaximumAngleDegrees))
                { LaunchAimPrediction.StopHoming(ref launch.ValueRW); continue; }
                if (hasPhysics && !committed)
                {
                    var collector = new LaunchObstructionCollector { Source = source, Target = target, Enemies = enemies };
                    if (physics.CollisionWorld.CastRay(new RaycastInput { Start = transform.ValueRO.Position,
                        End = committed ? flight.AimPoint : transforms[target].Position, Filter = CollisionFilter.Default }, ref collector))
                    { LaunchAimPrediction.StopHoming(ref launch.ValueRW); continue; }
                }

                float activeTime = math.min(dt, launch.ValueRO.HomingSecondsRemaining);
                float3 targetVelocity = velocities.HasComponent(target) ? velocities[target].Linear : float3.zero;
                float3 targetPoint = committed ? flight.AimPoint : LaunchAimPrediction.Point(transform.ValueRO.Position, transforms[target].Position,
                    targetVelocity, math.length(velocity.ValueRO.Linear.xz), settings.AimLeadSeconds, settings.AimLeadMaximumDistance);

                velocity.ValueRW.Linear = EnemyLaunchHoming.RotateHorizontalVelocity(
                    velocity.ValueRO.Linear,
                    targetPoint - transform.ValueRO.Position,
                    math.radians(settings.LaunchHomingDegreesPerSecond) * activeTime);
                launch.ValueRW.HomingSecondsRemaining = math.max(0f, launch.ValueRO.HomingSecondsRemaining - dt);
                if (launch.ValueRO.HomingSecondsRemaining <= 0f) LaunchAimPrediction.StopHoming(ref launch.ValueRW);
            }
        }

        private static bool IsValidTarget(Entity target,
            ComponentLookup<LocalTransform> transforms,
            ComponentLookup<EnemyLaunchState> launchStates,
            ComponentLookup<Health> health,
            ComponentLookup<RespawnRequest> respawns)
        {
            if (target == Entity.Null || !transforms.HasComponent(target)
                || !launchStates.HasComponent(target) || !health.HasComponent(target)
                || respawns.HasComponent(target) && respawns.IsComponentEnabled(target)) return false;
            EnemyLaunchPhase phase = launchStates[target].Phase;
            return health[target].Current > 0f
                && (phase == EnemyLaunchPhase.Active || phase == EnemyLaunchPhase.Recovering);
        }
    }

    public static class EnemyLaunchHoming
    {
        public static float3 RotateHorizontalVelocity(float3 velocity, float3 targetOffset, float maximumRadians)
        {
            float speed = math.length(velocity.xz);
            float2 desired = math.normalizesafe(targetOffset.xz);
            if (speed <= 0.0001f || math.lengthsq(desired) <= 0f || maximumRadians <= 0f) return velocity;

            float2 current = velocity.xz / speed;
            float signedAngle = math.atan2(current.x * desired.y - current.y * desired.x,
                math.dot(current, desired));
            float turn = math.clamp(signedAngle, -maximumRadians, maximumRadians);
            float sine = math.sin(turn);
            float cosine = math.cos(turn);
            velocity.xz = new float2(
                current.x * cosine - current.y * sine,
                current.x * sine + current.y * cosine) * speed;
            return velocity;
        }
    }
}
