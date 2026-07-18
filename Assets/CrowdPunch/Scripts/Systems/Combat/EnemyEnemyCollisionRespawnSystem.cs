using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Lifetime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>
    /// Propagates player-punch pool requests through enemy-enemy collisions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateBefore(typeof(EnemyRespawnSystem))]
    public partial struct EnemyEnemyCollisionRespawnSystem : ISystem
    {
        private const double MaxPendingPoolSeconds = 2d;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
            state.RequireForUpdate<Enemy>();
            state.RequireForUpdate<RespawnRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EnemyCollisionJob job = new EnemyCollisionJob
            {
                EnemyLookup = SystemAPI.GetComponentLookup<Enemy>(true),
                RespawnLookup = SystemAPI.GetComponentLookup<RespawnRequest>(),
                ForcePoolAt = SystemAPI.Time.ElapsedTime + MaxPendingPoolSeconds
            };

            state.Dependency = job.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        }

        [BurstCompile]
        private struct EnemyCollisionJob : ICollisionEventsJob
        {
            [ReadOnly] public ComponentLookup<Enemy> EnemyLookup;
            public ComponentLookup<RespawnRequest> RespawnLookup;

            public double ForcePoolAt;

            public void Execute(CollisionEvent collisionEvent)
            {
                Entity entityA = collisionEvent.EntityA;
                Entity entityB = collisionEvent.EntityB;

                if (!EnemyLookup.HasComponent(entityA)
                    || !EnemyLookup.HasComponent(entityB)
                    || !RespawnLookup.HasComponent(entityA)
                    || !RespawnLookup.HasComponent(entityB))
                {
                    return;
                }

                if (!IsPlayerPunchPending(entityA) && !IsPlayerPunchPending(entityB))
                {
                    return;
                }

                RequestRespawn(entityA);
                RequestRespawn(entityB);
            }

            private bool IsPlayerPunchPending(Entity entity)
            {
                if (!RespawnLookup.IsComponentEnabled(entity))
                {
                    return false;
                }

                RespawnRequest respawnRequest = RespawnLookup[entity];
                return respawnRequest.IsPooled == 0 && respawnRequest.FromPlayerPunch != 0;
            }

            private void RequestRespawn(Entity entity)
            {
                double forcePoolAt = ForcePoolAt;
                if (RespawnLookup.IsComponentEnabled(entity))
                {
                    RespawnRequest existingRequest = RespawnLookup[entity];
                    if (existingRequest.IsPooled != 0)
                    {
                        return;
                    }

                    if (existingRequest.ForcePoolAt > 0d && existingRequest.ForcePoolAt < forcePoolAt)
                    {
                        forcePoolAt = existingRequest.ForcePoolAt;
                    }
                }

                RespawnLookup[entity] = new RespawnRequest
                {
                    RespawnAt = 0d,
                    IsPooled = 0,
                    ForcePoolAt = forcePoolAt,
                    FromPlayerPunch = 1
                };
                RespawnLookup.SetComponentEnabled(entity, true);
            }
        }
    }
}
