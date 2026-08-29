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
    public partial struct DasherObstacleStopSystem : ISystem
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
            StopOnStaticCollisionJob job = new StopOnStaticCollisionJob
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
        private struct StopOnStaticCollisionJob : ICollisionEventsJob
        {
            private const float MaximumWallNormalY = 0.5f;

            public ComponentLookup<DasherState> DasherLookup;
            [ReadOnly] public ComponentLookup<DasherSettings> SettingsLookup;
            [ReadOnly] public ComponentLookup<RespawnRequest> RespawnLookup;
            public ComponentLookup<PhysicsVelocity> VelocityLookup;
            public int NumDynamicBodies;

            public void Execute(CollisionEvent collisionEvent)
            {
                // Ground and walkable slopes are static too; only lateral obstruction ends a dash.
                if (math.abs(collisionEvent.Normal.y) > MaximumWallNormalY) return;

                if (collisionEvent.BodyIndexA >= NumDynamicBodies)
                    StopDash(collisionEvent.EntityB);
                if (collisionEvent.BodyIndexB >= NumDynamicBodies)
                    StopDash(collisionEvent.EntityA);
            }

            private void StopDash(Entity entity)
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

                dash.Phase = DasherPhase.Recovering;
                dash.SecondsRemaining = math.max(0f, SettingsLookup[entity].RecoveryDuration);
                DasherLookup[entity] = dash;

                PhysicsVelocity velocity = VelocityLookup[entity];
                velocity.Linear.xz = float2.zero;
                VelocityLookup[entity] = velocity;
            }
        }
    }
}
