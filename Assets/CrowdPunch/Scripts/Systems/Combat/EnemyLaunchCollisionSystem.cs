using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace CrowdPunch.Systems.Combat
{
    /// <summary>
    /// Propagates launched state and attenuated physical motion through qualifying enemy collisions.
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
                VelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(),
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                MinimumRelativeSpeed = math.max(0f, settings.MinimumPropagationRelativeSpeed),
                VelocityFactor = math.clamp(settings.PropagatedVelocityFactor, 0f, 1f)
            };

            state.Dependency = job.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        }

        [BurstCompile]
        private struct EnemyCollisionJob : ICollisionEventsJob
        {
            [ReadOnly] public ComponentLookup<Enemy> EnemyLookup;
            public ComponentLookup<EnemyLaunchState> LaunchStateLookup;
            public ComponentLookup<PhysicsVelocity> VelocityLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            public float MinimumRelativeSpeed;
            public float VelocityFactor;

            public void Execute(CollisionEvent collisionEvent)
            {
                Entity entityA = collisionEvent.EntityA;
                Entity entityB = collisionEvent.EntityB;

                if (!EnemyLookup.HasComponent(entityA)
                    || !EnemyLookup.HasComponent(entityB)
                    || !LaunchStateLookup.HasComponent(entityA)
                    || !LaunchStateLookup.HasComponent(entityB)
                    || !VelocityLookup.HasComponent(entityA)
                    || !VelocityLookup.HasComponent(entityB)
                    || !TransformLookup.HasComponent(entityA)
                    || !TransformLookup.HasComponent(entityB))
                {
                    return;
                }

                EnemyLaunchPhase phaseA = LaunchStateLookup[entityA].Phase;
                EnemyLaunchPhase phaseB = LaunchStateLookup[entityB].Phase;

                if (phaseA == EnemyLaunchPhase.Launched && phaseB != EnemyLaunchPhase.Launched)
                {
                    TryPropagate(entityA, entityB);
                    return;
                }

                if (phaseB == EnemyLaunchPhase.Launched && phaseA != EnemyLaunchPhase.Launched)
                {
                    TryPropagate(entityB, entityA);
                }
            }

            private void TryPropagate(Entity source, Entity target)
            {
                EnemyLaunchPhase targetPhase = LaunchStateLookup[target].Phase;
                if (targetPhase != EnemyLaunchPhase.Active && targetPhase != EnemyLaunchPhase.Recovering)
                {
                    return;
                }

                float3 sourceVelocity = VelocityLookup[source].Linear;
                float3 targetVelocity = VelocityLookup[target].Linear;
                float relativeSpeed = math.length(sourceVelocity - targetVelocity);
                if (relativeSpeed < MinimumRelativeSpeed)
                {
                    return;
                }

                float3 sourceToTarget = TransformLookup[target].Position - TransformLookup[source].Position;
                float3 relativeVelocity = sourceVelocity - targetVelocity;
                float3 fallbackDirection = math.normalizesafe(sourceToTarget);
                float3 transferredVelocity = math.normalizesafe(relativeVelocity, fallbackDirection)
                    * math.length(relativeVelocity)
                    * VelocityFactor;
                if (math.lengthsq(transferredVelocity) <= 0f)
                {
                    return;
                }

                PhysicsVelocity targetPhysicsVelocity = VelocityLookup[target];
                targetPhysicsVelocity.Linear += transferredVelocity;
                VelocityLookup[target] = targetPhysicsVelocity;

                EnemyLaunchState targetState = LaunchStateLookup[target];
                targetState.Phase = EnemyLaunchPhase.Launched;
                targetState.LastCause = EnemyLaunchCause.EnemyCollision;
                targetState.BelowUsefulMomentumSeconds = 0f;
                targetState.RecoverySecondsRemaining = 0f;
                targetState.PropagatedLaunchCount++;
                LaunchStateLookup[target] = targetState;
            }
        }
    }
}
