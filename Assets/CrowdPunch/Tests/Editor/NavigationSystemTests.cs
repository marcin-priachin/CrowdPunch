using CrowdPunch.Components;
using CrowdPunch.Systems.AI;
using CrowdPunch.Utilities;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
namespace CrowdPunch.Tests
{
    public sealed class NavigationSystemTests
    {
        [TestCase(EnemyLaunchPhase.Launched, NavigationMode.Travel)]
        [TestCase(EnemyLaunchPhase.Active, NavigationMode.Committed)]
        public void Vision004_PhysicalEscapeCacheClearsWhenVoluntaryTravelStops(EnemyLaunchPhase phase, NavigationMode mode)
        {
            using var blob = MakeGrid(); using var world = new World("Escape ownership transition");
            var em = world.EntityManager; Arena(em, blob); var enemy = Enemy(em);
            var system = world.GetOrCreateSystem<EnemyNavigationSystem>(); Tick(world, system, 0);
            em.SetComponentData(enemy, new NavigationPathState { Escaping = 1, NextEscapeSearchAt = 1, EscapeGoal = new float2(-7, 0) });
            em.SetComponentData(enemy, new EnemyLaunchState { Phase = phase });
            var intent = em.GetComponentData<NavigationIntent>(enemy); intent.Mode = mode; em.SetComponentData(enemy, intent);
            var movement = new DesiredMovement { Direction = new float3(0, 0, 1), Speed = 3 }; em.SetComponentData(enemy, movement);
            Tick(world, system, .1);
            Assert.That(em.GetComponentData<NavigationPathState>(enemy).Escaping, Is.Zero);
            Assert.That(em.GetComponentData<NavigationPathState>(enemy).NextEscapeSearchAt, Is.Zero);
            Assert.That(em.GetComponentData<DesiredMovement>(enemy).Speed, Is.EqualTo(3));
        }

        [TestCase(false, false)] [TestCase(true, false)] [TestCase(false, true)]
        public void Vision004_PhysicalCutoutReentryRespectsCrowdAndHazards(bool crowdInExit, bool hazardBlocked)
        {
            using var obstacles = new NativeArray<NavigationRectangle>(new[] { new NavigationRectangle
                { Minimum = new float2(-6), Maximum = new float2(6) } }, Allocator.Temp);
            using var blob = NavigationGridConstruction.Build(new float2(-12), new float2(12), 1,
                new float3(.62f, .97f, 1.62f), obstacles, Allocator.Persistent);
            using var world = new World("Physical cutout reentry"); var em = world.EntityManager;
            Arena(em, blob); var enemy = Enemy(em);
            using var body = Unity.Physics.CapsuleCollider.Create(new CapsuleGeometry
                { Radius = .5f, Vertex0 = new float3(0, -.5f, 0), Vertex1 = new float3(0, .5f, 0) });
            using var rock = Unity.Physics.BoxCollider.Create(new BoxGeometry
                { Center = float3.zero, Size = new float3(2), Orientation = quaternion.identity });
            using var physicsOwner = new PhysicsWorld(crowdInExit ? 2 : 1, 0, 0); var physics = physicsOwner;
            var bodies = physics.StaticBodies;
            bodies[0] = new RigidBody { Collider = rock, WorldFromBody = RigidTransform.identity, Scale = 1 };
            if (crowdInExit)
            {
                var other = em.CreateEntity(typeof(Enemy));
                bodies[1] = new RigidBody { Entity = other, Collider = body,
                    WorldFromBody = new RigidTransform(quaternion.identity, new float3(5.3f, 0, 0)), Scale = 1 };
            }
            physics.CollisionWorld.BuildBroadphase(ref physics, .02f, float3.zero, true);
            em.AddComponentData(em.CreateEntity(), new PhysicsWorldSingleton { PhysicsWorld = physics });
            em.AddComponentData(enemy, new PhysicsCollider { Value = body });
            em.SetComponentData(enemy, new NavigationAgent { Radius = .5f });
            em.SetComponentData(enemy, LocalTransform.FromPosition(new float3(4, 0, 0)));
            em.SetComponentData(enemy, NavigationIntent.Travel(new float3(9, 0, 0), 2, .2f, float3.zero));
            Assert.False(NavigationGeometry.TryEscape(ref blob.Value, new float2(4, 0), .5f, 0, out _), "Reproduces the old rectangular escape rejection.");
            var system = world.GetOrCreateSystem<EnemyNavigationSystem>();
            Tick(world, system, 0);
            Assert.Greater(em.GetComponentData<DesiredMovement>(enemy).Speed, 0);
            Assert.AreEqual(new float3(4, 0, 0), em.GetComponentData<LocalTransform>(enemy).Position, "Navigation cannot teleport the body.");
            em.AddComponent<GroundHazardRoute>(enemy); em.AddBuffer<GroundHazardWaypoint>(enemy);
            em.AddComponent<GroundHazardPolicy>(em.CreateEntity());
            var patch = em.CreateEntity(typeof(GroundHazard), typeof(GroundHazardState));
            var escape = em.GetComponentData<NavigationPathState>(enemy).EscapeGoal;
            em.SetComponentData(patch, new GroundHazard { Position = hazardBlocked ? new float3(escape.x, 0, escape.y) : new float3(-9, 0, -9), HalfSize = new float2(.2f) });
            em.SetComponentData(patch, new GroundHazardState { Introduced = 1, Phase = GroundHazardPhase.Active });
            var hazards = world.GetOrCreateSystem<GroundHazardNavigationSystem>();
            hazards.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            if (hazardBlocked) { Assert.That(em.GetComponentData<DesiredMovement>(enemy).Speed, Is.Zero); return; }
            Assert.That(em.GetComponentData<DesiredMovement>(enemy).Speed, Is.GreaterThan(0));
            for (int frame = 1; frame <= 400; frame++)
            {
                Tick(world, system, frame * .02);
                hazards.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                var movement = em.GetComponentData<DesiredMovement>(enemy);
                var pose = em.GetComponentData<LocalTransform>(enemy);
                pose.Position += movement.Direction * movement.Speed * .02f;
                em.SetComponentData(enemy, pose);
            }
            Assert.That(em.GetComponentData<LocalTransform>(enemy).Position.x, Is.GreaterThan(8.7f));
            Assert.That(em.GetComponentData<NavigationPathState>(enemy).Escaping, Is.Zero);
        }

