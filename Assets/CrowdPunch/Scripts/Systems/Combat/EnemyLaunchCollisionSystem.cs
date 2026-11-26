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
                .WithAll<LocalTransform>().WithAny<Enemy, Barricade>()
                .Build();
            var damage = new CollisionDamageQueue
            {
                Requests = SystemAPI.GetComponentLookup<DamageRequest>(),
                History = SystemAPI.GetBufferLookup<CollisionDamageHistory>(),
                Health = SystemAPI.GetComponentLookup<Health>(true),
                Settings = settings
            };
            EnemyCollisionJob job = new EnemyCollisionJob
            {
                MushroomImpacts = SystemAPI.GetBufferLookup<MushroomBeamImpact>(),
                SpikeBursts = SystemAPI.GetBufferLookup<AlienSpikeBurst>(),
                SpikeHistory = SystemAPI.GetBufferLookup<AlienSpikeBurstHistory>(),
                Lifetimes = SystemAPI.GetComponentLookup<EnemyLifetime>(true),
                SnakeHeads = SystemAPI.GetComponentLookup<SnakeHead>(true),
                Commitments = SystemAPI.GetComponentLookup<CommittedLaunchFlight>(),
                AuthoritativePaths = SystemAPI.GetComponentLookup<AuthoritativeLaunchPath>(true),
                PairEndpoints = SystemAPI.GetComponentLookup<LinkedPairEndpoint>(true),
                SnakeSegments = SystemAPI.GetComponentLookup<SnakeSegment>(),
                Armors = SystemAPI.GetComponentLookup<EnemyArmor>(),
                Barricades = SystemAPI.GetComponentLookup<Barricade>(true),
                ArmorSettings = SystemAPI.GetComponentLookup<EnemyArmorSettings>(true),
                ArmorHistory = SystemAPI.GetBufferLookup<ArmorHitHistory>(),
                MassLookup = SystemAPI.GetComponentLookup<PhysicsMass>(true),
                FeedbackLookup = SystemAPI.GetComponentLookup<EnemyImpactFeedback>(),
                ElapsedTime = SystemAPI.Time.ElapsedTime,
                EnemyLookup = SystemAPI.GetComponentLookup<Enemy>(true),
                LaunchStateLookup = SystemAPI.GetComponentLookup<EnemyLaunchState>(),
                RespawnLookup = SystemAPI.GetComponentLookup<RespawnRequest>(true),
                Damage = damage,
                DasherSettingsLookup = SystemAPI.GetComponentLookup<DasherSettings>(true),
                DasherStateLookup = SystemAPI.GetComponentLookup<DasherState>(true),
                TierLookup = SystemAPI.GetComponentLookup<EnemyTier>(true),
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                HealthLookup = SystemAPI.GetComponentLookup<Health>(true),
                VelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(),
                CorrectionCandidates = settings.PropagationAimCorrectionRadius > 0f
                    ? correctionCandidateQuery.ToEntityArray(Allocator.TempJob)
                    : new NativeArray<Entity>(0, Allocator.TempJob),
                World = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
                MinimumPropagationImpulse = math.max(0f, settings.MinimumPropagationImpulse),
                PropagationAimCorrectionRadius = math.max(0f, settings.PropagationAimCorrectionRadius),
                PropagationAimMaximumAngleDegrees = settings.PropagationAimMaximumAngleDegrees,
                HomingDuration = settings.LaunchHomingDuration,
                LeadSeconds = settings.AimLeadSeconds,
                LeadMaximumDistance = settings.AimLeadMaximumDistance,
                MinimumDamageImpulse = math.max(0f, settings.MinimumDamageImpulse)
            };

            state.Dependency = job.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);

            // The motor can turn at a verified sweep contact without a solver event.
            state.Dependency = new SweptContactJob
            {
                Damage = damage,
                Enemies = SystemAPI.GetComponentLookup<Enemy>(true),
                Dashers = SystemAPI.GetComponentLookup<DasherSettings>(true),
                Launches = SystemAPI.GetComponentLookup<EnemyLaunchState>(true),
                Tiers = SystemAPI.GetComponentLookup<EnemyTier>(true),
                Respawns = SystemAPI.GetComponentLookup<RespawnRequest>(true),
                Feedbacks = SystemAPI.GetComponentLookup<EnemyImpactFeedback>(),
                Now = SystemAPI.Time.ElapsedTime
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        private struct EnemyCollisionJob : ICollisionEventsJob
        {
            public ComponentLookup<CommittedLaunchFlight> Commitments;
            [ReadOnly] public ComponentLookup<AuthoritativeLaunchPath> AuthoritativePaths;
            public BufferLookup<MushroomBeamImpact> MushroomImpacts;
            public BufferLookup<AlienSpikeBurst> SpikeBursts;
            public BufferLookup<AlienSpikeBurstHistory> SpikeHistory;
            [ReadOnly] public ComponentLookup<EnemyLifetime> Lifetimes;
            [ReadOnly] public ComponentLookup<SnakeHead> SnakeHeads;
            [ReadOnly] public ComponentLookup<LinkedPairEndpoint> PairEndpoints;
            public ComponentLookup<SnakeSegment> SnakeSegments;
            public ComponentLookup<EnemyArmor> Armors;
            [ReadOnly] public ComponentLookup<Barricade> Barricades;
            [ReadOnly] public ComponentLookup<EnemyArmorSettings> ArmorSettings;
            public BufferLookup<ArmorHitHistory> ArmorHistory;
            [ReadOnly] public ComponentLookup<PhysicsMass> MassLookup;
            public ComponentLookup<EnemyImpactFeedback> FeedbackLookup;
            public double ElapsedTime;
            [ReadOnly] public ComponentLookup<Enemy> EnemyLookup;
            public ComponentLookup<EnemyLaunchState> LaunchStateLookup;
            [ReadOnly] public ComponentLookup<RespawnRequest> RespawnLookup;
            public CollisionDamageQueue Damage;
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
            public float PropagationAimMaximumAngleDegrees, HomingDuration, LeadSeconds, LeadMaximumDistance;
            public float MinimumDamageImpulse;

            public void Execute(CollisionEvent collisionEvent)
            {
                Entity entityA = collisionEvent.EntityA;
                Entity entityB = collisionEvent.EntityB;
                if (LinkedPairLaunchResolution.ArePartners(PairEndpoints, entityA, entityB)) return;
                RecordCommittedContact(collisionEvent, entityA, entityB, collisionEvent.Normal);
                RecordCommittedContact(collisionEvent, entityB, entityA, -collisionEvent.Normal);
                StopHomingOnImpact(collisionEvent, entityA);
                StopHomingOnImpact(collisionEvent, entityB);

                RecordSnakeBounce(collisionEvent, entityA, entityB, collisionEvent.BodyIndexB >= World.NumDynamicBodies);
                RecordSnakeBounce(collisionEvent, entityB, entityA, collisionEvent.BodyIndexA >= World.NumDynamicBodies);

                bool enemyA = EnemyLookup.HasComponent(entityA);
                bool enemyB = EnemyLookup.HasComponent(entityB);
                if (enemyA != enemyB)
                {
                    Entity body = enemyA ? entityA : entityB;
                    Entity surface = enemyA ? entityB : entityA;
                    if (!IsUnavailable(body) && LaunchStateLookup.HasComponent(body)
                        && math.abs(collisionEvent.Normal.y) < .7f)
                    {
                        var launch = LaunchStateLookup[body];
                        if (launch.Phase == EnemyLaunchPhase.Launched
                            && launch.Owner == EnemyLaunchOwner.Player
                            && PropagationAimCorrectionRadius > 0f && PropagationAimMaximumAngleDegrees > 0f)
                        {
                            var details = collisionEvent.CalculateDetails(ref World);
                            if (LaunchContactResponse.Meaningful(collisionEvent.Normal, details.EstimatedImpulse, MinimumPropagationImpulse))
                            {
                                launch.RicochetSurface = surface;
                                launch.RicochetNormal = enemyA ? collisionEvent.Normal : -collisionEvent.Normal;
                                LaunchStateLookup[body] = launch;
                            }
                        }
                    }
                    if (!IsUnavailable(body) && LaunchStateLookup.HasComponent(body)
                        && LaunchStateLookup[body].Phase == EnemyLaunchPhase.Launched && FeedbackLookup.HasComponent(body))
                    {
                        var feedback = FeedbackLookup[body];
                        if (ElapsedTime >= feedback.NextContactTime)
                        {
                            float speed = math.abs(math.dot(feedback.IncomingVelocity, collisionEvent.Normal));
                            if (speed <= .0001f) return;
                            var contact = collisionEvent.CalculateDetails(ref World);
                            ImpactFeedbackRecording.RecordWorldContact(ref feedback,
                                contact.AverageContactPointPosition, enemyA ? collisionEvent.Normal : -collisionEvent.Normal,
                                speed, contact.EstimatedImpulse, LaunchStateLookup[body], ElapsedTime);
                            FeedbackLookup[body] = feedback;
                        }
                    }
                    return;
                }
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

            private void RecordCommittedContact(CollisionEvent collision, Entity source, Entity other, float3 normal)
            {
                if (!Commitments.HasComponent(source) || !Commitments.IsComponentEnabled(source)
                    || !LaunchStateLookup.HasComponent(source)) return;
                var flight = Commitments[source];
                var launch = LaunchStateLookup[source];
                if (flight.LaunchSequence != launch.LaunchSequence || flight.Cancelled != 0
                    || launch.Phase != EnemyLaunchPhase.Launched || math.abs(normal.y) > .7f) return;
                var details = collision.CalculateDetails(ref World);
                if (!LaunchContactResponse.Meaningful(normal, details.EstimatedImpulse, MinimumPropagationImpulse)) return;
                CommittedLaunchPlan.RecordContact(ref flight, other, normal);
                Commitments[source] = flight;
            }

            private void StopHomingOnImpact(CollisionEvent collision, Entity entity)
            {
                if (!LaunchStateLookup.HasComponent(entity) || math.abs(collision.Normal.y) > .7f) return;
                var launch = LaunchStateLookup[entity];
                if (launch.Phase != EnemyLaunchPhase.Launched || launch.HomingTarget == Entity.Null) return;
                var details = collision.CalculateDetails(ref World);
                if (!LaunchContactResponse.Meaningful(collision.Normal, details.EstimatedImpulse, MinimumPropagationImpulse)) return;
                LaunchAimPrediction.StopHoming(ref launch);
                LaunchStateLookup[entity] = launch;
            }

            private void RecordSnakeBounce(CollisionEvent collision, Entity source, Entity other, bool staticSurface)
            {
                if (!SnakeSegments.HasComponent(source) || !FeedbackLookup.HasComponent(source)
                    || !LaunchStateLookup.HasComponent(source)
                    || LaunchStateLookup[source].Phase != EnemyLaunchPhase.Launched) return;
                var segment = SnakeSegments[source];
                bool wall = staticSurface && !EnemyLookup.HasComponent(other) && math.abs(collision.Normal.y) <= .7f;
                if (segment.Detached == 0 || (segment.BouncedOutsideOwner != 0 && !wall) || other == segment.Head
                    || SnakeSegments.HasComponent(other) && SnakeSegments[other].Head == segment.Head
                        && SnakeSegments[other].Generation == segment.Generation) return;
                float3 incoming = FeedbackLookup[source].IncomingVelocity;
                if (FeedbackLookup.HasComponent(other)) incoming -= FeedbackLookup[other].IncomingVelocity;
                if (math.abs(math.dot(incoming.xz, collision.Normal.xz)) <= .1f) return;
                var details = collision.CalculateDetails(ref World);
                SnakeProjectileCollision.RecordExternalBounce(ref segment, incoming, collision.Normal, details.EstimatedImpulse);
                if (wall)
                    SnakeProjectileCollision.RecordWallBounce(ref segment, incoming, collision.Normal,
                        details.EstimatedImpulse, details.AverageContactPointPosition, ElapsedTime);
                SnakeSegments[source] = segment;
            }

            private void ResolveImpact(
                CollisionEvent collisionEvent,
                Entity source,
                Entity target,
                EnemyLaunchPhase targetPhase)
            {
                if (SnakeHitRules.Protected(target, SnakeHeads, SnakeSegments)) return;
                if (DasherSettingsLookup.HasComponent(source)) return;
                if (DasherStateLookup.HasComponent(target)
                    && DasherStateLookup[target].Phase == DasherPhase.Dashing) return;
                if (targetPhase != EnemyLaunchPhase.Active && targetPhase != EnemyLaunchPhase.Recovering)
                {
                    return;
                }

                CollisionEvent.Details details = collisionEvent.CalculateDetails(ref World);
                float estimatedImpulse = math.max(0f, details.EstimatedImpulse);

                var triggeringLaunch = LaunchStateLookup[source];
                float threshold = math.min(MinimumDamageImpulse, MinimumPropagationImpulse);
                if (Armors.HasComponent(target) && Armors[target].Stages > 0)
                    threshold = MinimumPropagationImpulse;
                if (triggeringLaunch.Owner == EnemyLaunchOwner.Player
                    && estimatedImpulse >= math.max(.01f, threshold)
                    && (SpikeBursts.HasBuffer(source) || SpikeBursts.HasBuffer(target)))
                {
                    uint lifetime = Lifetimes.HasComponent(source) ? Lifetimes[source].Generation : 0;
                    RecordSpikeBurst(source, source, triggeringLaunch, lifetime);
                    RecordSpikeBurst(target, source, triggeringLaunch, lifetime);
                }

                if (Armors.HasComponent(target))
                {
                    // Use the existing meaningful-impact thresholds, independent of launch ownership.
                    var armor = Armors[target];
                    if (armor.Stages > 0 && estimatedImpulse < math.max(.01f, MinimumPropagationImpulse)) return;
                    float damage = EnemyCollisionDamage.Calculate(LaunchStateLookup[source].LaunchDamage,
                        estimatedImpulse, Damage.Settings);
                    var outcome = ArmorHitResolution.Resolve(ref armor, ArmorSettings[target], ArmorHistory[target],
                        source, LaunchStateLookup[source].LaunchSequence, ElapsedTime, damage);
                    Armors[target] = armor;
                    if (outcome == ArmorHitOutcome.Blocked) return;
                    if (outcome == ArmorHitOutcome.Absorbed)
                    {
                        if (VelocityLookup.HasComponent(target))
                        {
                            var velocity = VelocityLookup[target];
                            float3 direction = TransformLookup[target].Position - TransformLookup[source].Position;
                            velocity.Linear.xz = math.normalizesafe(direction.xz, new float2(0, 1)) * ArmorSettings[target].KnockbackSpeed;
                            VelocityLookup[target] = velocity;
                        }
                        return;
                    }
                }
                // Establish launch before queuing damage so lethal collision damage is deferred deterministically.
                if (estimatedImpulse >= MinimumPropagationImpulse)
                {
                    PropagateLaunch(source, target, estimatedImpulse);
                }

                TryQueueDamage(source, target, collisionEvent, estimatedImpulse);
                if (FeedbackLookup.HasComponent(source) && FeedbackLookup.HasComponent(target))
                {
                    var feedback = FeedbackLookup[target];
                    float3 relative = FeedbackLookup[source].IncomingVelocity - feedback.IncomingVelocity;
                    // Only closing speed along the contact normal counts, not grazing travel speed.
                    float speed = math.abs(math.dot(relative, collisionEvent.Normal));
                    // A body may receive and transfer momentum within the same solver step.
                    // Its pre-step speed is then zero; impulse/mass estimates that transferred delta-v.
                    if (MassLookup.HasComponent(source))
                        speed = math.max(speed, estimatedImpulse * MassLookup[source].InverseMass);
                    var significance = LaunchStateLookup[source];
                    significance.FeedbackChainDepth = math.min(64, significance.FeedbackChainDepth + 1);
                    ImpactFeedbackRecording.Record(ref feedback, CombatImpactKind.EnemyCollision,
                        details.AverageContactPointPosition, math.normalizesafe(relative,
                            source == collisionEvent.EntityA ? -collisionEvent.Normal : collisionEvent.Normal), speed,
                        estimatedImpulse, significance, ElapsedTime);
                    FeedbackLookup[target] = feedback;
                }
            }

            private void RecordSpikeBurst(Entity spike, Entity source, in EnemyLaunchState launch, uint lifetime)
            {
                if (!SpikeBursts.HasBuffer(spike)) return;
                var contact = new AlienSpikeBurst
                {
                    Source = source, LaunchSequence = launch.LaunchSequence, SourceLifetime = lifetime,
                    PunchSequence = launch.OriginatingPunchSequence, ChainDepth = launch.FeedbackChainDepth + 1
                };
                if (!AlienSpikeBurstRecording.TryRecord(SpikeHistory[spike], contact)) return;
                contact.Pose = TransformLookup[spike];
                SpikeBursts[spike].Add(contact);
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
                    sourceState.Owner, sourceState.OriginatingPunchSequence);
                targetState.FallbackChain = EnemyLaunchTransition.GetFallbackChain(source,
                    Lifetimes.HasComponent(source) ? Lifetimes[source].Generation : 0, sourceState);
                targetState.FeedbackChainDepth = math.min(64, sourceState.FeedbackChainDepth + 1);
                targetState.PropagatedLaunchCount++;
                targetState.LastPropagationImpulse = estimatedImpulse;
                LaunchStateLookup[target] = targetState;
                CorrectPropagatedDirection(source, target);
                if (targetState.Owner == EnemyLaunchOwner.Player && MushroomImpacts.TryGetBuffer(target, out var impacts))
                {
                    float3 position = TransformLookup[target].Position;
                    float3 outward = MushroomShotGeometry.Horizontal(position - TransformLookup[source].Position, math.forward());
                    float3 direction = MushroomShotGeometry.Horizontal(VelocityLookup[target].Linear, outward);
                    impacts.Add(new MushroomBeamImpact
                    {
                        Position = position, Direction = direction, PunchSequence = targetState.OriginatingPunchSequence,
                        ChainDepth = targetState.FeedbackChainDepth
                    });
                }
            }

            private void CorrectPropagatedDirection(Entity source, Entity launchedTarget)
            {
                if (PropagationAimCorrectionRadius <= 0f || PropagationAimMaximumAngleDegrees <= 0f
                    || !VelocityLookup.HasComponent(launchedTarget)
                    || !TransformLookup.HasComponent(launchedTarget)) return;

                PhysicsVelocity velocity = VelocityLookup[launchedTarget];
                float horizontalSpeed = math.length(velocity.Linear.xz);
                if (horizontalSpeed <= 0.0001f) return;

                float3 initialDirection = new float3(velocity.Linear.x, 0f, velocity.Linear.z) / horizontalSpeed;
                float3 launchedPosition = TransformLookup[launchedTarget].Position;
                Entity best = Entity.Null;
                float bestDot = float.MinValue;
                float bestDistanceSq = float.MaxValue;

                for (int i = 0; i < CorrectionCandidates.Length; i++)
                {
                    Entity candidate = CorrectionCandidates[i];
                    if (candidate == source || candidate == launchedTarget || !TransformLookup.HasComponent(candidate)
                        || LinkedPairLaunchResolution.ArePartners(PairEndpoints, launchedTarget, candidate)) continue;
                    float3 offset = TransformLookup[candidate].Position - launchedPosition;
                    if (!LaunchAimPrediction.BetterCorrection(initialDirection, offset, PropagationAimCorrectionRadius,
                            PropagationAimMaximumAngleDegrees, candidate.Index, best == Entity.Null ? int.MaxValue : best.Index,
                            bestDot, bestDistanceSq, out float dot, out float distanceSq)) continue;
                    if (SnakeSegments.HasComponent(launchedTarget))
                    {
                        var segment = SnakeSegments[launchedTarget];
                        if (segment.BouncedOutsideOwner == 0 && (candidate == segment.Head
                            || SnakeSegments.HasComponent(candidate) && SnakeSegments[candidate].Head == segment.Head
                                && SnakeSegments[candidate].Generation == segment.Generation)) continue;
                    }
                    if (SnakeHitRules.Protected(candidate, SnakeHeads, SnakeSegments)) continue;
                    bool wall = Barricades.HasComponent(candidate) && Barricades[candidate].HitsRemaining > 0;
                    if (IsUnavailable(candidate)
                        || !wall && (!LaunchStateLookup.HasComponent(candidate) || !HealthLookup.HasComponent(candidate))) continue;

                    EnemyLaunchState candidateLaunch = wall ? default : LaunchStateLookup[candidate];
                    if (!wall && ((candidateLaunch.Phase != EnemyLaunchPhase.Active
                         && candidateLaunch.Phase != EnemyLaunchPhase.Recovering)
                        || HealthLookup[candidate].Current <= 0f)) continue;

                    var obstruction = new LaunchObstructionCollector { Source = launchedTarget, Target = candidate, Enemies = EnemyLookup };
                    if (World.CollisionWorld.CastRay(new RaycastInput { Start = launchedPosition,
                        End = TransformLookup[candidate].Position, Filter = CollisionFilter.Default }, ref obstruction)) continue;
                    best = candidate;
                    bestDot = dot;
                    bestDistanceSq = distanceSq;
                }

                if (best == Entity.Null) return;
                EnemyLaunchState launchedState = LaunchStateLookup[launchedTarget];
                launchedState.HomingTarget = best;
                launchedState.HomingSecondsRemaining = HomingDuration;
                LaunchStateLookup[launchedTarget] = launchedState;
                float3 targetVelocity = VelocityLookup.HasComponent(best) ? VelocityLookup[best].Linear : float3.zero;
                velocity.Linear = LaunchAimPrediction.CorrectVelocity(velocity.Linear, launchedPosition, TransformLookup[best].Position,
                    targetVelocity, PropagationAimMaximumAngleDegrees, LeadSeconds, LeadMaximumDistance);
                VelocityLookup[launchedTarget] = velocity;
            }

            private void TryQueueDamage(Entity source, Entity target, CollisionEvent contact, float estimatedImpulse)
            {
                var launch = LaunchStateLookup[source];
                if (TierLookup.TryGetComponent(target, out var tier) && tier.Value == EnemyCombatTier.Elite)
                {
                    if (!EnemyCollisionDamage.CanDamage(tier.Value, launch.Owner)) return;
                    estimatedImpulse = math.max(estimatedImpulse, EstimateEliteImpact(source, target, launch, contact));
                }

                Damage.TryQueue(source, target, launch, estimatedImpulse);
            }

            private float EstimateEliteImpact(Entity source, Entity target, in EnemyLaunchState launch, CollisionEvent contact)
            {
                if (math.abs(contact.Normal.y) > .7f || !AuthoritativePaths.HasComponent(source)
                    || !AuthoritativePaths.IsComponentEnabled(source)) return 0f;
                var path = AuthoritativePaths[source];
                if (path.LaunchSequence != launch.LaunchSequence) return 0f;
                if (path.Contact == target && math.abs(path.ContactNormal.y) <= .7f) return path.ContactImpulse;
                if (!MassLookup.HasComponent(source) || !MassLookup.HasComponent(target)
                    || !FeedbackLookup.HasComponent(target)) return 0f;

                // The motor can slow to reach a vertex. Recover the incoming impulse for damage
                // on this real solver contact while leaving the elite's physical response alone.
                float3 incoming = path.CrowdSurface == target ? path.CrowdIncomingVelocity : path.IncomingVelocity;
                float3 towardTarget = source == contact.EntityA ? -contact.Normal : contact.Normal;
                float closingSpeed = math.dot(incoming - FeedbackLookup[target].IncomingVelocity, towardTarget);
                float inverseMass = MassLookup[source].InverseMass + MassLookup[target].InverseMass;
                return math.max(0f, closingSpeed) / math.max(.0001f, inverseMass);
            }
        }

        [BurstCompile]
        [WithAll(typeof(Enemy))]
        [WithNone(typeof(RespawnRequest))]
        private partial struct SweptContactJob : IJobEntity
        {
            public CollisionDamageQueue Damage;
            [ReadOnly] public ComponentLookup<Enemy> Enemies;
            [ReadOnly] public ComponentLookup<DasherSettings> Dashers;
            [ReadOnly] public ComponentLookup<EnemyLaunchState> Launches;
            [ReadOnly] public ComponentLookup<EnemyTier> Tiers;
            [ReadOnly] public ComponentLookup<RespawnRequest> Respawns;
            public ComponentLookup<EnemyImpactFeedback> Feedbacks;
            public double Now;

            private void Execute(Entity source, in EnemyLaunchState launch, in AuthoritativeLaunchPath path)
            {
                Entity target = path.Contact;
                if (target == Entity.Null || path.LaunchSequence != launch.LaunchSequence
                    || launch.Phase != EnemyLaunchPhase.Launched) return;

                if (!Enemies.HasComponent(target))
                {
                    float closingSpeed = -math.dot(path.IncomingVelocity, path.ContactNormal);
                    if (ImpactFeedbackRecording.IsObstacleContact(path.ContactNormal) && closingSpeed > 0f
                        && path.ContactImpulse > 0f && Feedbacks.TryGetComponent(source, out var worldFeedback))
                    {
                        // Share the solver's mailbox so a sweep and a collision event yield one accent.
                        ImpactFeedbackRecording.RecordWorldContact(ref worldFeedback, path.ContactPoint, path.ContactNormal,
                            closingSpeed, path.ContactImpulse, launch, Now);
                        Feedbacks[source] = worldFeedback;
                    }
                    return;
                }

                if (Dashers.HasComponent(source) || math.abs(path.ContactNormal.y) > .7f
                    || !Tiers.TryGetComponent(target, out var tier) || tier.Value != EnemyCombatTier.Elite
                    || !EnemyCollisionDamage.CanDamage(tier.Value, launch.Owner)
                    || !Launches.TryGetComponent(target, out var targetLaunch)
                    || targetLaunch.Phase != EnemyLaunchPhase.Active && targetLaunch.Phase != EnemyLaunchPhase.Recovering
                    || Respawns.HasComponent(target) && Respawns.IsComponentEnabled(target))
                    return;

                if (!Damage.TryQueue(source, target, launch, path.ContactImpulse) || !Feedbacks.HasComponent(target)) return;
                var feedback = Feedbacks[target];
                var significance = launch;
                significance.FeedbackChainDepth = math.min(64, significance.FeedbackChainDepth + 1);
                ImpactFeedbackRecording.Record(ref feedback, CombatImpactKind.EnemyCollision,
                    path.ContactPoint, -path.ContactNormal, -math.dot(path.IncomingVelocity, path.ContactNormal),
                    path.ContactImpulse, significance, Now);
                Feedbacks[target] = feedback;
            }
        }

        private struct CollisionDamageQueue
        {
            public ComponentLookup<DamageRequest> Requests;
            public BufferLookup<CollisionDamageHistory> History;
            [ReadOnly] public ComponentLookup<Health> Health;
            public EnemyLaunchSettings Settings;

            public bool TryQueue(Entity source, Entity target, in EnemyLaunchState launch, float impulse)
            {
                float damage = EnemyCollisionDamage.Calculate(launch.LaunchDamage, impulse, Settings);
                if (damage <= 0f || !Health.HasComponent(target) || !Requests.HasComponent(target)
                    || !History.HasBuffer(target)) return false;

                var history = History[target];
                foreach (var entry in history)
                    if (entry.Source == source && entry.SourceLaunchSequence == launch.LaunchSequence) return false;
                history.Add(new CollisionDamageHistory { Source = source, SourceLaunchSequence = launch.LaunchSequence });

                var request = Requests.IsComponentEnabled(target) ? Requests[target] : default;
                request.Add(damage, Health[target].Current, launch.OriginatingPunchSequence);
                Requests[target] = request;
                Requests.SetComponentEnabled(target, true);
                return true;
            }
        }
    }
}
