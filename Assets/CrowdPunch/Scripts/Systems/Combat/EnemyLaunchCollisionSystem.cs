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
    /// Interprets solver-resolved enemy impacts, independently resolving launch propagation and damage.
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
                RespawnLookup = SystemAPI.GetComponentLookup<RespawnRequest>(true),
                DamageRequestLookup = SystemAPI.GetComponentLookup<DamageRequest>(),
                DamageHistoryLookup = SystemAPI.GetBufferLookup<CollisionDamageHistory>(),
                DasherSettingsLookup = SystemAPI.GetComponentLookup<DasherSettings>(true),
                DasherStateLookup = SystemAPI.GetComponentLookup<DasherState>(true),
                TierLookup = SystemAPI.GetComponentLookup<EnemyTier>(true),
                World = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
                MinimumPropagationImpulse = math.max(0f, settings.MinimumPropagationImpulse),
                MinimumDamageImpulse = math.max(0f, settings.MinimumDamageImpulse),
                BaseDamageMultiplier = math.max(0f, settings.BaseCollisionDamageMultiplier),
                DamageMultiplierPerExcessImpulse = math.max(0f, settings.DamageMultiplierPerExcessImpulse),
                MaximumDamageMultiplier = math.max(0f, settings.MaximumCollisionDamageMultiplier)
            };

            state.Dependency = job.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        }

        [BurstCompile]
        private struct EnemyCollisionJob : ICollisionEventsJob
        {
            [ReadOnly] public ComponentLookup<Enemy> EnemyLookup;
            public ComponentLookup<EnemyLaunchState> LaunchStateLookup;
            [ReadOnly] public ComponentLookup<RespawnRequest> RespawnLookup;
            public ComponentLookup<DamageRequest> DamageRequestLookup;
            public BufferLookup<CollisionDamageHistory> DamageHistoryLookup;
            [ReadOnly] public ComponentLookup<DasherSettings> DasherSettingsLookup;
            [ReadOnly] public ComponentLookup<DasherState> DasherStateLookup;
            [ReadOnly] public ComponentLookup<EnemyTier> TierLookup;
            [ReadOnly] public PhysicsWorld World;
            public float MinimumPropagationImpulse;
            public float MinimumDamageImpulse;
            public float BaseDamageMultiplier;
            public float DamageMultiplierPerExcessImpulse;
            public float MaximumDamageMultiplier;

            public void Execute(CollisionEvent collisionEvent)
            {
                Entity entityA = collisionEvent.EntityA;
                Entity entityB = collisionEvent.EntityB;

                if (!EnemyLookup.HasComponent(entityA)
                    || !EnemyLookup.HasComponent(entityB)
                    || !LaunchStateLookup.HasComponent(entityA)
                    || !LaunchStateLookup.HasComponent(entityB)
                    || IsUnavailable(entityA)
                    || IsUnavailable(entityB))
                {
                    return;
                }

                EnemyLaunchPhase phaseA = LaunchStateLookup[entityA].Phase;
                EnemyLaunchPhase phaseB = LaunchStateLookup[entityB].Phase;

                if (phaseA == EnemyLaunchPhase.Launched && phaseB != EnemyLaunchPhase.Launched)
                {
                    ResolveImpact(collisionEvent, entityA, entityB, phaseB);
                    return;
                }

                if (phaseB == EnemyLaunchPhase.Launched && phaseA != EnemyLaunchPhase.Launched)
                {
                    ResolveImpact(collisionEvent, entityB, entityA, phaseA);
                }
            }

            private bool IsUnavailable(Entity entity)
            {
                return RespawnLookup.HasComponent(entity) && RespawnLookup.IsComponentEnabled(entity);
            }

            private void ResolveImpact(
                CollisionEvent collisionEvent,
                Entity source,
                Entity target,
                EnemyLaunchPhase targetPhase)
            {
                if (DasherSettingsLookup.HasComponent(source)) return;
                if (DasherStateLookup.HasComponent(target)
                    && DasherStateLookup[target].Phase == DasherPhase.Dashing) return;
                if (targetPhase != EnemyLaunchPhase.Active && targetPhase != EnemyLaunchPhase.Recovering)
                {
                    return;
                }

                CollisionEvent.Details details = collisionEvent.CalculateDetails(ref World);
                float estimatedImpulse = math.max(0f, details.EstimatedImpulse);

                // Establish launch before queuing damage so lethal collision damage is deferred deterministically.
                if (estimatedImpulse >= MinimumPropagationImpulse)
                {
                    PropagateLaunch(source, target, estimatedImpulse);
                }

                TryQueueDamage(source, target, estimatedImpulse);
            }

            private void PropagateLaunch(Entity source, Entity target, float estimatedImpulse)
            {
                if (!TierLookup.HasComponent(target)
                    || !EnemyLaunchTransition.IsLaunchable(TierLookup[target])) return;
                EnemyLaunchState sourceState = LaunchStateLookup[source];
                EnemyLaunchState targetState = LaunchStateLookup[target];
                EnemyLaunchTransition.Begin(
                    ref targetState,
                    EnemyLaunchCause.EnemyCollision,
                    sourceState.LaunchDamage);
                targetState.PropagatedLaunchCount++;
                targetState.LastPropagationImpulse = estimatedImpulse;
                LaunchStateLookup[target] = targetState;
            }

            private void TryQueueDamage(Entity source, Entity target, float estimatedImpulse)
            {
                if (estimatedImpulse < MinimumDamageImpulse
                    || !DamageRequestLookup.HasComponent(target)
                    || !DamageHistoryLookup.HasBuffer(target))
                {
                    return;
                }

                uint sourceLaunchSequence = LaunchStateLookup[source].LaunchSequence;
                DynamicBuffer<CollisionDamageHistory> history = DamageHistoryLookup[target];
                for (int index = 0; index < history.Length; index++)
                {
                    CollisionDamageHistory entry = history[index];
                    if (entry.Source == source && entry.SourceLaunchSequence == sourceLaunchSequence)
                    {
                        return;
                    }
                }

                float launchDamage = math.max(0f, LaunchStateLookup[source].LaunchDamage);
                float damageMultiplier = math.min(
                    MaximumDamageMultiplier,
                    BaseDamageMultiplier
                    + (estimatedImpulse - MinimumDamageImpulse) * DamageMultiplierPerExcessImpulse);
                float damage = launchDamage * damageMultiplier;
                if (damage <= 0f)
                {
                    return;
                }

                history.Add(new CollisionDamageHistory
                {
                    Source = source,
                    SourceLaunchSequence = sourceLaunchSequence
                });

                DamageRequest pendingDamage = DamageRequestLookup.IsComponentEnabled(target)
                    ? DamageRequestLookup[target]
                    : default;
                pendingDamage.Amount += damage;
                DamageRequestLookup[target] = pendingDamage;
                DamageRequestLookup.SetComponentEnabled(target, true);
            }
        }
    }
}
