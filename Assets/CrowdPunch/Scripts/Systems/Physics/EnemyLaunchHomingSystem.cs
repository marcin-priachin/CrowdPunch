using CrowdPunch.Components;
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
            float maximumRadians = math.radians(math.max(0f,
                SystemAPI.GetSingleton<EnemyLaunchSettings>().LaunchHomingDegreesPerSecond))
                * SystemAPI.Time.DeltaTime;
            if (maximumRadians <= 0f) return;

            ComponentLookup<LocalTransform> transforms = SystemAPI.GetComponentLookup<LocalTransform>(true);
            ComponentLookup<EnemyLaunchState> launchStates = SystemAPI.GetComponentLookup<EnemyLaunchState>(true);
            ComponentLookup<Health> health = SystemAPI.GetComponentLookup<Health>(true);
            ComponentLookup<RespawnRequest> respawns = SystemAPI.GetComponentLookup<RespawnRequest>(true);

            foreach ((RefRO<LocalTransform> transform, RefRO<EnemyLaunchState> launch,
                         RefRW<PhysicsVelocity> velocity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLaunchState>, RefRW<PhysicsVelocity>>()
                         .WithAll<Enemy>())
            {
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Launched) continue;
                Entity target = launch.ValueRO.HomingTarget;
                if (!IsValidTarget(target, transforms, launchStates, health, respawns)) continue;

                velocity.ValueRW.Linear = EnemyLaunchHoming.RotateHorizontalVelocity(
                    velocity.ValueRO.Linear,
                    transforms[target].Position - transform.ValueRO.Position,
                    maximumRadians);
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
