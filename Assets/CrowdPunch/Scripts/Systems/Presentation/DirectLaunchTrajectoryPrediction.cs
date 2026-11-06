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
    internal struct LaunchTrajectoryContact
    {
        public int EndPointIndex;
        public float3 Position;
    }

    internal struct LaunchTrajectoryImpact
    {
        public float3 Position, Direction;
    }

    // Optional ECS-only comparison data. Entity identities are never copied to the player bridge.
    internal struct LaunchTrajectorySample
    {
        public float Seconds;
        public float3 Position, Velocity;
        public Entity Contact;
        public float3 ContactNormal;
        public Entity GuidanceTarget;
        public float GuidanceSeconds;
    }

    // PLAYER-003 / INFO-005: estimate only the directly punched body's flight. No gameplay writes.
    internal static class DirectLaunchTrajectoryPrediction
    {
        public const float DefaultTimestep = 1f / 60f;

        public static void Trace(EntityManager em, CollisionWorld world, LaunchPreviewSource source,
            NativeArray<LaunchPreviewSource> punched, in EnemyLaunchSettings settings,
            float maximumSeconds, int steps, NativeList<float3> points, float3 gravity = default,
            EnemyDefeatBounds? defeatBounds = null, NativeList<LaunchTrajectoryImpact> impacts = default,
            PlayerSnapshot player = default, float fixedTimestep = DefaultTimestep,
            NativeList<LaunchTrajectorySample> samples = default, NativeList<LaunchReboundPlan> rebounds = default,
            NativeList<LaunchPathPoint> path = default, NativeList<LaunchTrajectoryContact> firstContact = default)
        {
            points.Clear();
            if (firstContact.IsCreated) firstContact.Clear();
            if (impacts.IsCreated) impacts.Clear();
            if (samples.IsCreated) samples.Clear();
            if (rebounds.IsCreated) rebounds.Clear();
            if (path.IsCreated) path.Clear();
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
            Entity homing = source.HasAimPoint != 0 ? source.AimTarget : em.HasComponent<PunchAimAssistTarget>(source.Entity)
                ? em.GetComponentData<PunchAimAssistTarget>(source.Entity).Target : Entity.Null;
            float3 frozenPoint = source.AimPoint;
            if (source.HasAimPoint == 0 && homing != Entity.Null && em.HasComponent<LocalTransform>(homing))
                frozenPoint = LaunchAimPrediction.Point(pose.Position, em.GetComponentData<LocalTransform>(homing).Position,
                    LaunchImpactPrediction.Velocity(em, homing, punched, source.Speed), source.Speed,
                    settings.AimLeadSeconds, settings.AimLeadMaximumDistance);
            bool canCommitRebounds = true;
            float remainingHoming = homing != Entity.Null ? settings.LaunchHomingDuration : 0f;
            float dt = math.isfinite(fixedTimestep) && fixedTimestep > 0f ? fixedTimestep : DefaultTimestep;
            float duration = math.clamp(maximumSeconds, .5f, 20f);
            float elapsed = 0f, lowMomentum = 0f;
            points.Add(pose.Position);
            PathPoint(path, 0f, pose.Position, velocity);
            if (samples.IsCreated) samples.Add(new LaunchTrajectorySample { Position = pose.Position, Velocity = velocity,
                GuidanceTarget = homing, GuidanceSeconds = remainingHoming });
            using var hitStorage = new NativeList<ColliderCastHit>(Allocator.Temp);
            using var nearbyStorage = new NativeList<int>(Allocator.Temp);
            using var rayStorage = new NativeList<RaycastHit>(Allocator.Temp);
            // A struck crowd body departs with transferred momentum. Do not repeatedly resolve it
            // at its old snapshot position; forecasting its subsequent collision chain is out of scope.
            using var departed = new NativeHashSet<Entity>(16, Allocator.Temp);
            using var markedTargets = new NativeHashSet<Entity>(16, Allocator.Temp);
            using var assistExcluded = new NativeHashSet<Entity>(16, Allocator.Temp);
            using var countedWalls = new NativeHashSet<Entity>(16, Allocator.Temp);
            foreach (var body in punched) assistExcluded.Add(body.Entity);
            var hits = hitStorage; var nearby = nearbyStorage; var rays = rayStorage;
            for (int step = 0; step < math.clamp(steps, 16, 400); step++)
            {
                // A partial final step would have a different damping/guidance/contact order.
                if (elapsed + dt > duration + .00001f) break;
                Entity pendingSurface = Entity.Null, contactEntity = Entity.Null;
                float3 pendingNormal = default, pendingIncoming = default, contactCenter = default, firstContactNormal = default;
                bool authoredRebound = false;
                if (homing != Entity.Null && (remainingHoming <= 0f || settings.LaunchHomingDegreesPerSecond <= 0f))
                { homing = Entity.Null; remainingHoming = 0f; }
                if (homing != Entity.Null)
                {
                    if (departed.Contains(homing) || !LaunchImpactPrediction.GuidanceTarget(em, source.Entity, homing))
                    { homing = Entity.Null; remainingHoming = 0f; }
                    else
                    {
                        if (!LaunchAimPrediction.WithinAngle(velocity, frozenPoint - pose.Position, settings.LaunchHomingMaximumAngleDegrees)
                            || LaunchImpactPrediction.Obstructed(em, world, source.Entity, homing, pose.Position, frozenPoint, ref rays, departed))
                        { homing = Entity.Null; remainingHoming = 0f; }
                        else
                        {
                            velocity = EnemyLaunchHoming.RotateHorizontalVelocity(velocity, frozenPoint - pose.Position,
                                math.radians(settings.LaunchHomingDegreesPerSecond) * math.min(dt, remainingHoming));
                        }
                    }
                    remainingHoming = math.max(0f, remainingHoming - dt);
                    if (remainingHoming <= 0f) homing = Entity.Null;
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
                        { points.Add(end); PathPoint(path, elapsed + dt - remaining + math.distance(pose.Position, end)
                            / math.max(.0001f, math.length(velocity)), end, velocity); return; }
                        pose.Position = end;
                        remaining = 0f;
                        break;
                    }
                    float3 impact = pose.Position + velocity * (remaining * closest.Fraction);
                    if (ClipAtDefeatBounds(pose.Position, ref impact, defeatBounds))
                    { points.Add(impact); PathPoint(path, elapsed + dt - remaining + math.distance(pose.Position, impact)
                        / math.max(.0001f, math.length(velocity)), impact, velocity); return; }
                    pose.Position = impact;
                    points.Add(pose.Position);
                    if (firstContact.IsCreated && firstContact.Length == 0)
                        firstContact.Add(new LaunchTrajectoryContact { EndPointIndex = points.Length - 1, Position = impact });
                    PathPoint(path, elapsed + dt - remaining + remaining * closest.Fraction,
                        pose.Position, velocity, closest.Entity, closest.SurfaceNormal);
                    if (impacts.IsCreated && IsImpactTarget(em, source.Entity, closest.Entity) && markedTargets.Add(closest.Entity))
                        impacts.Add(new LaunchTrajectoryImpact { Position = impact, Direction = math.normalizesafe(velocity) });
                    remaining *= 1f - closest.Fraction;
                    bool enemy = em.HasComponent<Enemy>(closest.Entity);
                    float otherInverseMass = em.HasComponent<PhysicsMass>(closest.Entity)
                        ? em.GetComponentData<PhysicsMass>(closest.Entity).InverseMass : 0f;
                    var material = SourceMaterial(collider);
                    float restitution = Unity.Physics.Material.GetCombinedRestitution(material, closest.Material);
                    float impulse = -(1f + restitution) * math.dot(velocity - hitVelocity, closest.SurfaceNormal)
                        / math.max(.0001f, inverseMass + otherInverseMass);
                    if (contactEntity == Entity.Null) { contactEntity = closest.Entity; firstContactNormal = closest.SurfaceNormal; }
                    if (math.abs(closest.SurfaceNormal.y) <= .7f && PillarToppleSystem.CanTopple(em, closest.Entity)
                        || PassesThroughDestroyedWall(em, source, closest.Entity, velocity, closest.SurfaceNormal,
                            inverseMass, settings, countedWalls))
                    {
                        departed.Add(closest.Entity); assistExcluded.Add(closest.Entity);
                        continue;
                    }
                    bool forced = math.abs(closest.SurfaceNormal.y) <= .7f
                        && (em.HasComponent<RotatingCover>(closest.Entity) || em.HasComponent<Barricade>(closest.Entity));
                    bool meaningful = LaunchContactResponse.Meaningful(closest.SurfaceNormal, impulse, settings.MinimumPropagationImpulse);
                    if (enemy && meaningful) canCommitRebounds = false;
                    if (meaningful && contactEntity != closest.Entity) canCommitRebounds = false;
                    if (forced || meaningful)
                    {
                        homing = Entity.Null;
                        remainingHoming = 0f;
                        if (!enemy)
                        {
                            pendingSurface = closest.Entity; pendingNormal = closest.SurfaceNormal;
                            pendingIncoming = velocity; contactCenter = impact; authoredRebound = forced;
                        }
                    }
                    // Dasher's launched motor passes through ordinary crowds; geometry still deflects it.
                    bool passThrough = dasher && enemy && em.HasComponent<EnemyTier>(closest.Entity)
                        && EnemyLaunchTransition.IsLaunchable(em.GetComponentData<EnemyTier>(closest.Entity));
                    if (!passThrough)
                    {
                        velocity += closest.SurfaceNormal * (impulse * inverseMass);
                        float3 relative = velocity - hitVelocity;
                        float3 tangent = relative - closest.SurfaceNormal * math.dot(relative, closest.SurfaceNormal);
                        float friction = Unity.Physics.Material.GetCombinedFriction(material, closest.Material);
                        float frictionImpulse = math.min(friction * impulse,
                            math.length(tangent) / math.max(.0001f, inverseMass + otherInverseMass));
                        velocity -= math.normalizesafe(tangent) * (frictionImpulse * inverseMass);
                        if (grounded) velocity.y = 0f;
                    }
                    if (enemy) { departed.Add(closest.Entity); assistExcluded.Add(closest.Entity); }
                    // Small separation keeps a zero-fraction surface from consuming every later cast.
                    pose.Position += closest.SurfaceNormal * .002f;
                }
                if (grounded) pose.Position.y = em.GetComponentData<EnemyGroundConstraint>(source.Entity).Height;
                velocity *= math.clamp(1f - damping * dt, 0f, 1f);
                elapsed += dt;
                // Match post-physics overrides and reacquisition. No guidance acts during the
                // remainder of the contact step; its new budget begins on the next fixed step.
                if (pendingSurface != Entity.Null)
                {
                    if (authoredRebound)
                    {
                        pose.Position = LaunchContactResponse.OutsideSurface(pose.Position, contactCenter, pendingNormal);
                        if (em.HasComponent<RotatingCover>(pendingSurface))
                        {
                            velocity.xz = CoverReflectionSystem.ReflectedVelocity(new CoverReflection {
                                ContactCenter = contactCenter, Normal = pendingNormal, IncomingVelocity = pendingIncoming,
                                PlayerPosition = player.IsAvailable ? player.Position : contactCenter - pendingIncoming },
                                em.GetComponentData<RotatingCover>(pendingSurface).ReflectionMultiplier);
                        }
                        else
                        {
                            velocity = LaunchContactResponse.BarricadeVelocity(velocity, pendingIncoming, pendingNormal,
                                em.GetComponentData<Barricade>(pendingSurface).ReboundMultiplier);
                            lowMomentum = 0f;
                            if (em.HasComponent<FinaleBoss>(pendingSurface) && player.IsAvailable)
                                velocity = LaunchContactResponse.FinaleSafety(velocity, pose.Position, player.Position, pendingNormal,
                                    em.GetComponentData<FinaleTuning>(pendingSurface).ReboundSafetyDegrees);
                        }
                    }
                    if (canCommitRebounds && RicochetAimCorrection.TryCorrect(em, world, source.Entity, pendingSurface, pose.Position,
                        pendingNormal, ref velocity, settings, ref nearby, ref rays, out Entity reboundTarget,
                        out frozenPoint, elapsed, assistExcluded))
                    {
                        homing = reboundTarget; remainingHoming = settings.LaunchHomingDuration;
                    }
                }
                if (contactEntity != Entity.Null && player.IsAvailable
                    && (em.HasComponent<ChickenBoss>(contactEntity) || em.HasComponent<RollingBoss>(contactEntity)
                        || em.HasComponent<BossPart>(contactEntity) && em.GetComponentData<BossPart>(contactEntity).Kind == BossPartKind.Head))
                    velocity = BossHeadBounceSystem.Redirect(velocity, pose.Position,
                        em.GetComponentData<LocalTransform>(contactEntity).Position, player.Position, source.Entity.Index);
                if (rebounds.IsCreated && canCommitRebounds && pendingSurface != Entity.Null
                    && rebounds.Length < CommittedLaunchPlan.MaximumRebounds && math.lengthsq(velocity.xz) > .000001f)
                    rebounds.Add(new LaunchReboundPlan { Surface = pendingSurface, Normal = pendingNormal,
                        Direction = math.normalizesafe(velocity), Target = homing, AimPoint = frozenPoint,
                        HasAimPoint = homing != Entity.Null ? (byte)1 : (byte)0,
                        TargetGeneration = CommittedLaunchPlan.Generation(em, homing) });
                if (math.distancesq(points[points.Length - 1], pose.Position) > .000001f) points.Add(pose.Position);
                PathPoint(path, elapsed, pose.Position, velocity);
                if (samples.IsCreated) samples.Add(new LaunchTrajectorySample { Seconds = elapsed, Position = pose.Position,
                    Velocity = velocity, Contact = contactEntity, ContactNormal = firstContactNormal,
                    GuidanceTarget = homing, GuidanceSeconds = remainingHoming });
                lowMomentum = math.length(velocity.xz) < math.max(0f, settings.UsefulMomentumSpeed)
                    ? lowMomentum + dt : 0f;
                if ((lowMomentum >= math.max(0f, settings.LowMomentumPeriod)
                    && math.length(velocity.xz) < math.max(0f, settings.UsefulMomentumSpeed))
                    || math.lengthsq(velocity) < .000001f) break;
            }
        }

        private static void PathPoint(NativeList<LaunchPathPoint> path, float seconds, float3 position, float3 velocity,
            Entity surface = default, float3 normal = default)
        {
            if (path.IsCreated && path.Length < AuthoritativeLaunchPlan.MaximumPoints)
                path.Add(new LaunchPathPoint { Seconds = seconds, Position = position, Velocity = velocity, Surface = surface, Normal = normal });
        }

        private static bool PassesThroughDestroyedWall(EntityManager em, LaunchPreviewSource source, Entity target,
            float3 incoming, float3 normal, float inverseMass, in EnemyLaunchSettings settings, NativeHashSet<Entity> counted)
        {
            if (!em.HasComponent<Barricade>(target) || em.HasComponent<FinaleBoss>(target) || em.HasComponent<TrackObject>(target)) return false;
            var wall = em.GetComponentData<Barricade>(target);
            uint nextSequence = em.HasComponent<EnemyLaunchState>(source.Entity)
                ? em.GetComponentData<EnemyLaunchState>(source.Entity).LaunchSequence + 1 : 1;
            bool recorded = counted.Contains(target) || em.HasBuffer<BarricadeHitHistory>(target)
                && BarricadeHitResolution.Recorded(em.GetBuffer<BarricadeHitHistory>(target, true), source.Entity, nextSequence);
            bool allowed = FinaleHitResolution.Active(em, target);
            if (em.HasComponent<ShellTarget>(target))
            {
                float impulse = inverseMass > .0001f ? math.max(0f, -math.dot(incoming, normal)) / inverseMass : 0f;
                float damage = EnemyCollisionDamage.Calculate(source.Damage, impulse, settings);
                var shell = em.GetComponentData<ShellTarget>(target);
                float health = ShellHitResolution.CoreAfterImpact(shell, damage, allowed, recorded);
                if (health < shell.CoreHealth) counted.Add(target);
                return health <= 0f;
            }
            int remaining = BarricadeHitResolution.RemainingAfterHit(wall.HitsRemaining,
                allowed && BarricadeHitResolution.Allows(wall.Sources, EnemyLaunchOwner.Player), recorded);
            if (remaining < wall.HitsRemaining) counted.Add(target);
            return remaining <= 0;
        }

        private static bool IsImpactTarget(EntityManager em, Entity source, Entity target)
        {
            // PLAYER-003: solidity still bends the path, but immunity must not advertise an effective hit.
            if (em.HasComponent<DinoBoss>(target) || SnakeHitRules.Protected(em, target)) return false;
            if (em.HasComponent<RollingBoss>(target)
                && !RollingDamageResolution.CanReceiveDamage(em.GetComponentData<RollingBoss>(target), em.World.Time.ElapsedTime))
                return false;
            if (em.HasComponent<ShellTarget>(target))
            {
                var shell = em.GetComponentData<ShellTarget>(target);
                return shell.CoreHealth > 0 && (shell.ExplosionsRemaining <= 0
                    || em.HasComponent<ExplosiveEnemyState>(source)
                        && em.GetComponentData<ExplosiveEnemyState>(source).HasExploded == 0);
            }
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
