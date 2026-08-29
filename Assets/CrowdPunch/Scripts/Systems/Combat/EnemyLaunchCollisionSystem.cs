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
            EntityQuery correctionCandidateQuery = SystemAPI.QueryBuilder()
                .WithAll<Enemy, LocalTransform, EnemyLaunchState, Health>()
                .Build();
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
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                HealthLookup = SystemAPI.GetComponentLookup<Health>(true),
                VelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(),
                CorrectionCandidates = correctionCandidateQuery.ToEntityArray(Allocator.TempJob),
                World = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
                MinimumPropagationImpulse = math.max(0f, settings.MinimumPropagationImpulse),
                PropagationAimCorrectionRadius = math.max(0f, settings.PropagationAimCorrectionRadius),
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
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<Health> HealthLookup;
            public ComponentLookup<PhysicsVelocity> VelocityLookup;
            [ReadOnly, DeallocateOnJobCompletion] public NativeArray<Entity> CorrectionCandidates;
            [ReadOnly] public PhysicsWorld World;
            public float MinimumPropagationImpulse;
            public float PropagationAimCorrectionRadius;
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
                    sourceState.LaunchDamage,
                    sourceState.Owner);
                targetState.PropagatedLaunchCount++;
                targetState.LastPropagationImpulse = estimatedImpulse;
                LaunchStateLookup[target] = targetState;
                CorrectPropagatedDirection(source, target);
            }

            private void CorrectPropagatedDirection(Entity source, Entity launchedTarget)
            {
                if (PropagationAimCorrectionRadius <= 0f
                    || !VelocityLookup.HasComponent(launchedTarget)
                    || !TransformLookup.HasComponent(launchedTarget)) return;

                PhysicsVelocity velocity = VelocityLookup[launchedTarget];
                float horizontalSpeed = math.length(velocity.Linear.xz);
                if (horizontalSpeed <= 0.0001f) return;

                float3 initialDirection = new float3(velocity.Linear.x, 0f, velocity.Linear.z) / horizontalSpeed;
                float3 launchedPosition = TransformLookup[launchedTarget].Position;
                float radiusSq = PropagationAimCorrectionRadius * PropagationAimCorrectionRadius;
                Entity best = Entity.Null;
                float bestDot = float.MinValue;
                float bestDistanceSq = float.MaxValue;

                for (int i = 0; i < CorrectionCandidates.Length; i++)
                {
                    Entity candidate = CorrectionCandidates[i];
                    if (candidate == source || candidate == launchedTarget
                        || IsUnavailable(candidate)
                        || !LaunchStateLookup.HasComponent(candidate)
                        || !TransformLookup.HasComponent(candidate)
                        || !HealthLookup.HasComponent(candidate)) continue;

                    EnemyLaunchState candidateLaunch = LaunchStateLookup[candidate];
                    if ((candidateLaunch.Phase != EnemyLaunchPhase.Active
                         && candidateLaunch.Phase != EnemyLaunchPhase.Recovering)
                        || HealthLookup[candidate].Current <= 0f) continue;

                    float3 offset = TransformLookup[candidate].Position - launchedPosition;
                    offset.y = 0f;
                    float distanceSq = math.lengthsq(offset);
                    if (distanceSq <= 0.000001f || distanceSq > radiusSq) continue;

                    float dot = math.dot(initialDirection, offset * math.rsqrt(distanceSq));
                    if (dot > bestDot
                        || dot == bestDot && distanceSq < bestDistanceSq
                        || dot == bestDot && distanceSq == bestDistanceSq
                        && (best == Entity.Null || candidate.Index < best.Index))
                    {
                        best = candidate;
                        bestDot = dot;
                        bestDistanceSq = distanceSq;
                    }
                }

                if (best == Entity.Null) return;
                EnemyLaunchState launchedState = LaunchStateLookup[launchedTarget];
                launchedState.HomingTarget = best;
                LaunchStateLookup[launchedTarget] = launchedState;
                float3 correctedDirection = TransformLookup[best].Position - launchedPosition;
                correctedDirection.y = 0f;
                correctedDirection = math.normalizesafe(correctedDirection, initialDirection);
                velocity.Linear.xz = correctedDirection.xz * horizontalSpeed;
                VelocityLookup[launchedTarget] = velocity;
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

                float damage = EnemyCollisionDamage.Calculate(
                    LaunchStateLookup[source].LaunchDamage,
                    estimatedImpulse,
                    new EnemyLaunchSettings
                    {
                        MinimumDamageImpulse = MinimumDamageImpulse,
                        BaseCollisionDamageMultiplier = BaseDamageMultiplier,
                        DamageMultiplierPerExcessImpulse = DamageMultiplierPerExcessImpulse,
                        MaximumCollisionDamageMultiplier = MaximumDamageMultiplier
                    });
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
