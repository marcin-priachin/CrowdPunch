using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(EnemyLaunchCollisionSystem))]
    [UpdateAfter(typeof(BossHeadBounceSystem))]
    [UpdateBefore(typeof(EnemyRecoverySystem))]
    public partial struct EnemyRicochetAimSystem : ISystem
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
            using var nearbyStorage = new NativeList<int>(Allocator.Temp);
            using var rayStorage = new NativeList<RaycastHit>(Allocator.Temp);
            var nearby = nearbyStorage; var rays = rayStorage;
            foreach (var (launch, velocity, pose, source) in
                SystemAPI.Query<RefRW<EnemyLaunchState>, RefRW<PhysicsVelocity>, RefRO<LocalTransform>>()
                    .WithAll<Enemy>().WithNone<RespawnRequest>().WithEntityAccess())
            {
                Entity surface = launch.ValueRO.RicochetSurface;
                if (surface == Entity.Null) continue;
                var normal = launch.ValueRO.RicochetNormal;
                launch.ValueRW.RicochetSurface = Entity.Null;
                launch.ValueRW.RicochetNormal = default;
                if (CommittedLaunchPlan.Owns(state.EntityManager, source, launch.ValueRO)) continue;
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Launched || launch.ValueRO.Owner != EnemyLaunchOwner.Player) continue;
                var corrected = velocity.ValueRO.Linear;
                if (!RicochetAimCorrection.TryCorrect(state.EntityManager, world, source, surface, pose.ValueRO.Position,
                    normal, ref corrected, settings, ref nearby, ref rays, out Entity target)) continue;
                velocity.ValueRW.Linear = corrected;
                launch.ValueRW.HomingTarget = target;
                launch.ValueRW.HomingSecondsRemaining = settings.LaunchHomingDuration;
            }
        }
    }
}
