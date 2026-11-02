using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Systems.Physics
{
    [UpdateInGroup(typeof(GamePrePhysicsGroup))]
    [UpdateAfter(typeof(EnemyLaunchHomingSystem))]
    [UpdateAfter(typeof(LinkedPairConstraintSystem))]
    [UpdateAfter(typeof(RotatingCoverSystem))]
    [UpdateBefore(typeof(EnemyGroundConstraintSystem))]
    [UpdateBefore(typeof(BarricadeImpactSystem))]
    public partial struct AuthoritativeLaunchMotorSystem : ISystem
    {
        private EntityQuery movingSolids;
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            movingSolids = new EntityQueryBuilder(Allocator.Temp).WithAll<PhysicsCollider, LocalTransform>()
                .WithAny<RotatingCover, TrackObject>().AddAdditionalQuery().WithAll<PhysicsCollider, LocalTransform>()
                .WithAny<BossPart, ChickenBoss, RollingBoss, DinoBoss, FinaleBoss>().Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0f) return;
            var em = state.EntityManager;
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            using var hitStorage = new NativeList<ColliderCastHit>(Allocator.Temp);
            using var moving = movingSolids.ToEntityArray(Allocator.Temp);
            var hits = hitStorage;
            foreach (var (path, launch, velocity, pose, collider, points, source) in
                SystemAPI.Query<RefRW<AuthoritativeLaunchPath>, RefRO<EnemyLaunchState>, RefRW<PhysicsVelocity>,
                    RefRO<LocalTransform>, RefRO<PhysicsCollider>, DynamicBuffer<CommittedLaunchPathPoint>>().WithEntityAccess())
            {
                path.ValueRW.Contact = Entity.Null;
                path.ValueRW.ContactImpulse = 0f;
                if (launch.ValueRO.Phase != EnemyLaunchPhase.Launched || launch.ValueRO.Owner != EnemyLaunchOwner.Player
                    || launch.ValueRO.LaunchSequence != path.ValueRO.LaunchSequence || !collider.ValueRO.Value.IsCreated
                    || em.HasComponent<RespawnRequest>(source) && em.IsComponentEnabled<RespawnRequest>(source))
                { AuthoritativeLaunchPlan.Release(em, source); continue; }
                int index = path.ValueRO.NextPoint;
                while (index < points.Length && points[index].Value.Seconds <= path.ValueRO.Seconds + .000001f
                    && math.distancesq(points[index].Value.Position, pose.ValueRO.Position) < .000001f) index++;
                if (index >= points.Length || index < 1)
                { AuthoritativeLaunchPlan.Release(em, source); continue; }
                var start = points[index - 1].Value;
                var end = points[index].Value;
                float seconds = math.min(path.ValueRO.Seconds + dt, end.Seconds);
                float fraction = end.Seconds > start.Seconds + .000001f
                    ? math.saturate((seconds - start.Seconds) / (end.Seconds - start.Seconds)) : 1f;
                float3 goal = math.lerp(start.Position, end.Position, fraction);
                float3 flightVelocity = end.Surface != Entity.Null ? end.Velocity
                    : (end.Position - start.Position) / math.max(.000001f, end.Seconds - start.Seconds);
                bool reached = seconds >= end.Seconds - .000001f;
                if (TetherExceeded(em, source, goal))
                { AuthoritativeLaunchPlan.Release(em, source); continue; }
                hits.Clear();
                // Include touching endpoints: collider casts can exclude a hit at exactly fraction one.
                float3 castGoal = goal + math.normalizesafe(goal - pose.ValueRO.Position) * .002f;
                world.CastCollider(new ColliderCastInput(collider.ValueRO.Value, pose.ValueRO.Position, castGoal,
                    pose.ValueRO.Rotation, pose.ValueRO.Scale), ref hits);
                // Scripted solids can already have a newer pose than the last world build.
                for (int i = hits.Length - 1; i >= 0; i--)
                    if (UsesCurrentPose(em, hits[i].Entity))
                        hits.RemoveAtSwapBack(i);
                foreach (Entity solid in moving)
                {
                    var solidPose = em.GetComponentData<LocalTransform>(solid);
                    var solidCollider = em.GetComponentData<PhysicsCollider>(solid).Value;
                    if (!solidCollider.IsCreated) continue;
                    var body = new RigidBody { Entity = solid, Collider = solidCollider,
                        WorldFromBody = new RigidTransform(solidPose.Rotation, solidPose.Position), Scale = solidPose.Scale };
                    float3 delta = em.HasComponent<PhysicsVelocity>(solid)
                        ? em.GetComponentData<PhysicsVelocity>(solid).Linear * dt : float3.zero;
                    int first = hits.Length;
                    body.CastCollider(new ColliderCastInput(collider.ValueRO.Value, pose.ValueRO.Position,
                        castGoal - delta, pose.ValueRO.Rotation, pose.ValueRO.Scale), ref hits);
                    for (int i = first; i < hits.Length; i++)
                    { var hit = hits[i]; hit.Position += delta * hit.Fraction; hits[i] = hit; }
                }
                bool expected = false, blocked = false;
                float blockedFraction = 1f;
                ColliderCastHit contact = default, blocker = default;
                foreach (var hit in hits)
                {
                    if (hit.Entity == source || AuthoritativeLaunchPlan.CrowdBody(em, hit.Entity)
                        || em.HasComponent<CoverEnclosure>(hit.Entity)
                        || SnakeProjectileCollision.IgnoresOwner(em, source, hit.Entity)
                        || hit.Material.CollisionResponse == CollisionResponsePolicy.None
                        || hit.Material.CollisionResponse == CollisionResponsePolicy.RaiseTriggerEvents
                        || em.HasComponent<Barricade>(hit.Entity) && em.GetComponentData<Barricade>(hit.Entity).HitsRemaining <= 0)
                        continue;
                    float3 travel = goal - pose.ValueRO.Position;
                    float3 relativeTravel = travel - (UsesCurrentPose(em, hit.Entity) && em.HasComponent<PhysicsVelocity>(hit.Entity)
                        ? em.GetComponentData<PhysicsVelocity>(hit.Entity).Linear * dt : float3.zero);
                    if (math.dot(relativeTravel, hit.SurfaceNormal) >= -.00001f) continue;
                    float3 center = pose.ValueRO.Position + (castGoal - pose.ValueRO.Position) * hit.Fraction;
                    if (reached && hit.Entity == end.Surface
                        && math.distancesq(center, end.Position) <= AuthoritativeLaunchPlan.ContactTolerance * AuthoritativeLaunchPlan.ContactTolerance
                        && math.dot(math.normalizesafe(hit.SurfaceNormal), math.normalizesafe(end.Normal))
                            >= math.cos(math.radians(CommittedLaunchPlan.NormalToleranceDegrees)))
                    { expected = true; contact = hit; continue; }
                    // The tiny endpoint margin must not detect an unrelated solid beyond this step.
                    if (math.distance(pose.ValueRO.Position, center) > math.length(travel) + .0001f) continue;
                    blocked = true;
                    if (hit.Fraction <= blockedFraction) { blockedFraction = hit.Fraction; blocker = hit; }
                }
                if (blocked)
                {
                    // Visit the real contact before handing back control. Gameplay must see this
                    // hit even when the obstacle was not present in the displayed snapshot.
                    contact = blocker;
                    goal = math.lerp(pose.ValueRO.Position, castGoal, math.max(0f, blockedFraction - .0001f));
                    reached = false;
                }
                path.ValueRW.NextPoint = index;
                path.ValueRW.Seconds = seconds;
                path.ValueRW.ScheduledPosition = goal;
                path.ValueRW.ReachedPoint = reached ? (byte)1 : (byte)0;
                path.ValueRW.ExpectedContact = expected ? (byte)1 : (byte)0;
                path.ValueRW.ReleaseAfterContact = blocked ? (byte)1 : (byte)0;
                path.ValueRW.ExitVelocity = reached
                    ? index + 1 < points.Length ? AuthoritativeLaunchPlan.ExitVelocity(end, points[index + 1].Value) : end.Velocity
                    : (end.Position - start.Position) / math.max(.000001f, end.Seconds - start.Seconds);
                velocity.ValueRW.Linear = (goal - pose.ValueRO.Position) / dt;
                velocity.ValueRW.Angular = float3.zero;
                path.ValueRW.IncomingVelocity = flightVelocity;
                // Discrete physics can first report a touching body on the step after the
                // contact vertex. Keep that body's incoming speed through the remainder step.
                bool crowdEnd = reached && AuthoritativeLaunchPlan.CrowdBody(em, end.Surface);
                bool crowdStart = AuthoritativeLaunchPlan.CrowdBody(em, start.Surface);
                path.ValueRW.CrowdSurface = crowdEnd ? end.Surface : crowdStart ? start.Surface : Entity.Null;
                path.ValueRW.CrowdIncomingVelocity = crowdEnd ? end.Velocity : crowdStart ? start.Velocity : flightVelocity;
                if (expected || blocked)
                {
                    path.ValueRW.Contact = contact.Entity;
                    path.ValueRW.ContactPoint = contact.Position;
                    path.ValueRW.ContactNormal = contact.SurfaceNormal;
                    float3 otherVelocity = em.HasComponent<PhysicsVelocity>(contact.Entity)
                        ? em.GetComponentData<PhysicsVelocity>(contact.Entity).Linear : float3.zero;
                    float inverseMass = em.GetComponentData<PhysicsMass>(source).InverseMass
                        + (em.HasComponent<PhysicsMass>(contact.Entity) ? em.GetComponentData<PhysicsMass>(contact.Entity).InverseMass : 0f);
                    path.ValueRW.ContactImpulse = math.max(0f, -math.dot(flightVelocity - otherVelocity, contact.SurfaceNormal))
                        / math.max(.0001f, inverseMass);
                }
                em.SetComponentData(source, new PhysicsMassOverride { IsKinematic = 1 });
                if (em.HasComponent<DasherState>(source))
                {
                    var dash = em.GetComponentData<DasherState>(source);
                    dash.PreservedLaunchedVelocity = velocity.ValueRO.Linear; em.SetComponentData(source, dash);
                }
            }
        }

        private static bool UsesCurrentPose(EntityManager em, Entity entity)
            => em.HasComponent<RotatingCover>(entity) || em.HasComponent<TrackObject>(entity)
                || em.HasComponent<BossPart>(entity) || em.HasComponent<ChickenBoss>(entity)
                || em.HasComponent<RollingBoss>(entity) || em.HasComponent<DinoBoss>(entity)
                || em.HasComponent<FinaleBoss>(entity);

        private static bool TetherExceeded(EntityManager em, Entity source, float3 goal)
        {
            if (!LinkedPairLaunchResolution.TryGet(em, source, out var pairEntity, out var pair)) return false;
            Entity partner = source == pair.A ? pair.B : pair.A;
            return em.HasComponent<LocalTransform>(partner) && math.distance(goal.xz, em.GetComponentData<LocalTransform>(partner).Position.xz)
                > em.GetComponentData<LinkedPairTuning>(pairEntity).MaximumLength + .025f;
        }
    }
}
