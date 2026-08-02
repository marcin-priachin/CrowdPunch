using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>
    /// Interprets solver-resolved enemy impacts and propagates launched state without altering velocity.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GamePostPhysicsGroup))]
    [UpdateBefore(typeof(CrowdPunch.Systems.Physics.EnemyRecoverySystem))]
    public partial struct EnemyLaunchCollisionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<Enemy>();
            state.RequireForUpdate<EnemyLaunchSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EnemyLaunchSettings settings = SystemAPI.GetSingleton<EnemyLaunchSettings>();
            EnemyCollisionJob job = new EnemyCollisionJob
            {
                EnemyLookup = SystemAPI.GetComponentLookup<Enemy>(true),
                LaunchStateLookup = SystemAPI.GetComponentLookup<EnemyLaunchState>(),
                World = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
                MinimumImpulse = math.max(0f, settings.MinimumPropagationImpulse)
            };

            state.Dependency = job.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        }

        [BurstCompile]
        private struct EnemyCollisionJob : ICollisionEventsJob
        {
            [ReadOnly] public ComponentLookup<Enemy> EnemyLookup;
            public ComponentLookup<EnemyLaunchState> LaunchStateLookup;
            [ReadOnly] public PhysicsWorld World;
            public float MinimumImpulse;

            public void Execute(CollisionEvent collisionEvent)
            {
                Entity entityA = collisionEvent.EntityA;
                Entity entityB = collisionEvent.EntityB;

                if (!EnemyLookup.HasComponent(entityA)
                    || !EnemyLookup.HasComponent(entityB)
                    || !LaunchStateLookup.HasComponent(entityA)
                    || !LaunchStateLookup.HasComponent(entityB))
                {
                    return;
                }

                EnemyLaunchPhase phaseA = LaunchStateLookup[entityA].Phase;
                EnemyLaunchPhase phaseB = LaunchStateLookup[entityB].Phase;

                if (phaseA == EnemyLaunchPhase.Launched && phaseB != EnemyLaunchPhase.Launched)
                {
                    TryPropagate(collisionEvent, entityB);
                    return;
                }

                if (phaseB == EnemyLaunchPhase.Launched && phaseA != EnemyLaunchPhase.Launched)
                {
                    TryPropagate(collisionEvent, entityA);
                }
            }

            private void TryPropagate(CollisionEvent collisionEvent, Entity target)
            {
                EnemyLaunchPhase targetPhase = LaunchStateLookup[target].Phase;
                if (targetPhase != EnemyLaunchPhase.Active && targetPhase != EnemyLaunchPhase.Recovering)
                {
                    return;
                }

                CollisionEvent.Details details = collisionEvent.CalculateDetails(ref World);
                if (details.EstimatedImpulse < MinimumImpulse)
                {
                    return;
                }

                EnemyLaunchState targetState = LaunchStateLookup[target];
                targetState.Phase = EnemyLaunchPhase.Launched;
                targetState.LastCause = EnemyLaunchCause.EnemyCollision;
                targetState.BelowUsefulMomentumSeconds = 0f;
                targetState.RecoverySecondsRemaining = 0f;
                targetState.PropagatedLaunchCount++;
                targetState.LastPropagationImpulse = details.EstimatedImpulse;
                LaunchStateLookup[target] = targetState;
            }
        }
    }
}