        [Test]
        public void Vision004_PhysicalEscapeCannotCrossAnEnclosingSolid()
        {
            using var obstacles = new NativeArray<NavigationRectangle>(new[] { new NavigationRectangle
                { Minimum = new float2(-6), Maximum = new float2(6) } }, Allocator.Temp);
            using var blob = NavigationGridConstruction.Build(new float2(-12), new float2(12), 1,
                new float3(.62f, .97f, 1.62f), obstacles, Allocator.Persistent);
            using var world = new World("Physical escape solid containment"); var em = world.EntityManager;
            var enemy = em.CreateEntity(typeof(Enemy));
            using var body = Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .5f });
            using var wall = Unity.Physics.BoxCollider.Create(new BoxGeometry
                { Size = new float3(1, 4, 10), Orientation = quaternion.identity });
            using var physicsOwner = new PhysicsWorld(4, 0, 0); var physics = physicsOwner;
            var bodies = physics.StaticBodies;
            for (int i = 0; i < 4; i++) bodies[i] = new RigidBody { Collider = wall, Scale = 1,
                WorldFromBody = new RigidTransform(quaternion.RotateY(i * math.PI / 2),
                    math.rotate(quaternion.RotateY(i * math.PI / 2), new float3(3, 0, 0))) };
            physics.CollisionWorld.BuildBroadphase(ref physics, .02f, float3.zero, true);
            var navigation = world.GetOrCreateSystem<EnemyNavigationSystem>();
            Assert.False(NavigationPhysicalEscape.TryFind(ref blob.Value, physics.CollisionWorld, enemy, LocalTransform.Identity,
                new PhysicsCollider { Value = body }, world.Unmanaged.ResolveSystemStateRef(navigation).GetComponentLookup<Enemy>(true), 0, out _));
        }

        [Test]
        public void Enemy009_PausedGauntlet07ProjectileEscapesConservativeObstacleCorner()
        {
            // Captured from the user's paused encounter: physically clear of the rounded corner,
            // but inside the square-inflated navigation obstacle, with no valid navigation anchor.
            using var obstacles = new NativeArray<NavigationRectangle>(new[] { new NavigationRectangle
            { Minimum = new float2(-6, 10), Maximum = new float2(-3, 13) } }, Allocator.Temp);
            using var blob = NavigationGridConstruction.Build(new float2(-15), new float2(15), 1,
                new float3(.62f, .97f, 1.62f), obstacles, Allocator.Persistent);
            float2 position = new float2(-6.421355f, 9.660804f);
            Assert.AreEqual(-1, NavigationGeometry.Anchor(ref blob.Value, position, 0));
            Assert.IsTrue(NavigationGeometry.TryEscape(ref blob.Value, position, .5f, 0, out float2 entry));
            Assert.GreaterOrEqual(NavigationGeometry.Anchor(ref blob.Value, entry, 0), 0);

            using var world = new World("Paused elite projectile corner escape");
            var em = world.EntityManager;
            Arena(em, blob);
            Entity enemy = Enemy(em);
            em.SetComponentData(enemy, new NavigationAgent { Radius = .5f });
            em.SetComponentData(enemy, LocalTransform.FromPosition(new float3(position.x, 0, position.y)));
            em.SetComponentData(enemy, NavigationIntent.Travel(new float3(entry.x, 0, entry.y), 8, .4f,
                float3.zero, NavigationGoalKind.ExactSetup));
            Tick(world, world.GetOrCreateSystem<EnemyNavigationSystem>(), 0);
            DesiredMovement movement = em.GetComponentData<DesiredMovement>(enemy);
            Assert.Greater(movement.Speed, 0, "The selected projectile must move instead of leaving both enemies waiting.");
            Assert.Greater(math.dot(movement.Direction.xz, entry - position), 0);
            Assert.AreEqual(position, em.GetComponentData<LocalTransform>(enemy).Position.xz);
        }

        [TestCase(-.4f, -.4f, -2f, -2f, false)]
        [TestCase(-.4f, -.4f, 2f, 2f, true)]
        [TestCase(-1f, -.1f, .1f, -1f, false)]
        [TestCase(-1f, -.1f, .1f, -.1f, true)]
        [TestCase(-.3f, -.3f, -2f, -2f, true)]
        [TestCase(-2f, -.5f, 2f, -.5f, true)]
        public void Combat017_RoundEscapeChecksWholeSweepAndRetainsObstacleBlocking(
            float startX, float startZ, float endX, float endZ, bool blocked)
        {
            Assert.AreEqual(blocked, NavigationGeometry.SweptCircleIntersectsRectangle(
                new float2(startX, startZ), new float2(endX, endZ), .5f, float2.zero, new float2(3)));
        }

        [Test]
        public void Combat017_RecoveredBodyOutsideSpacingBoundsCanMoveBackInside()
        {
            using var blob = MakeGrid();
            using var world = new World("Navigation bounds reentry");
            var em = world.EntityManager;
            Arena(em, blob);
            Entity enemy = Enemy(em);
            em.SetComponentData(enemy, LocalTransform.FromPosition(new float3(-14, 0, 0)));
            var navigation = world.GetOrCreateSystem<EnemyNavigationSystem>();
            Tick(world, navigation, 0);
            DesiredMovement movement = em.GetComponentData<DesiredMovement>(enemy);
            Assert.Greater(movement.Direction.x, 0);
            Assert.That(movement.Speed, Is.GreaterThan(0).And.LessThanOrEqualTo(1.5f));
            Assert.AreEqual(-14, em.GetComponentData<LocalTransform>(enemy).Position.x,
                "Reentry remains velocity intent; navigation must not teleport the body.");
        }

        [Test]
        public void Combat017_BoundsReentryCannotCrossStaticObstacles()
        {
            using var obstacles = new NativeArray<NavigationRectangle>(new[] { new NavigationRectangle
            { Minimum = new float2(-11.5f, -10), Maximum = new float2(-10.5f, 10) } }, Allocator.Temp);
            using var blob = NavigationGridConstruction.Build(new float2(-10), new float2(10), 1,
                new float3(.2f, .6f, 1.2f), obstacles, Allocator.Persistent);
            Assert.IsFalse(NavigationGeometry.TryEscape(ref blob.Value, new float2(-14, 0), .2f, 0, out _));
        }

        private static BlobAssetReference<NavigationGridBlob> MakeGrid(bool sealedWall = false)
        {
            using var obstacles = new NativeArray<NavigationRectangle>(new[]{new NavigationRectangle{
                Minimum=new float2(-1,sealedWall?-10:-6),Maximum=new float2(1,sealedWall?10:6)}}, Allocator.Temp);
            return NavigationGridConstruction.Build(new float2(-10), new float2(10), 1, new float3(.2f, .6f, 1.2f), obstacles, Allocator.Persistent);
        }
        private static Entity Arena(EntityManager em, BlobAssetReference<NavigationGridBlob> blob)
        {
            var e = em.CreateEntity(typeof(NavigationGrid), typeof(NavigationRuntimeSettings), typeof(NavigationDiagnostics));
            em.SetComponentData(e, new NavigationGrid { Data = blob, ParticipationAnchor = new float2(-5, 0) });
            em.SetComponentData(e, new NavigationRuntimeSettings
            {
                Enabled = 1,
                DirectInterval = .2f,
                WaypointArrival = .2f,
                LookAhead = .5f,
                DestinationThreshold = 1,
                RepathCooldown = .2f,
                StuckDuration = 1000,
                MinimumProgress = .2f,
                FailureDelay = 3,
                SmoothingChecks = 4,
                GlobalBudget = 1,
                SearchLimit = 1000,
                ConcurrentSearches = 1,
                MaxPathLength = 128,
                MaxRequests = 16
            });
            return e;
        }
        private static Entity Enemy(EntityManager em, float z = 0)
        {
            var e = em.CreateEntity(typeof(Enemy), typeof(NavigationIntent), typeof(NavigationAgent), typeof(NavigationPathState),
                typeof(EnemyLaunchState), typeof(RespawnRequest), typeof(LocalTransform), typeof(DesiredMovement), typeof(EnemyMovementSettings));
            em.AddBuffer<NavigationWaypoint>(e); em.SetComponentEnabled<RespawnRequest>(e, false);
            em.SetComponentData(e, new NavigationAgent { Radius = .2f }); em.SetComponentData(e, LocalTransform.FromPosition(new float3(-5, 0, z)));
            em.SetComponentData(e, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            em.SetComponentData(e, new EnemyMovementSettings { BrakingAcceleration = 10 });
            em.SetComponentData(e, NavigationIntent.Travel(new float3(5, 0, z), 2, .2f, float3.zero, NavigationGoalKind.ExactSetup));
            return e;
        }
        private static void Tick(World w, SystemHandle system, double now)
        { w.SetTime(new TimeData(now, .02f)); system.Update(w.Unmanaged); w.EntityManager.CompleteAllTrackedJobs(); }
        [Test]
        public void SharedFifoCompletesEveryWaitingEnemyWithinGlobalBudget()
        {
            using var blob = MakeGrid(); using var world = new World("Navigation scheduler integration"); var em = world.EntityManager;
            var arena = Arena(em, blob); var agents = new[] { Enemy(em, -2), Enemy(em, -1), Enemy(em, 1), Enemy(em, 2) };
            var system = world.GetOrCreateSystem<EnemyNavigationSystem>(); int completed = 0;
            for (int frame = 0; frame < 1600 && completed < 4; frame++)
            {
                Tick(world, system, frame * .02); Assert.LessOrEqual(em.GetComponentData<NavigationDiagnostics>(arena).Expanded, 1);
                completed = 0; foreach (var e in agents) if (em.GetBuffer<NavigationWaypoint>(e).Length > 0) completed++;
            }
            Assert.AreEqual(4, completed, "FIFO requests must not starve behind active searches.");
            foreach (var e in agents) Assert.AreEqual(-5, em.GetComponentData<LocalTransform>(e).Position.x, "Navigation must not write transforms.");
        }
        [Test]
        public void ChangedGoalCancelsAnUnfinishedResultAndDisabledAssistanceRetainsLegacy()
        {
            using var blob = MakeGrid(); using var world = new World("Navigation changed goal integration"); var em = world.EntityManager;
            var arena = Arena(em, blob); var enemy = Enemy(em); var system = world.GetOrCreateSystem<EnemyNavigationSystem>();
            Tick(world, system, 0); Assert.AreEqual(1, em.GetComponentData<NavigationPathState>(enemy).Pending);
            em.SetComponentData(enemy, NavigationIntent.Travel(new float3(-7, 0, 0), 2, .2f, float3.zero)); Tick(world, system, 1);
            Assert.AreEqual(NavigationTravelState.Direct, em.GetComponentData<NavigationPathState>(enemy).TravelState);
            Assert.Less(em.GetComponentData<DesiredMovement>(enemy).Direction.x, 0);
            Assert.AreEqual(0, em.GetBuffer<NavigationWaypoint>(enemy).Length);
            Assert.Greater(em.GetComponentData<NavigationDiagnostics>(arena).StaleResults, 0);
            var settings = em.GetComponentData<NavigationRuntimeSettings>(arena); settings.Enabled = 0; em.SetComponentData(arena, settings);
            var legacy = new DesiredMovement { Direction = new float3(0, 0, 1), Speed = 7 }; em.SetComponentData(enemy, legacy); Tick(world, system, 2);
            Assert.AreEqual(legacy.Speed, em.GetComponentData<DesiredMovement>(enemy).Speed);
            Assert.AreEqual(legacy.Direction, em.GetComponentData<DesiredMovement>(enemy).Direction);
            Assert.AreEqual(0, em.GetComponentData<NavigationPathState>(enemy).Initialized);
        }
        [Test]
        public void PoolingAndSceneReloadInvalidateSearchAndResumeFromActualPosition()
        {
            // LOOP-006 and COMBAT-013: no route or pending work leaks across lifecycle boundaries.
            using var blob = MakeGrid(); using var world = new World("Navigation lifecycle integration"); var em = world.EntityManager;
            var arena = Arena(em, blob); var enemy = Enemy(em); var system = world.GetOrCreateSystem<EnemyNavigationSystem>();
            Tick(world, system, 0); uint version = em.GetComponentData<NavigationPathState>(enemy).Version;
            em.SetComponentEnabled<RespawnRequest>(enemy, true); Tick(world, system, .1);
            Assert.AreEqual(0, em.GetComponentData<NavigationPathState>(enemy).Pending);
            Assert.Greater(em.GetComponentData<NavigationPathState>(enemy).Version, version);
            em.SetComponentEnabled<RespawnRequest>(enemy, false); em.SetComponentData(enemy, LocalTransform.FromPosition(new float3(4, 0, 0)));
            Tick(world, system, .2); Assert.AreEqual(NavigationTravelState.Direct, em.GetComponentData<NavigationPathState>(enemy).TravelState);
            em.DestroyEntity(arena); Tick(world, system, .3); arena = Arena(em, blob); Tick(world, system, .4);
            Assert.AreEqual(0, em.GetComponentData<NavigationDiagnostics>(arena).Queued);
            Assert.AreEqual(0, em.GetBuffer<NavigationWaypoint>(enemy).Length);
        }
        [Test]
        public void ImpossibleSetupRetriesAfterCooldownWithoutPretendingToArrive()
        {
            using var blob = MakeGrid(true); using var world = new World("Navigation impossible setup integration"); var em = world.EntityManager;
            var arena = Arena(em, blob); var enemy = Enemy(em); var system = world.GetOrCreateSystem<EnemyNavigationSystem>();
            Tick(world, system, 0); for (int frame = 1; frame < 100; frame++) Tick(world, system, frame * .02);
            Assert.AreEqual(1, em.GetComponentData<NavigationDiagnostics>(arena).Failures);
            Assert.AreEqual(NavigationTravelState.Failed, em.GetComponentData<NavigationPathState>(enemy).TravelState);
            Tick(world, system, 3.1); Assert.AreEqual(2, em.GetComponentData<NavigationDiagnostics>(arena).Failures);
            Assert.AreEqual(0, em.GetComponentData<DesiredMovement>(enemy).Speed);
        }
    }
}
