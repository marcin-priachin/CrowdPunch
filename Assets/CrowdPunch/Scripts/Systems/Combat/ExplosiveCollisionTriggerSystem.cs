using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>Requests a detonation when an enemy collision contains an explosive and a launched participant.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateBefore(typeof(EnemyLaunchCollisionSystem))]
    public partial struct ExplosiveCollisionTriggerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
            state.RequireForUpdate<ExplosiveEnemySettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TriggerJob job = new TriggerJob
            {
                EnemyLookup = SystemAPI.GetComponentLookup<Enemy>(true),
                LaunchLookup = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                ExplosiveStateLookup = SystemAPI.GetComponentLookup<ExplosiveEnemyState>(true),
                RequestLookup = SystemAPI.GetComponentLookup<ExplosiveDetonationRequest>()
            };
            state.Dependency = job.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        }

        [BurstCompile]
        private struct TriggerJob : ICollisionEventsJob
        {
            [ReadOnly] public ComponentLookup<Enemy> EnemyLookup;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> LaunchLookup;
            [ReadOnly] public ComponentLookup<ExplosiveEnemyState> ExplosiveStateLookup;
            public ComponentLookup<ExplosiveDetonationRequest> RequestLookup;

            public void Execute(CollisionEvent collisionEvent)
            {
                Entity a = collisionEvent.EntityA;
                Entity b = collisionEvent.EntityB;
                if (!EnemyLookup.HasComponent(a) || !EnemyLookup.HasComponent(b)
                    || !LaunchLookup.HasComponent(a) || !LaunchLookup.HasComponent(b))
                {
                    return;
                }

                bool hasLaunchedParticipant = LaunchLookup[a].Phase == EnemyLaunchPhase.Launched
                    || LaunchLookup[b].Phase == EnemyLaunchPhase.Launched;
                if (!hasLaunchedParticipant)
                {
                    return;
                }

                RequestIfAvailable(a);
                RequestIfAvailable(b);
            }

            private void RequestIfAvailable(Entity entity)
            {
                if (ExplosiveStateLookup.HasComponent(entity)
                    && ExplosiveStateLookup[entity].HasExploded == 0
                    && RequestLookup.HasComponent(entity))
                {
                    RequestLookup.SetComponentEnabled(entity, true);
                }
            }
        }
    }
}
