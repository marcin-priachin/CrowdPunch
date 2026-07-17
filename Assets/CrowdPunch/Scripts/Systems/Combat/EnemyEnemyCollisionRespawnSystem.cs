using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Lifetime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>
    /// Returns fast enemy-enemy collisions to the respawn pool.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateBefore(typeof(EnemyRespawnSystem))]
    public partial struct EnemyEnemyCollisionRespawnSystem : ISystem
    {
        private const float CollisionSpeedThreshold = 20f;
        private const double RespawnDelaySeconds = 5d;

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
                VelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(true),
                RespawnLookup = SystemAPI.GetComponentLookup<RespawnRequest>(),
                RespawnAt = SystemAPI.Time.ElapsedTime + RespawnDelaySeconds,
                CollisionSpeedThresholdSq = CollisionSpeedThreshold * CollisionSpeedThreshold
            };

            state.Dependency = job.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        }

        [BurstCompile]
        private struct EnemyCollisionJob : ICollisionEventsJob
        {
            [ReadOnly] public ComponentLookup<Enemy> EnemyLookup;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> VelocityLookup;
            public ComponentLookup<RespawnRequest> RespawnLookup;

            public double RespawnAt;
            public float CollisionSpeedThresholdSq;

            public void Execute(CollisionEvent collisionEvent)
            {
                Entity entityA = collisionEvent.EntityA;
                Entity entityB = collisionEvent.EntityB;

                if (!EnemyLookup.HasComponent(entityA)
                    || !EnemyLookup.HasComponent(entityB)
                    || !VelocityLookup.HasComponent(entityA)
                    || !VelocityLookup.HasComponent(entityB)
                    || !RespawnLookup.HasComponent(entityA)
                    || !RespawnLookup.HasComponent(entityB))
                {
                    return;
                }

                float3 relativeVelocity = VelocityLookup[entityA].Linear - VelocityLookup[entityB].Linear;
                if (math.lengthsq(relativeVelocity) <= CollisionSpeedThresholdSq)
                {
                    return;
                }

                RequestRespawn(entityA);
                RequestRespawn(entityB);
            }

            private void RequestRespawn(Entity entity)
            {
                RespawnLookup[entity] = new RespawnRequest
                {
                    RespawnAt = RespawnAt,
                    IsPooled = 0
                };
                RespawnLookup.SetComponentEnabled(entity, true);
            }
        }
    }
}
