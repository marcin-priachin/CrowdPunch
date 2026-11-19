using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Movement;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Systems.Physics
{
    [BurstCompile, UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateAfter(typeof(DasherEnemyImpactSystem))]
    [UpdateAfter(typeof(DasherPlayerImpactSystem))]
    [UpdateAfter(typeof(BossHeadBounceSystem))]
    [UpdateAfter(typeof(CoverReflectionSystem))]
    [UpdateAfter(typeof(BarricadeReboundSystem))]
    [UpdateBefore(typeof(EnemyRecoverySystem))]
    [UpdateBefore(typeof(EnemyRicochetAimSystem))]
    public partial struct DasherObstacleRedirectSystem : ISystem
    {
        internal const float MaximumWallNormalY = 0.5f;
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<DasherState>();
        }

        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            // These surfaces can find a contact by sweep before the solver reports one.
            foreach (var (dash, settings, launch, velocity) in
                     SystemAPI.Query<RefRW<DasherState>, RefRO<DasherSettings>, RefRO<EnemyLaunchState>, RefRW<PhysicsVelocity>>()
                         .WithNone<RespawnRequest>())
            {
                Entity surface = launch.ValueRO.RicochetSurface;
                if (surface != Entity.Null && DasherMovementSystem.IsPlayerDash(dash.ValueRO, launch.ValueRO)
                    && (SystemAPI.HasComponent<Barricade>(surface) || SystemAPI.HasComponent<RotatingCover>(surface)))
                    Redirect(ref dash.ValueRW, ref velocity.ValueRW, settings.ValueRO.DashSpeed, launch.ValueRO.RicochetNormal);
            }

            PhysicsWorld physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            RedirectOnObstacleContactJob job = new RedirectOnObstacleContactJob
            {
                DasherLookup = SystemAPI.GetComponentLookup<DasherState>(),
                SettingsLookup = SystemAPI.GetComponentLookup<DasherSettings>(true),
                RespawnLookup = SystemAPI.GetComponentLookup<RespawnRequest>(true),
                VelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(),
                LaunchLookup = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                EnemyLookup = SystemAPI.GetComponentLookup<Enemy>(true),
                TierLookup = SystemAPI.GetComponentLookup<KnockbackResponse>(true),
                NumDynamicBodies = physicsWorld.NumDynamicBodies
            };

            state.Dependency = job.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        }

        [BurstCompile]
        private struct RedirectOnObstacleContactJob : ICollisionEventsJob
        {
            public ComponentLookup<DasherState> DasherLookup;
            [ReadOnly] public ComponentLookup<DasherSettings> SettingsLookup;
            [ReadOnly] public ComponentLookup<RespawnRequest> RespawnLookup;
            public ComponentLookup<PhysicsVelocity> VelocityLookup;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> LaunchLookup;
            [ReadOnly] public ComponentLookup<Enemy> EnemyLookup;
            [ReadOnly] public ComponentLookup<KnockbackResponse> TierLookup;
            public int NumDynamicBodies;

            public void Execute(CollisionEvent collisionEvent)
            {
                // Ground and walkable slopes are static too; only lateral obstruction redirects a dash.
                if (math.abs(collisionEvent.Normal.y) > MaximumWallNormalY) return;

                RedirectDash(collisionEvent.EntityB, collisionEvent.EntityA, collisionEvent.BodyIndexA >= NumDynamicBodies,
                    collisionEvent.Normal);
                RedirectDash(collisionEvent.EntityA, collisionEvent.EntityB, collisionEvent.BodyIndexB >= NumDynamicBodies,
                    collisionEvent.Normal);
            }

            private void RedirectDash(Entity entity, Entity surface, bool isStatic, float3 wallNormal)
            {
                if (!DasherLookup.HasComponent(entity)
                    || !SettingsLookup.HasComponent(entity)
                    || !VelocityLookup.HasComponent(entity)
                    || RespawnLookup.HasComponent(entity) && RespawnLookup.IsComponentEnabled(entity))
                {
                    return;
                }

                DasherState dash = DasherLookup[entity];
                if (dash.Phase != DasherPhase.Dashing) return;
                if (LaunchLookup.TryGetComponent(entity, out var launch) && launch.Phase != EnemyLaunchPhase.Active
                    && !DasherMovementSystem.IsPlayerDash(dash, launch)) return;
                // Moving arena geometry and boss bodies can still make solver contact.
                if (!isStatic && EnemyLookup.HasComponent(surface)
                    && (!TierLookup.TryGetComponent(surface, out var tier) || tier.Tier != KnockbackResponseTier.Boss)) return;

                PhysicsVelocity velocity = VelocityLookup[entity];
                Redirect(ref dash, ref velocity, SettingsLookup[entity].DashSpeed, wallNormal);
                DasherLookup[entity] = dash;
                VelocityLookup[entity] = velocity;
            }
        }

        private static void Redirect(ref DasherState dash, ref PhysicsVelocity velocity, float speed, float3 normal)
        {
            float3 direction = ResolveRedirectedDirection(dash.LockedDirection, velocity.Linear, normal);
            DasherMovementSystem.LockDirection(ref dash, direction);
            velocity.Linear.xz = direction.xz * math.max(0f, speed);
        }

        internal static float3 ResolveRedirectedDirection(
            float3 committedDirection,
            float3 solvedVelocity,
            float3 wallNormal)
        {
            float3 solvedDirection = solvedVelocity;
            solvedDirection.y = 0f;
            if (math.lengthsq(solvedDirection) > 0.0001f)
                return math.normalize(solvedDirection);

            float3 incoming = math.normalizesafe(
                new float3(committedDirection.x, 0f, committedDirection.z),
                math.forward());
            float3 horizontalNormal = math.normalizesafe(
                new float3(wallNormal.x, 0f, wallNormal.z));
            float3 reflected = math.reflect(incoming, horizontalNormal);
            return math.normalizesafe(reflected, -incoming);
        }
    }
}
