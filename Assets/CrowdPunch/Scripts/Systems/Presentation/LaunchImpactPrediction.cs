using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Physics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Presentation
{
    internal struct LaunchPreviewSource
    {
        public Entity Entity;
        public float3 Direction;
        public float Speed;
    }

    // PLAYER-003/004: bounded local forecast. It never writes gameplay state or simulates a chain.
    internal static class LaunchImpactPrediction
    {
        public static bool TryPredict(EntityManager em, CollisionWorld world,
            LaunchPreviewSource source, NativeArray<LaunchPreviewSource> punched,
            NativeArray<Entity> candidates, NativeArray<LocalTransform> poses, float distance, float strength,
            in EnemyLaunchSettings settings, double now, NativeList<ColliderCastHit> hits,
            out Entity target, out float3 direction, NativeHashSet<Entity> alreadyShown = default)
        {
            bool result = TryForecast(em, world, source, punched, candidates, poses, distance, strength,
                settings, now, hits, default, out var forecast);
            target = forecast.Victim; direction = forecast.Direction;
            return result && (!alreadyShown.IsCreated || !alreadyShown.Contains(target));
        }

        public static bool TryForecast(EntityManager em, CollisionWorld world,
            LaunchPreviewSource source, NativeArray<LaunchPreviewSource> punched,
            NativeArray<Entity> candidates, NativeArray<LocalTransform> poses, float distance, float strength,
            in EnemyLaunchSettings settings, double now, NativeList<ColliderCastHit> hits,
            in CollisionPreviewSettings tuning, out LaunchImpactForecast forecast)
        {
            forecast = default;
            if (distance <= 0f || strength <= 0f || em.HasComponent<DasherSettings>(source.Entity)
                || !em.HasComponent<PhysicsCollider>(source.Entity) || !em.HasComponent<PhysicsMass>(source.Entity)) return false;
            var collider = em.GetComponentData<PhysicsCollider>(source.Entity).Value;
            if (!collider.IsCreated) return false;
            var pose = em.GetComponentData<LocalTransform>(source.Entity);
            float duration = distance / strength;
            // Legacy callers without budget data retain their one-step forecast. PlayerPunch always supplies a budget.
            if (tuning.ForecastSteps > 0) duration = math.min(duration, math.clamp(tuning.ForecastSeconds, .02f, .5f));
            int steps = math.clamp(tuning.ForecastSteps, 1, 12);
            float dt = duration / steps;
            float3 velocity = math.normalizesafe(source.Direction) * strength;
            if (em.HasComponent<EnemyGroundConstraint>(source.Entity))
            {
                var ground = em.GetComponentData<EnemyGroundConstraint>(source.Entity);
                if (ground.HasGround != 0) { pose.Position.y = ground.Height; velocity.y = 0f; }
            }
            float damping = em.HasComponent<PhysicsDamping>(source.Entity) ? em.GetComponentData<PhysicsDamping>(source.Entity).Linear : 0f;
            Entity homing = em.HasComponent<PunchAimAssistTarget>(source.Entity)
                ? em.GetComponentData<PunchAimAssistTarget>(source.Entity).Target : Entity.Null;
            float remainingHoming = settings.LaunchHomingDuration;
            using var nearbyOwner = new NativeList<int>(Allocator.Temp);
            using var raysOwner = new NativeList<RaycastHit>(Allocator.Temp);
            var nearby = nearbyOwner; var rays = raysOwner;
            float elapsed = 0f;
            for (int step = 0; step < steps; step++)
            {
                if (homing != Entity.Null && remainingHoming > 0f && settings.LaunchHomingDegreesPerSecond > 0f)
                {
                    if (!GuidanceTarget(em, source.Entity, homing)) homing = Entity.Null;
                    else
                    {
                        float3 targetPoint = PositionAt(em, homing, em.GetComponentData<LocalTransform>(homing).Position,
                            elapsed, punched, strength, settings);
                        if (!LaunchAimPrediction.WithinAngle(velocity, targetPoint - pose.Position, settings.LaunchHomingMaximumAngleDegrees)
                            || Obstructed(em, world, source.Entity, homing, pose.Position, targetPoint, ref rays)) homing = Entity.Null;
                        else
                        {
                            float3 lead = LaunchAimPrediction.Point(pose.Position, targetPoint, Velocity(em, homing, punched, strength),
                                math.length(velocity.xz), settings.AimLeadSeconds, settings.AimLeadMaximumDistance);
                            velocity = EnemyLaunchHoming.RotateHorizontalVelocity(velocity, lead - pose.Position,
                                math.radians(settings.LaunchHomingDegreesPerSecond) * math.min(dt, remainingHoming));
                        }
                    }
                    remainingHoming = math.max(0f, remainingHoming - dt);
                }
                float3 end = pose.Position + velocity * dt;
                CastStep(em, world, source, punched, collider, pose, velocity, elapsed, dt, strength, settings,
                    ref hits, ref nearby);
                ColliderCastHit closest = default;
                float fraction = float.MaxValue;
                foreach (var hit in hits)
                {
                    if (hit.Entity == source.Entity || hit.Fraction > fraction
                        || hit.Material.CollisionResponse == CollisionResponsePolicy.RaiseTriggerEvents
                        || hit.Material.CollisionResponse == CollisionResponsePolicy.None
                        || SnakeProjectileCollision.IgnoresOwner(em, source.Entity, hit.Entity)) continue;
                    float3 targetVelocity = Velocity(em, hit.Entity, punched, strength);
                    if (math.dot(velocity - targetVelocity, hit.SurfaceNormal) >= -.001f) continue;
                    if (em.HasComponent<Barricade>(hit.Entity) && em.GetComponentData<Barricade>(hit.Entity).HitsRemaining <= 0) continue;
                    if (hit.Fraction == fraction && hit.Entity.Index >= closest.Entity.Index) continue;
                    closest = hit; fraction = hit.Fraction;
                }
                if (fraction != float.MaxValue)
                {
                    float arrival = elapsed + dt * fraction;
                    if (Punched(closest.Entity, punched) || !CanPropagate(em, closest.Entity, now + arrival)) return false;
                    float sourceMass = em.GetComponentData<PhysicsMass>(source.Entity).InverseMass;
                    float targetMass = em.GetComponentData<PhysicsMass>(closest.Entity).InverseMass;
                    if (sourceMass <= 0f || targetMass <= 0f) return false;
                    float3 targetVelocity = Velocity(em, closest.Entity, punched, strength);
                    float3 relative = velocity - targetVelocity;
                    float closing = math.max(0f, math.dot(relative, -closest.SurfaceNormal));
                    float impulse = closing / (sourceMass + targetMass);
                    if (impulse <= 0f || impulse < math.max(0f, settings.MinimumPropagationImpulse)) return false;
                    float3 victimPosition = PositionAt(em, closest.Entity, em.GetComponentData<LocalTransform>(closest.Entity).Position,
                        arrival, punched, strength, settings);
                    float3 outgoing = targetVelocity - closest.SurfaceNormal * impulse * targetMass;
                    if (em.HasComponent<EnemyGroundConstraint>(closest.Entity))
                    {
                        var ground = em.GetComponentData<EnemyGroundConstraint>(closest.Entity);
                        if (ground.HasGround != 0 && ground.IsLocked != 0) outgoing.y = 0f;
                    }
                    outgoing = CorrectDirection(em, world, source.Entity, closest.Entity, victimPosition, outgoing,
                        arrival, punched, candidates, poses, strength, settings, ref rays, out Entity followup);
                    if (math.lengthsq(outgoing) < .000001f) return false;
                    float confidence = math.saturate(closing / math.max(.001f, math.length(relative)))
                        * math.saturate(impulse / math.max(.001f, settings.MinimumPropagationImpulse * 2f));
                    if (math.lengthsq(targetVelocity.xz) > .001f && arrival > settings.AimLeadSeconds) confidence *= .5f;
                    forecast = new LaunchImpactForecast { Source = source.Entity, Victim = closest.Entity, Followup = followup,
                        Direction = math.normalizesafe(outgoing), VictimPosition = victimPosition, ArrivalSeconds = arrival,
                        Impulse = impulse, Confidence = confidence, SourceLifetime = Lifetime(em, source.Entity),
                        VictimLifetime = Lifetime(em, closest.Entity), SourceLaunchSequence = em.HasComponent<EnemyLaunchState>(source.Entity)
                            ? em.GetComponentData<EnemyLaunchState>(source.Entity).LaunchSequence : 0,
                        FollowupLifetime = followup == Entity.Null ? 0 : Lifetime(em, followup) };
                    return true;
                }
                pose.Position = end;
                velocity *= math.clamp(1f - math.max(0f, damping) * dt, 0f, 1f);
                elapsed += dt;
            }
            return false;
        }

        internal static void CastStep(EntityManager em, CollisionWorld world, LaunchPreviewSource source,
            NativeArray<LaunchPreviewSource> punched, BlobAssetReference<Collider> collider, LocalTransform pose,
            float3 velocity, float elapsed, float dt, float strength, in EnemyLaunchSettings settings,
            ref NativeList<ColliderCastHit> hits, ref NativeList<int> nearby)
        {
            float3 end = pose.Position + velocity * dt;
            hits.Clear();
            world.CastCollider(new ColliderCastInput(collider, pose.Position, end, pose.Rotation, pose.Scale), ref hits);
            // Fixed geometry keeps the world cast. Crowd colliders move relative to this short flight step.
            for (int i = hits.Length - 1; i >= 0; i--)
                if (em.HasComponent<Enemy>(hits[i].Entity)) hits.RemoveAtSwapBack(i);
            var bounds = collider.Value.CalculateAabb(new RigidTransform(pose.Rotation, pose.Position), pose.Scale);
            float3 travel = end - pose.Position;
            nearby.Clear();
            world.OverlapAabb(new OverlapAabbInput { Filter = collider.Value.GetCollisionFilter(), Aabb = new Aabb {
                Min = math.min(bounds.Min, bounds.Min + travel) - math.max(0f, settings.AimLeadMaximumDistance),
                Max = math.max(bounds.Max, bounds.Max + travel) + math.max(0f, settings.AimLeadMaximumDistance) } }, ref nearby);
            foreach (int index in nearby)
            {
                var body = world.Bodies[index];
                Entity candidate = body.Entity;
                if (candidate == source.Entity || !em.HasComponent<Enemy>(candidate)
                    || !em.HasComponent<LocalTransform>(candidate) || !body.Collider.IsCreated || Punched(candidate, punched)) continue;
                var targetPose = em.GetComponentData<LocalTransform>(candidate);
                body.WorldFromBody = new RigidTransform(targetPose.Rotation, targetPose.Position); body.Scale = targetPose.Scale;
                CastMovingBody(em, body, collider, pose, velocity, elapsed, dt, punched, strength, settings, ref hits);
            }
            // Co-punched bodies depart at replacement velocity, rather than remaining at their initial positions.
            foreach (var other in punched)
            {
                if (other.Entity == source.Entity || !em.HasComponent<PhysicsCollider>(other.Entity)) continue;
                var targetCollider = em.GetComponentData<PhysicsCollider>(other.Entity).Value;
                if (!targetCollider.IsCreated) continue;
                var targetPose = em.GetComponentData<LocalTransform>(other.Entity);
                var body = new RigidBody { Entity = other.Entity, Collider = targetCollider, Scale = targetPose.Scale,
                    WorldFromBody = new RigidTransform(targetPose.Rotation, targetPose.Position) };
                CastMovingBody(em, body, collider, pose, velocity, elapsed, dt, punched, strength, settings, ref hits);
            }
        }

        private static void CastMovingBody(EntityManager em, RigidBody body, BlobAssetReference<Collider> collider,
            LocalTransform pose, float3 velocity, float elapsed, float dt, NativeArray<LaunchPreviewSource> punched,
            float strength, in EnemyLaunchSettings settings, ref NativeList<ColliderCastHit> hits)
        {
            float3 targetVelocity = Velocity(em, body.Entity, punched, strength);
            float stop = Punched(body.Entity, punched) ? float.MaxValue
                : math.min(math.max(0f, settings.AimLeadSeconds), math.max(0f, settings.AimLeadMaximumDistance)
                    / math.max(.0001f, math.length(targetVelocity.xz)));
            float split = math.clamp(stop - elapsed, 0f, dt);
            AppendMotionHits(body, collider, pose, velocity, dt, 0f, split,
                targetVelocity * math.min(elapsed, stop), targetVelocity * math.min(elapsed + split, stop), ref hits);
            AppendMotionHits(body, collider, pose, velocity, dt, split, dt,
                targetVelocity * math.min(elapsed + split, stop), targetVelocity * math.min(elapsed + dt, stop), ref hits);
        }

        private static void AppendMotionHits(RigidBody body, BlobAssetReference<Collider> collider, LocalTransform pose,
            float3 velocity, float duration, float startTime, float endTime, float3 startDelta, float3 endDelta,
            ref NativeList<ColliderCastHit> hits)
        {
            if (endTime - startTime <= .000001f) return;
            int first = hits.Length;
            body.CastCollider(new ColliderCastInput(collider, pose.Position + velocity * startTime - startDelta,
                pose.Position + velocity * endTime - endDelta, pose.Rotation, pose.Scale), ref hits);
            for (int i = first; i < hits.Length; i++)
            {
                var hit = hits[i]; hit.Position += math.lerp(startDelta, endDelta, hit.Fraction);
                hit.Fraction = (startTime + (endTime - startTime) * hit.Fraction) / duration; hits[i] = hit;
            }
        }

        internal static float3 Velocity(EntityManager em, Entity target, NativeArray<LaunchPreviewSource> punched, float strength)
        {
            foreach (var source in punched) if (source.Entity == target)
                return source.Direction * (source.Speed > 0f ? source.Speed : strength);
            var velocity = em.HasComponent<PhysicsVelocity>(target) ? em.GetComponentData<PhysicsVelocity>(target).Linear : float3.zero;
            return new float3(velocity.x, 0f, velocity.z);
        }

        private static bool Punched(Entity target, NativeArray<LaunchPreviewSource> punched)
        { foreach (var source in punched) if (source.Entity == target) return true; return false; }

        internal static float3 PositionAt(EntityManager em, Entity entity, float3 position, float time,
            NativeArray<LaunchPreviewSource> punched, float strength, in EnemyLaunchSettings settings)
        {
            float3 velocity = Velocity(em, entity, punched, strength);
            return position + (Punched(entity, punched) ? velocity * time
                : LaunchAimPrediction.Displacement(velocity, math.min(time, math.max(0f, settings.AimLeadSeconds)), settings.AimLeadMaximumDistance));
        }

        private static uint Lifetime(EntityManager em, Entity entity) => em.HasComponent<EnemyLifetime>(entity)
            ? em.GetComponentData<EnemyLifetime>(entity).Generation : 0;

        internal static bool GuidanceTarget(EntityManager em, Entity source, Entity target)
        {
            if (!PunchAimAssist.IsValidTarget(em, source, target)) return false;
            if (!em.HasComponent<EnemyLaunchState>(target)) return true;
            var phase = em.GetComponentData<EnemyLaunchState>(target).Phase;
            return phase == EnemyLaunchPhase.Active || phase == EnemyLaunchPhase.Recovering;
        }

        private static bool CanPropagate(EntityManager em, Entity target, double now)
        {
            if (!em.HasComponent<Enemy>(target) || em.HasComponent<BossPart>(target) || !em.HasComponent<EnemyTier>(target)
                || !em.HasComponent<EnemyLaunchState>(target) || !em.HasComponent<Health>(target) || !em.HasComponent<PhysicsMass>(target)
                || !em.HasComponent<PhysicsVelocity>(target) || !em.HasComponent<LocalTransform>(target)
                || em.HasComponent<RespawnRequest>(target) && em.IsComponentEnabled<RespawnRequest>(target)
                || ArmorHitResolution.IsProtected(em, target) || !EnemyLaunchTransition.IsLaunchable(em.GetComponentData<EnemyTier>(target))) return false;
            if (em.HasComponent<EnemyArmor>(target) && em.GetComponentData<EnemyArmor>(target).ProtectedUntil > now) return false;
            if (em.HasComponent<DasherState>(target) && em.GetComponentData<DasherState>(target).Phase == DasherPhase.Dashing) return false;
            var phase = em.GetComponentData<EnemyLaunchState>(target).Phase;
            return em.GetComponentData<Health>(target).Current > 0f && (phase == EnemyLaunchPhase.Active || phase == EnemyLaunchPhase.Recovering);
        }

        private static float3 CorrectDirection(EntityManager em, CollisionWorld world, Entity source, Entity victim,
            float3 position, float3 velocity, float arrival, NativeArray<LaunchPreviewSource> punched,
            NativeArray<Entity> candidates, NativeArray<LocalTransform> poses, float strength, in EnemyLaunchSettings settings,
            ref NativeList<RaycastHit> rays, out Entity best)
        {
            best = Entity.Null;
            if (settings.PropagationAimCorrectionRadius <= 0f || settings.PropagationAimMaximumAngleDegrees <= 0f) return velocity;
            float bestDot = float.MinValue, bestDistanceSq = float.MaxValue;
            float3 bestPosition = default;
            float expandedRadius = settings.PropagationAimCorrectionRadius + settings.AimLeadMaximumDistance * 2f;
            for (int i = 0; i < candidates.Length; i++)
            {
                Entity candidate = candidates[i];
                if (candidate == source || candidate == victim || Punched(candidate, punched)
                    || math.distancesq(poses[i].Position.xz, position.xz) > expandedRadius * expandedRadius) continue;
                float3 point = PositionAt(em, candidate, poses[i].Position, arrival, punched, strength, settings);
                if (!LaunchAimPrediction.BetterCorrection(velocity, point - position, settings.PropagationAimCorrectionRadius,
                        settings.PropagationAimMaximumAngleDegrees, candidate.Index, best == Entity.Null ? int.MaxValue : best.Index,
                        bestDot, bestDistanceSq, out float dot, out float distanceSq)) continue;
                if (SnakeProjectileCollision.IgnoresOwner(em, victim, candidate) || SnakeHitRules.Protected(em, candidate)
                    || em.HasComponent<RespawnRequest>(candidate) && em.IsComponentEnabled<RespawnRequest>(candidate)) continue;
                bool wall = em.HasComponent<Barricade>(candidate) && em.GetComponentData<Barricade>(candidate).HitsRemaining > 0;
                if (!wall)
                {
                    if (!em.HasComponent<EnemyLaunchState>(candidate) || !em.HasComponent<Health>(candidate)) continue;
                    var phase = em.GetComponentData<EnemyLaunchState>(candidate).Phase;
                    if (em.GetComponentData<Health>(candidate).Current <= 0f || phase != EnemyLaunchPhase.Active && phase != EnemyLaunchPhase.Recovering) continue;
                }
                if (Obstructed(em, world, victim, candidate, position, point, ref rays)) continue;
                best = candidate; bestDot = dot; bestDistanceSq = distanceSq; bestPosition = point;
            }
            return best == Entity.Null ? velocity : LaunchAimPrediction.CorrectVelocity(velocity, position, bestPosition,
                Velocity(em, best, punched, strength), settings.PropagationAimMaximumAngleDegrees, settings.AimLeadSeconds, settings.AimLeadMaximumDistance);
        }

        internal static bool Obstructed(EntityManager em, CollisionWorld world, Entity source, Entity target,
            float3 start, float3 end, ref NativeList<RaycastHit> rays)
        {
            rays.Clear();
            world.CastRay(new RaycastInput { Start = start, End = end, Filter = CollisionFilter.Default }, ref rays);
            foreach (var hit in rays)
                if (hit.Entity != source && hit.Entity != target && !em.HasComponent<Enemy>(hit.Entity)
                    && hit.Material.CollisionResponse != CollisionResponsePolicy.RaiseTriggerEvents
                    && hit.Material.CollisionResponse != CollisionResponsePolicy.None) return true;
            return false;
        }
    }
}
