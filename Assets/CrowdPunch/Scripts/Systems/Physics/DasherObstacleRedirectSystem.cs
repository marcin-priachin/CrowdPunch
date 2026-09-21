using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
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
    public partial struct DasherObstacleRedirectSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            PhysicsWorld physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            RedirectOnStaticCollisionJob job = new RedirectOnStaticCollisionJob
            {
                DasherLookup = SystemAPI.GetComponentLookup<DasherState>(),
                SettingsLookup = SystemAPI.GetComponentLookup<DasherSettings>(true),
                RespawnLookup = SystemAPI.GetComponentLookup<RespawnRequest>(true),
                VelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(),
                NumDynamicBodies = physicsWorld.NumDynamicBodies
            };

            state.Dependency = job.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        }

        [BurstCompile]
        private struct RedirectOnStaticCollisionJob : ICollisionEventsJob
        {
            private const float MaximumWallNormalY = 0.5f;

            public ComponentLookup<DasherState> DasherLookup;
            [ReadOnly] public ComponentLookup<DasherSettings> SettingsLookup;
            [ReadOnly] public ComponentLookup<RespawnRequest> RespawnLookup;
            public ComponentLookup<PhysicsVelocity> VelocityLookup;
            public int NumDynamicBodies;

            public void Execute(CollisionEvent collisionEvent)
            {
                // Ground and walkable slopes are static too; only lateral obstruction redirects a dash.
                if (math.abs(collisionEvent.Normal.y) > MaximumWallNormalY) return;

                if (collisionEvent.BodyIndexA >= NumDynamicBodies)
                    RedirectDash(collisionEvent.EntityB, collisionEvent.Normal);
                if (collisionEvent.BodyIndexB >= NumDynamicBodies)
                    RedirectDash(collisionEvent.EntityA, collisionEvent.Normal);
            }

            private void RedirectDash(Entity entity, float3 wallNormal)
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

                PhysicsVelocity velocity = VelocityLookup[entity];
                float3 redirected = ResolveRedirectedDirection(
                    dash.LockedDirection,
                    velocity.Linear,
                    wallNormal);
                dash.LockedDirection = redirected;
                dash.LockedRotation = quaternion.LookRotationSafe(redirected, math.up());
                dash.HasLockedRotation = 1;
                DasherLookup[entity] = dash;

                velocity.Linear.xz = redirected.xz * math.max(0f, SettingsLookup[entity].DashSpeed);
                VelocityLookup[entity] = velocity;
            }
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
