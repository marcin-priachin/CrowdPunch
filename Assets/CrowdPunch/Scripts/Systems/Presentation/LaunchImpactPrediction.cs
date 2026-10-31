using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
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
    }

    // PLAYER-003/004, COMBAT-003: a local estimate, never an authoritative physics simulation.
    internal static class LaunchImpactPrediction
    {
        public static bool TryPredict(EntityManager em, CollisionWorld world,
            LaunchPreviewSource source, NativeArray<LaunchPreviewSource> punched,
            NativeArray<Entity> candidates, NativeArray<LocalTransform> candidateTransforms, float distance, float strength,
            in EnemyLaunchSettings settings, double now, NativeList<ColliderCastHit> hits,
            out Entity target, out float3 direction, NativeHashSet<Entity> alreadyShown = default)
        {
            target = Entity.Null;
            direction = default;
            if (distance <= 0f || strength <= 0f || em.HasComponent<DasherSettings>(source.Entity)
                || !em.HasComponent<PhysicsCollider>(source.Entity)
                || !em.HasComponent<PhysicsMass>(source.Entity)) return false;
            var collider = em.GetComponentData<PhysicsCollider>(source.Entity).Value;
            if (!collider.IsCreated) return false;
            var pose = em.GetComponentData<LocalTransform>(source.Entity);
            var input = new ColliderCastInput(collider, pose.Position,
                pose.Position + source.Direction * distance, pose.Rotation, pose.Scale);
            hits.Clear();
            world.CastCollider(input, ref hits);
            // Replace stationary crowd hits with relative-motion casts. Fixed geometry keeps its
            // authoritative world cast; nearby bodies use the same bounded lead as launch aiming.
            float duration = distance / math.max(.0001f, strength);
            float horizon = math.min(duration, math.max(0f, settings.AimLeadSeconds));
            if (horizon > 0f && settings.AimLeadMaximumDistance > 0f)
            {
                for (int i = hits.Length - 1; i >= 0; i--)
                    if (em.HasComponent<Enemy>(hits[i].Entity)) hits.RemoveAtSwapBack(i);
                var sourceBounds = collider.Value.CalculateAabb(new RigidTransform(pose.Rotation, pose.Position), pose.Scale);
                float3 travel = source.Direction * distance;
                using var nearbyOwner = new NativeList<int>(Allocator.Temp);
                var nearby = nearbyOwner;
                world.OverlapAabb(new OverlapAabbInput { Filter = collider.Value.GetCollisionFilter(),
                    Aabb = new Aabb { Min = math.min(sourceBounds.Min, sourceBounds.Min + travel) - settings.AimLeadMaximumDistance,
                        Max = math.max(sourceBounds.Max, sourceBounds.Max + travel) + settings.AimLeadMaximumDistance } }, ref nearby);
                foreach (int index in nearby)
                {
                    var body = world.Bodies[index];
                    Entity candidate = body.Entity;
                    if (candidate == source.Entity || !em.HasComponent<Enemy>(candidate)
                        || !em.HasComponent<LocalTransform>(candidate) || !body.Collider.IsCreated) continue;
                    var targetPose = em.GetComponentData<LocalTransform>(candidate);
                    body.WorldFromBody = new RigidTransform(targetPose.Rotation, targetPose.Position);
                    body.Scale = targetPose.Scale;
                    float3 movingVelocity = em.HasComponent<PhysicsVelocity>(candidate)
                        ? em.GetComponentData<PhysicsVelocity>(candidate).Linear : float3.zero;
                    movingVelocity.y = 0f;
                    float movingSeconds = math.min(horizon, settings.AimLeadMaximumDistance / math.max(.0001f, math.length(movingVelocity)));
                    float3 displacement = movingVelocity * movingSeconds;
                    AppendMotionHits(body, collider, pose, source.Direction * strength, duration,
                        0f, movingSeconds, float3.zero, displacement, ref hits);
                    AppendMotionHits(body, collider, pose, source.Direction * strength, duration,
                        movingSeconds, duration, displacement, displacement, ref hits);
                }
            }
            ColliderCastHit closest = default;
            float fraction = float.MaxValue;
            foreach (var hit in hits)
            {
                if (hit.Entity == source.Entity || hit.Fraction > fraction
                    || hit.Material.CollisionResponse == CollisionResponsePolicy.RaiseTriggerEvents
                    || hit.Material.CollisionResponse == CollisionResponsePolicy.None
                    || SnakeProjectileCollision.IgnoresOwner(em, source.Entity, hit.Entity)) continue;
                float3 hitVelocity = em.HasComponent<PhysicsVelocity>(hit.Entity) ? em.GetComponentData<PhysicsVelocity>(hit.Entity).Linear : float3.zero;
                if (math.dot(source.Direction * strength - hitVelocity, hit.SurfaceNormal) >= -.001f) continue;
                if (em.HasComponent<Barricade>(hit.Entity)
                    && em.GetComponentData<Barricade>(hit.Entity).HitsRemaining <= 0) continue;
                if (hit.Fraction == fraction && hit.Entity.Index >= closest.Entity.Index) continue;
                closest = hit;
                fraction = hit.Fraction;
            }
            if (fraction == float.MaxValue || alreadyShown.IsCreated && alreadyShown.Contains(closest.Entity)
                || !CanPropagate(em, closest.Entity, now)) return false;
            // Bodies punched together are already flying; their collision cannot start another launch.
            foreach (var other in punched)
                if (other.Entity == closest.Entity) return false;

            float sourceInverseMass = em.GetComponentData<PhysicsMass>(source.Entity).InverseMass;
            float targetInverseMass = em.GetComponentData<PhysicsMass>(closest.Entity).InverseMass;
            if (sourceInverseMass <= 0f || targetInverseMass <= 0f) return false;
            float3 normal = -closest.SurfaceNormal;
            float3 targetVelocity = em.GetComponentData<PhysicsVelocity>(closest.Entity).Linear;
            // Estimate frictionless momentum transfer onto the struck body. The real solver may
            // distribute impulse among several contacts; never modify its result from presentation.
            // ApplyImpulseSystem treats punch strength as a velocity change, independently of mass.
            float closingSpeed = math.dot(source.Direction * strength - targetVelocity, normal);
            float impulse = math.max(0f, closingSpeed) / (sourceInverseMass + targetInverseMass);
            if (impulse <= 0f || impulse < math.max(0f, settings.MinimumPropagationImpulse)) return false;
            float3 velocity = targetVelocity + normal * impulse * targetInverseMass;
            if (math.lengthsq(velocity) <= 0.000001f) return false;
            target = closest.Entity;
            velocity = CorrectDirection(em, world, source.Entity, target, velocity, punched, candidates, candidateTransforms,
                settings);
            direction = math.normalizesafe(velocity);
            return true;
        }

        private static void AppendMotionHits(RigidBody body, BlobAssetReference<Collider> collider, LocalTransform pose,
            float3 velocity, float duration, float startTime, float endTime, float3 startDelta, float3 endDelta,
            ref NativeList<ColliderCastHit> hits)
        {
            if (endTime - startTime <= .000001f) return;
            int first = hits.Length;
            var input = new ColliderCastInput(collider, pose.Position + velocity * startTime - startDelta,
                pose.Position + velocity * endTime - endDelta, pose.Rotation, pose.Scale);
            body.CastCollider(input, ref hits);
            for (int i = first; i < hits.Length; i++)
            {
                var hit = hits[i];
                hit.Position += math.lerp(startDelta, endDelta, hit.Fraction);
                hit.Fraction = (startTime + (endTime - startTime) * hit.Fraction) / duration;
                hits[i] = hit;
            }
        }

        private static bool CanPropagate(EntityManager em, Entity target, double now)
        {
            if (!em.HasComponent<Enemy>(target) || em.HasComponent<BossPart>(target)
                || !em.HasComponent<EnemyTier>(target) || !em.HasComponent<EnemyLaunchState>(target)
                || !em.HasComponent<Health>(target) || !em.HasComponent<PhysicsMass>(target)
                || !em.HasComponent<PhysicsVelocity>(target) || !em.HasComponent<LocalTransform>(target)
                || em.HasComponent<RespawnRequest>(target) && em.IsComponentEnabled<RespawnRequest>(target)
                || ArmorHitResolution.IsProtected(em, target)
                || !EnemyLaunchTransition.IsLaunchable(em.GetComponentData<EnemyTier>(target))) return false;
            if (em.HasComponent<EnemyArmor>(target)
                && em.GetComponentData<EnemyArmor>(target).ProtectedUntil > now) return false;
            if (em.HasComponent<DasherState>(target)
                && em.GetComponentData<DasherState>(target).Phase == DasherPhase.Dashing) return false;
            var phase = em.GetComponentData<EnemyLaunchState>(target).Phase;
            return em.GetComponentData<Health>(target).Current > 0f
                && (phase == EnemyLaunchPhase.Active || phase == EnemyLaunchPhase.Recovering);
        }

        private static float3 CorrectDirection(EntityManager em, CollisionWorld world, Entity source, Entity target,
            float3 velocity, NativeArray<LaunchPreviewSource> punched, NativeArray<Entity> candidates,
            NativeArray<LocalTransform> candidateTransforms, in EnemyLaunchSettings settings)
        {
            float radius = settings.PropagationAimCorrectionRadius;
            float speed = math.length(velocity.xz);
            if (radius <= 0f || settings.PropagationAimMaximumAngleDegrees <= 0f || speed <= 0.0001f) return velocity;
            float3 position = em.GetComponentData<LocalTransform>(target).Position;
            float2 forward = velocity.xz / speed;
            Entity best = Entity.Null;
            float bestDot = float.MinValue, bestDistanceSq = float.MaxValue;
            using var sightOwner = new NativeList<RaycastHit>(Allocator.Temp);
            var sightHits = sightOwner;
            for (int i = 0; i < candidates.Length; i++)
            {
                Entity candidate = candidates[i];
                float2 offset = candidateTransforms[i].Position.xz - position.xz;
                float distanceSq = math.lengthsq(offset);
                if (distanceSq <= 0.000001f || distanceSq > radius * radius) continue;
                float dot = math.dot(forward, offset * math.rsqrt(distanceSq));
                if (dot < math.cos(math.radians(settings.PropagationAimMaximumAngleDegrees))) continue;
                bool improves = dot > bestDot || dot == bestDot && distanceSq < bestDistanceSq
                    || dot == bestDot && distanceSq == bestDistanceSq
                    && (best == Entity.Null || candidate.Index < best.Index);
                if (!improves) continue;
                // Snapshot positions once per frame; only inspect ECS eligibility for candidates
                // inside the radius that could improve the current direction choice.
                bool directlyPunched = false;
                foreach (var body in punched) directlyPunched |= body.Entity == candidate;
                if (directlyPunched) continue;
                if (candidate == source || candidate == target
                    || SnakeProjectileCollision.IgnoresOwner(em, target, candidate)
                    || SnakeHitRules.Protected(em, candidate)
                    || em.HasComponent<RespawnRequest>(candidate) && em.IsComponentEnabled<RespawnRequest>(candidate)) continue;
                bool wall = em.HasComponent<Barricade>(candidate)
                    && em.GetComponentData<Barricade>(candidate).HitsRemaining > 0;
                if (!wall)
                {
                    if (!em.HasComponent<EnemyLaunchState>(candidate) || !em.HasComponent<Health>(candidate)) continue;
                    var phase = em.GetComponentData<EnemyLaunchState>(candidate).Phase;
                    if (em.GetComponentData<Health>(candidate).Current <= 0f
                        || phase != EnemyLaunchPhase.Active && phase != EnemyLaunchPhase.Recovering) continue;
                }
                sightHits.Clear();
                world.CastRay(new RaycastInput { Start = position, End = candidateTransforms[i].Position,
                    Filter = CollisionFilter.Default }, ref sightHits);
                bool blocked = false;
                foreach (var hit in sightHits)
                    blocked |= hit.Entity != target && hit.Entity != candidate && !em.HasComponent<Enemy>(hit.Entity)
                        && hit.Material.CollisionResponse != CollisionResponsePolicy.RaiseTriggerEvents
                        && hit.Material.CollisionResponse != CollisionResponsePolicy.None;
                if (blocked) continue;
                best = candidate;
                bestDot = dot;
                bestDistanceSq = distanceSq;
            }
            if (best != Entity.Null)
            {
                float3 targetVelocity = em.HasComponent<PhysicsVelocity>(best) ? em.GetComponentData<PhysicsVelocity>(best).Linear : float3.zero;
                float3 point = LaunchAimPrediction.Point(position, em.GetComponentData<LocalTransform>(best).Position,
                    targetVelocity, speed, settings.AimLeadSeconds, settings.AimLeadMaximumDistance);
                velocity = CrowdPunch.Systems.Physics.EnemyLaunchHoming.RotateHorizontalVelocity(velocity,
                    point - position, math.radians(settings.PropagationAimMaximumAngleDegrees));
            }
            return velocity;
        }
    }
}
