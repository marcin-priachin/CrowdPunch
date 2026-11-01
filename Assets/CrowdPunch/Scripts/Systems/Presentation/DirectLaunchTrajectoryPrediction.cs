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
    internal struct LaunchTrajectoryImpact
    {
        public float3 Position, Direction;
    }

    // PLAYER-003 / INFO-005: estimate only the directly punched body's flight. No gameplay writes.
    internal static class DirectLaunchTrajectoryPrediction
    {
        public static void Trace(EntityManager em, CollisionWorld world, LaunchPreviewSource source,
            NativeArray<LaunchPreviewSource> punched, in EnemyLaunchSettings settings,
            float maximumSeconds, int steps, NativeList<float3> points, float3 gravity = default,
            EnemyDefeatBounds? defeatBounds = null, NativeList<LaunchTrajectoryImpact> impacts = default)
        {
            points.Clear();
            if (impacts.IsCreated) impacts.Clear();
            if (!em.HasComponent<PhysicsCollider>(source.Entity) || !em.HasComponent<PhysicsMass>(source.Entity)) return;
            var collider = em.GetComponentData<PhysicsCollider>(source.Entity).Value;
            if (!collider.IsCreated || source.Speed <= 0f) return;
            var pose = em.GetComponentData<LocalTransform>(source.Entity);
            float3 velocity = math.normalizesafe(source.Direction) * source.Speed;
            bool grounded = em.HasComponent<EnemyGroundConstraint>(source.Entity)
                && em.GetComponentData<EnemyGroundConstraint>(source.Entity).HasGround != 0;
            if (grounded)
            {
                pose.Position.y = em.GetComponentData<EnemyGroundConstraint>(source.Entity).Height;
                velocity.y = 0f;
            }
            bool dasher = em.HasComponent<DasherSettings>(source.Entity);
            float damping = em.HasComponent<PhysicsDamping>(source.Entity)
                ? math.max(0f, em.GetComponentData<PhysicsDamping>(source.Entity).Linear) : 0f;
            float inverseMass = em.GetComponentData<PhysicsMass>(source.Entity).InverseMass;
            if (inverseMass <= 0f) return;
            gravity *= em.HasComponent<PhysicsGravityFactor>(source.Entity)
                ? em.GetComponentData<PhysicsGravityFactor>(source.Entity).Value : 1f;
            Entity homing = em.HasComponent<PunchAimAssistTarget>(source.Entity)
                ? em.GetComponentData<PunchAimAssistTarget>(source.Entity).Target : Entity.Null;
            float remainingHoming = settings.LaunchHomingDuration;
            float dt = math.clamp(maximumSeconds, .5f, 20f) / math.clamp(steps, 16, 400);
            float elapsed = 0f, lowMomentum = 0f;
            points.Add(pose.Position);
            using var hitStorage = new NativeList<ColliderCastHit>(Allocator.Temp);
            using var nearbyStorage = new NativeList<int>(Allocator.Temp);
            using var rayStorage = new NativeList<RaycastHit>(Allocator.Temp);
            // A struck crowd body departs with transferred momentum. Do not repeatedly resolve it
            // at its old snapshot position; forecasting its subsequent collision chain is out of scope.
            using var departed = new NativeHashSet<Entity>(16, Allocator.Temp);
            using var markedTargets = new NativeHashSet<Entity>(16, Allocator.Temp);
            var hits = hitStorage; var nearby = nearbyStorage; var rays = rayStorage;
            for (int step = 0; step < math.clamp(steps, 16, 400); step++)
            {
                if (homing != Entity.Null && remainingHoming > 0f && settings.LaunchHomingDegreesPerSecond > 0f)
                {
                    if (!LaunchImpactPrediction.GuidanceTarget(em, source.Entity, homing)) homing = Entity.Null;
                    else
                    {
                        float3 target = LaunchImpactPrediction.PositionAt(em, homing,
                            em.GetComponentData<LocalTransform>(homing).Position, elapsed, punched, source.Speed, settings);
                        if (!LaunchAimPrediction.WithinAngle(velocity, target - pose.Position, settings.LaunchHomingMaximumAngleDegrees)
                            || LaunchImpactPrediction.Obstructed(em, world, source.Entity, homing, pose.Position, target, ref rays))
                            homing = Entity.Null;
                        else
                        {
                            float3 lead = LaunchAimPrediction.Point(pose.Position, target,
                                LaunchImpactPrediction.Velocity(em, homing, punched, source.Speed), math.length(velocity.xz),
                                settings.AimLeadSeconds, settings.AimLeadMaximumDistance);
                            velocity = EnemyLaunchHoming.RotateHorizontalVelocity(velocity, lead - pose.Position,
                                math.radians(settings.LaunchHomingDegreesPerSecond) * math.min(dt, remainingHoming));
                        }
                    }
                    remainingHoming = math.max(0f, remainingHoming - dt);
                }

                float remaining = dt;
                if (!grounded) velocity += gravity * dt;
                // Resolve a bounded number of contacts inside a step so a bounce doesn't skip a nearby wall.
                for (int contact = 0; contact < 4 && remaining > .00001f; contact++)
                {
                    LaunchImpactPrediction.CastStep(em, world, source, punched, collider, pose, velocity,
                        elapsed + dt - remaining, remaining, source.Speed, settings, ref hits, ref nearby);
                    bool found = false;
                    ColliderCastHit closest = default;
                    float3 hitVelocity = default;
                    foreach (var hit in hits)
                    {
                        if (hit.Entity == source.Entity || departed.Contains(hit.Entity)
                            || hit.Material.CollisionResponse == CollisionResponsePolicy.RaiseTriggerEvents
                            || hit.Material.CollisionResponse == CollisionResponsePolicy.None
                            || SnakeProjectileCollision.IgnoresOwner(em, source.Entity, hit.Entity)
                            || em.HasComponent<Barricade>(hit.Entity) && em.GetComponentData<Barricade>(hit.Entity).HitsRemaining <= 0)
                            continue;
                        float3 targetVelocity = LaunchImpactPrediction.Velocity(em, hit.Entity, punched, source.Speed);
                        if (math.dot(velocity - targetVelocity, hit.SurfaceNormal) >= -.001f) continue;
                        if (found && (hit.Fraction > closest.Fraction
                            || hit.Fraction == closest.Fraction && hit.Entity.Index >= closest.Entity.Index)) continue;
                        closest = hit; hitVelocity = targetVelocity; found = true;
                    }
                    if (!found)
                    {
                        float3 end = pose.Position + velocity * remaining;
                        if (ClipAtDefeatBounds(pose.Position, ref end, defeatBounds))
                        { points.Add(end); return; }
                        pose.Position = end;
                        remaining = 0f;
                        break;
                    }
                    float3 impact = pose.Position + velocity * (remaining * closest.Fraction);
                    if (ClipAtDefeatBounds(pose.Position, ref impact, defeatBounds))
                    { points.Add(impact); return; }
                    pose.Position = impact;
                    points.Add(pose.Position);
                    if (impacts.IsCreated && IsImpactTarget(em, closest.Entity) && markedTargets.Add(closest.Entity))
                        impacts.Add(new LaunchTrajectoryImpact { Position = impact, Direction = math.normalizesafe(velocity) });
                    remaining *= 1f - closest.Fraction;
                    homing = Entity.Null;
                    bool enemy = em.HasComponent<Enemy>(closest.Entity);
                    // Dasher's launched motor passes through ordinary crowds; geometry still deflects it.
                    bool passThrough = dasher && enemy && em.HasComponent<EnemyTier>(closest.Entity)
                        && EnemyLaunchTransition.IsLaunchable(em.GetComponentData<EnemyTier>(closest.Entity));
                    if (!passThrough)
                    {
                        float otherInverseMass = em.HasComponent<PhysicsMass>(closest.Entity)
                            ? em.GetComponentData<PhysicsMass>(closest.Entity).InverseMass : 0f;
                        var material = SourceMaterial(collider);
                        float restitution = Unity.Physics.Material.GetCombinedRestitution(material, closest.Material);
                        float impulse = -(1f + restitution) * math.dot(velocity - hitVelocity, closest.SurfaceNormal)
                            / math.max(.0001f, inverseMass + otherInverseMass);
                        velocity += closest.SurfaceNormal * (impulse * inverseMass);
                        float3 relative = velocity - hitVelocity;
                        float3 tangent = relative - closest.SurfaceNormal * math.dot(relative, closest.SurfaceNormal);
                        float friction = Unity.Physics.Material.GetCombinedFriction(material, closest.Material);
                        float frictionImpulse = math.min(friction * impulse,
                            math.length(tangent) / math.max(.0001f, inverseMass + otherInverseMass));
                        velocity -= math.normalizesafe(tangent) * (frictionImpulse * inverseMass);
                        if (grounded) velocity.y = 0f;
                    }
                    if (enemy) departed.Add(closest.Entity);
                    // Small separation keeps a zero-fraction surface from consuming every later cast.
                    pose.Position += closest.SurfaceNormal * .002f;
                }
                if (grounded) pose.Position.y = em.GetComponentData<EnemyGroundConstraint>(source.Entity).Height;
                if (math.distancesq(points[points.Length - 1], pose.Position) > .000001f) points.Add(pose.Position);
                velocity *= math.clamp(1f - damping * dt, 0f, 1f);
                elapsed += dt;
                lowMomentum = math.length(velocity.xz) < math.max(0f, settings.UsefulMomentumSpeed)
                    ? lowMomentum + dt : 0f;
                if ((lowMomentum >= math.max(0f, settings.LowMomentumPeriod)
                    && math.length(velocity.xz) < math.max(0f, settings.UsefulMomentumSpeed))
                    || math.lengthsq(velocity) < .000001f) break;
            }
        }

        private static bool IsImpactTarget(EntityManager em, Entity target)
        {
            return em.HasComponent<Enemy>(target) || em.HasComponent<Barricade>(target)
                || em.HasComponent<TrackObject>(target) || em.HasComponent<BossEncounter>(target)
                || em.HasComponent<ChickenBoss>(target) || em.HasComponent<RollingBoss>(target)
                || em.HasComponent<DinoBoss>(target) || em.HasComponent<BossPart>(target)
                || em.HasComponent<FallingPillar>(target)
                    && em.GetComponentData<FallingPillar>(target).Phase == PillarPhase.Upright;
        }

        private static bool ClipAtDefeatBounds(float3 start, ref float3 end, EnemyDefeatBounds? bounds)
        {
            if (!bounds.HasValue) return false;
            float3 minimum = bounds.Value.Center - math.max(float3.zero, bounds.Value.Extents);
            float3 maximum = bounds.Value.Center + math.max(float3.zero, bounds.Value.Extents);
            float3 travel = end - start;
            float fraction = 1f;
            for (int axis = 0; axis < 3; axis++)
            {
                if (end[axis] < minimum[axis] && travel[axis] < 0f)
                    fraction = math.min(fraction, (minimum[axis] - start[axis]) / travel[axis]);
                if (end[axis] > maximum[axis] && travel[axis] > 0f)
                    fraction = math.min(fraction, (maximum[axis] - start[axis]) / travel[axis]);
            }
            if (fraction >= 1f) return false;
            end = start + travel * math.saturate(fraction);
            return true;
        }

        private static Unity.Physics.Material SourceMaterial(BlobAssetReference<Collider> collider)
        {
            if (collider.Value.CollisionType == CollisionType.Convex)
                return Unity.Collections.LowLevel.Unsafe.UnsafeUtility.As<Collider, ConvexCollider>(ref collider.Value).Material;
            // Public composite getters use the first leaf. Per-leaf source contact material is not
            // available from ColliderCastHit; complex source colliders remain an approximation.
            var material = Unity.Physics.Material.Default;
            material.Restitution = collider.Value.GetRestitution();
            material.Friction = collider.Value.GetFriction();
            return material;
        }
    }
}
