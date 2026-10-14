using System.Linq;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Movement;
using CrowdPunch.Systems.Physics;
using CrowdPunch.Utilities;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class NinjaEnemyTests
    {
        private World world;
        private EntityManager em;
        private Entity player;
        private NinjaTuning tuning;
        [SetUp] public void Setup()
        {
            world = new World("NINJA-001..006"); em = world.EntityManager;
            world.SetTime(new TimeData(1, .05f));
            player = em.CreateEntity(typeof(PlayerSnapshot));
            em.SetComponentData(player, new PlayerSnapshot { Position = float3.zero, Forward = math.forward(), Radius = 1.5f, IsAvailable = true });
            tuning = AssetDatabase.LoadAssetAtPath<NinjaSettings>("Assets/CrowdPunch/Data/Settings/NinjaSettings.asset").Tuning;
        }
        [TearDown] public void TearDown() => world.Dispose();
        private Entity Ninja(float3 position)
        {
            var e = em.CreateEntity(typeof(Enemy), typeof(NinjaState), typeof(NinjaTuning), typeof(EnemyLaunchState),
                typeof(EnemyLifetime), typeof(LocalTransform), typeof(DesiredMovement), typeof(NavigationIntent),
                typeof(NavigationAgent), typeof(EnemyMovementSettings), typeof(EnemySeparationDistance),
                typeof(EnemyArchetypeSeparationDistances), typeof(CrowdPunch.Components.EnemyArchetype), typeof(PhysicsVelocity),
                typeof(Health), typeof(EnemyTier), typeof(RespawnRequest), typeof(ExternalImpulse), typeof(DamageRequest));
            em.SetComponentData(e, LocalTransform.FromPosition(position));
            em.SetComponentData(e, tuning);
            em.SetComponentData(e, new EnemyLifetime { Generation = 1 });
            em.SetComponentData(e, new NinjaState { Initialized = 1, Generation = 1, RandomState = (uint)e.Index + 1 });
            em.SetComponentData(e, new NavigationAgent { Radius = .45f });
            em.SetComponentData(e, new CrowdPunch.Components.EnemyArchetype { Value = EnemyArchetypeKind.Ninja });
            em.SetComponentData(e, new Health { Current = 30, Max = 30 });
            em.SetComponentData(e, new EnemyMovementSettings { WanderSpeed = 1.5f });
            em.SetComponentEnabled<RespawnRequest>(e, false);
            em.SetComponentEnabled<ExternalImpulse>(e, false);
            em.SetComponentEnabled<DamageRequest>(e, false);
            return e;
        }
        private void Decide() => world.GetOrCreateSystem<NinjaDecisionSystem>().Update(world.Unmanaged);
        private NinjaState State(Entity e) => em.GetComponentData<NinjaState>(e);
        private void Time(double now, float delta = .05f) => world.SetTime(new TimeData(now, delta));

        [Test]
        public void Ninja001_SavedTuningAndProfileHaveAgreedDefaults()
        {
            Assert.That(tuning.TriggerMode, Is.EqualTo(NinjaTriggerMode.Both));
            Assert.That(tuning.InitialDelay, Is.EqualTo(3)); Assert.That(tuning.MinimumCooldown, Is.EqualTo(6));
            Assert.That(tuning.PeriodicInterval, Is.EqualTo(10)); Assert.That(tuning.PeriodicVariation, Is.EqualTo(2));
            Assert.That(tuning.ProximityDistance, Is.EqualTo(6)); Assert.That(tuning.EngagementDistance, Is.EqualTo(20));
            Assert.That(tuning.PreferredMinimumDistance, Is.EqualTo(12)); Assert.That(tuning.PreferredMaximumDistance, Is.EqualTo(18));
            Assert.That(tuning.PreparationDuration, Is.EqualTo(.75f)); Assert.That(tuning.ArrivalWarningDuration, Is.EqualTo(.2f));
            Assert.That(tuning.DashSpeed, Is.EqualTo(48)); Assert.That(tuning.MaximumDashDistance, Is.EqualTo(5));
            Assert.That(tuning.RecoveryDuration, Is.EqualTo(1.6f)); Assert.That(tuning.PlayerDamage, Is.EqualTo(20));
            Assert.That(tuning.PlayerKnockback, Is.EqualTo(18)); Assert.That(tuning.PlayerInvulnerability, Is.EqualTo(.5f));
            Assert.That(tuning.MaximumConcurrentAmbushes, Is.EqualTo(2)); Assert.That(tuning.TeleportDistance, Is.EqualTo(2));
            Assert.That(tuning.RetreatSpeed, Is.EqualTo(AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>(
                "Assets/CrowdPunch/Data/Settings/PlayerMovementSettings.asset").MoveSpeed));
            var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>("Assets/CrowdPunch/Data/Settings/Enemies/NinjaEnemySpawnSettings.asset");
            var baseline = AssetDatabase.LoadAssetAtPath<EnemySpawnSettings>("Assets/CrowdPunch/Data/Settings/Enemies/EnemySpawnSettings.asset");
            Assert.That(profile.Health.Max, Is.EqualTo(baseline.Health.Max));
            Assert.That(profile.Archetype, Is.EqualTo(Configuration.EnemyArchetype.Ninja));
            Assert.That(profile.EnemyPrefab.GetComponent<CrowdPunch.Authoring.NinjaAuthoring>().settings, Is.Not.Null);
            Assert.That(profile.EnemyPrefab.GetComponent<UnityEngine.CapsuleCollider>().radius, Is.EqualTo(.45f));
        }

        [TestCase(NinjaTriggerMode.Periodic, false, false)]
        [TestCase(NinjaTriggerMode.Proximity, true, false)]
        [TestCase(NinjaTriggerMode.Both, true, false)]
        [TestCase(NinjaTriggerMode.Periodic, true, true)]
        [TestCase(NinjaTriggerMode.Proximity, false, true)]
        [TestCase(NinjaTriggerMode.Both, true, true)]
        public void Ninja004_TriggerModesRemainIndependent(NinjaTriggerMode mode, bool expected, bool periodicDue)
        {
            var e = Ninja(new float3(0, 0, periodicDue ? 14 : 4));
            var t = tuning; t.TriggerMode = mode; em.SetComponentData(e, t);
            var n = State(e); n.PeriodicAt = periodicDue ? 0 : 100; em.SetComponentData(e, n);
            Decide(); Assert.That(State(e).Phase == NinjaPhase.Preparing, Is.EqualTo(expected));
        }

        [Test]
        public void Ninja004_InitialDelayMinimumCooldownAndFreshLifetime()
        {
            var e = Ninja(new float3(0, 0, 4)); em.SetComponentData(e, default(NinjaState));
            Decide(); Assert.That(State(e).Phase, Is.EqualTo(NinjaPhase.Positioning));
            Time(3.99); Decide(); Assert.That(State(e).Phase, Is.EqualTo(NinjaPhase.Positioning));
            Time(4); Decide(); Assert.That(State(e).Phase, Is.EqualTo(NinjaPhase.Preparing));
            var n = State(e); NinjaScheduling.Interrupt(ref n, tuning, 4); em.SetComponentData(e, n);
            Time(9.99); Decide(); Assert.That(State(e).Phase, Is.EqualTo(NinjaPhase.Positioning));
            em.SetComponentData(e, new EnemyLifetime { Generation = 2 }); Time(20); Decide();
            Assert.That(State(e).ReadyAt, Is.EqualTo(23)); Assert.That(State(e).Generation, Is.EqualTo(2));
        }

        [Test]
        public void Ninja004_PeriodicRandomizationAndSlotRetryKeepReadiness()
        {
            var a = Ninja(new float3(-12, 0, 2)); var b = Ninja(new float3(12, 0, 2)); var c = Ninja(new float3(0, 0, 14));
            Decide(); Assert.That(new[] { a, b, c }.Count(e => State(e).OccupiesAmbushSlot), Is.EqualTo(2));
            Entity queued = new[] { a, b, c }.First(e => !State(e).OccupiesAmbushSlot);
            Assert.That(State(queued).RetryAt, Is.InRange(1.15, 1.35)); Assert.That(State(queued).PeriodicAt, Is.Zero);
            Entity released = new[] { a, b, c }.First(e => State(e).OccupiesAmbushSlot);
            var n = State(released); n.Phase = NinjaPhase.Recovering; n.SecondsRemaining = 1.6f; em.SetComponentData(released, n);
            Time(1.5); Decide(); Assert.That(State(queued).Phase, Is.EqualTo(NinjaPhase.Preparing));
            for (int i = 0; i < 25; i++)
            {
                NinjaScheduling.RestartCooldown(ref n, tuning, 30);
                Assert.That(n.ReadyAt, Is.EqualTo(36)); Assert.That(n.PeriodicAt, Is.InRange(38, 42));
            }
        }

        [Test]
        public void Ninja001_PreparationCompletesDespitePlayerLeavingEngagementRange()
        {
            var e = Ninja(new float3(0, 0, 14)); Decide();
            Assert.That(State(e).SecondsRemaining, Is.EqualTo(.75f));
            var p = em.GetComponentData<PlayerSnapshot>(player); p.Position.x = 50; em.SetComponentData(player, p);
            for (int i = 1; i <= 14; i++) { Time(1 + i * .05); Decide(); Assert.That(State(e).Phase, Is.EqualTo(NinjaPhase.Preparing)); }
            Time(1.75); Decide(); Assert.That(State(e).Phase, Is.EqualTo(NinjaPhase.TeleportPending));
        }

        [Test]
        public void Ninja001_DistanceManagementRetreatsAndApproaches()
        {
            var e = Ninja(new float3(0, 0, 4)); var n = State(e); n.ReadyAt = 100; em.SetComponentData(e, n);
            Decide(); var intent = em.GetComponentData<DesiredMovement>(e);
            Assert.That(intent.Direction.z, Is.GreaterThan(.99)); Assert.That(intent.Speed, Is.EqualTo(tuning.RetreatSpeed));
            em.SetComponentData(e, LocalTransform.FromPosition(new float3(0, 0, 19))); Decide();
            intent = em.GetComponentData<DesiredMovement>(e); Assert.That(intent.Direction.z, Is.LessThan(-.99));
            Assert.That(intent.Speed, Is.EqualTo(tuning.ApproachSpeed));
        }

        [Test]
        public void Ninja002_RearSearchRejectsObstaclesCrowdsHazardsBoundsAndDisconnectedSections()
        {
            using var rectangles = new NativeArray<NavigationRectangle>(0, Allocator.Temp);
            using var blob = NavigationGridConstruction.Build(new float2(-10), new float2(10), .5f, new float3(.6f, 1, 1.8f), rectangles, Allocator.Persistent);
            var grid = new NavigationGrid { Data = blob }; var bounds = new ArenaBounds { Extents = new float3(10) };
            var p = em.GetComponentData<PlayerSnapshot>(player); var e = Ninja(new float3(0, 0, 8));
            using var occupied = new NativeArray<NinjaTeleportOccupant>(1, Allocator.Temp);
            using var hazards = new NativeArray<GroundHazard>(1, Allocator.Temp);
            var c = NinjaTeleportGeometry.Candidate(p, tuning, .45f, 0, 0);
            Assert.That(c.z, Is.EqualTo(-2).Within(.001));
            var occupants = occupied; occupants[0] = new NinjaTeleportOccupant { Entity = e, Position = c, Radius = .45f };
            var patches = hazards; patches[0] = new GroundHazard { Position = new float3(8, 0, 8), Shape = GroundHazardShape.Circle, Radius = 1 };
            Assert.That(NinjaTeleportGeometry.Valid(grid, bounds, e, new float3(0, 0, 8), c, p, .45f, occupied, hazards), Is.True);
            occupants[0] = new NinjaTeleportOccupant { Entity = player, Position = c, Radius = .75f };
            Assert.That(NinjaTeleportGeometry.Valid(grid, bounds, e, new float3(0, 0, 8), c, p, .45f, occupied, hazards), Is.False);
            occupants[0] = new NinjaTeleportOccupant { Entity = player, Position = c + new float3(0, 1.2f, 0), Radius = .75f, HalfHeight = 1 };
            Assert.That(NinjaTeleportGeometry.Valid(grid, bounds, e, new float3(0, 0, 8), c, p, .45f, occupied, hazards), Is.False,
                "Tall capsule occupancy must include vertical segments, not just centre spheres.");
            occupants[0] = new NinjaTeleportOccupant { Entity = player, Position = c + new float3(0, 4, 0), Radius = .75f, PlanarReservation = 1 };
            Assert.That(NinjaTeleportGeometry.Valid(grid, bounds, e, new float3(0, 0, 8), c, p, .45f, occupied, hazards), Is.False,
                "A pending spawn reserves its landing footprint even while its authored warning sits above the floor.");
            occupants[0] = default; occupants[0] = new NinjaTeleportOccupant { Position = new float3(8, 0, 8) };
            patches[0] = new GroundHazard { Position = c, Radius = .3f, Shape = GroundHazardShape.Circle };
            Assert.That(NinjaTeleportGeometry.Valid(grid, bounds, e, new float3(0, 0, 8), c, p, .45f, occupied, hazards), Is.False);
            patches[0] = new GroundHazard { Position = new float3(8, 0, 8), Radius = .3f, Shape = GroundHazardShape.Circle };
            Assert.That(NinjaTeleportGeometry.Valid(grid, bounds, e, new float3(0, 0, 8), new float3(0, 0, -10), p, .45f, occupied, hazards), Is.False);
            Assert.That(NinjaTeleportGeometry.Valid(default, bounds, e, new float3(0, 0, 8), c, p, .45f, occupied, hazards), Is.False);
            using var wall = new NativeArray<NavigationRectangle>(new[] { new NavigationRectangle { Minimum = new float2(-10, 3), Maximum = new float2(10, 4) } }, Allocator.Temp);
            using var disconnected = NavigationGridConstruction.Build(new float2(-10), new float2(10), .5f, new float3(.6f, 1, 1.8f), wall, Allocator.Persistent);
            Assert.That(NinjaTeleportGeometry.Valid(new NavigationGrid { Data = disconnected }, bounds, e, new float3(0, 0, 8), c, p, .45f, occupied, hazards), Is.False);
            using var obstacle = new NativeArray<NavigationRectangle>(new[] { new NavigationRectangle { Minimum = new float2(-.4f, -2.4f), Maximum = new float2(.4f, -1.6f) } }, Allocator.Temp);
            using var obstructed = NavigationGridConstruction.Build(new float2(-10), new float2(10), .5f, new float3(.6f, 1, 1.8f), obstacle, Allocator.Persistent);
            grid.Data = obstructed;
            Assert.That(NinjaTeleportGeometry.Valid(grid, bounds, e, new float3(0, 0, 8), c, p, .45f, occupied, hazards), Is.False);
            bool alternative = false;
            for (int i = 1; i <= 12; i++)
            {
                c = NinjaTeleportGeometry.Candidate(p, tuning, .45f, i, 0);
                Assert.That(c.z, Is.LessThan(0));
                alternative |= NinjaTeleportGeometry.Valid(grid, bounds, e, new float3(0, 0, 8), c, p, .45f, occupied, hazards);
            }
            Assert.That(alternative, Is.True);
            p.Position = new float3(0, 0, 9.5f); p.Radius = .5f;
            c = NinjaTeleportGeometry.Candidate(p, tuning, .45f, 0, 0);
            Assert.That(NinjaTeleportGeometry.Valid(new NavigationGrid { Data = blob }, bounds, e, new float3(0, 0, 8),
                c, p, .45f, occupied, hazards), Is.True,
                "A physically clear target beside a wall must connect through an inward cell even when its own raster cell is blocked.");
        }

        [Test]
        public void Ninja002_WarningHazardsCancelAndRetryRepeatsFullPreparationBeforeLockedArrival()
        {
            using var rectangles = new NativeArray<NavigationRectangle>(0, Allocator.Temp);
            using var blob = NavigationGridConstruction.Build(new float2(-10), new float2(10), .5f, new float3(.6f, 1, 1.8f), rectangles, Allocator.Persistent);
            em.SetComponentData(em.CreateEntity(typeof(NavigationGrid)), new NavigationGrid { Data = blob });
            em.SetComponentData(em.CreateEntity(typeof(ArenaBounds)), new ArenaBounds { Extents = new float3(10) });
            var physics = new PhysicsWorld(0, 0, 0); physics.CollisionWorld.BuildBroadphase(ref physics, .05f, float3.zero, true);
            em.SetComponentData(em.CreateEntity(typeof(PhysicsWorldSingleton)), new PhysicsWorldSingleton { PhysicsWorld = physics });
            var e = Ninja(new float3(0, 0, 8)); var n = State(e); n.Phase = NinjaPhase.TeleportPending; em.SetComponentData(e, n);
            var hazard = em.CreateEntity(typeof(GroundHazard), typeof(GroundHazardState));
            em.SetComponentData(hazard, new GroundHazard { Shape = GroundHazardShape.Circle, Radius = 5 });
            em.SetComponentData(hazard, new GroundHazardState { Introduced = 1, Phase = GroundHazardPhase.Warning });
            try
            {
                world.GetOrCreateSystem<NinjaTeleportSystem>().Update(world.Unmanaged);
                n = State(e); Assert.That(n.Phase, Is.EqualTo(NinjaPhase.Positioning));
                Assert.That(n.RetryAt, Is.InRange(1.35, 1.65)); Assert.That(n.PeriodicAt, Is.Zero);
                Assert.That(em.GetComponentData<LocalTransform>(e).Position.z, Is.EqualTo(8));
                em.SetComponentData(hazard, new GroundHazardState { Introduced = 1, Phase = GroundHazardPhase.Inactive });
                Time(2); Decide(); Assert.That(State(e).Phase, Is.EqualTo(NinjaPhase.Preparing));
                Assert.That(State(e).SecondsRemaining, Is.EqualTo(.75f));
                for (int i = 1; i <= 15; i++) { Time(2 + i * .05); Decide(); }
                world.GetOrCreateSystem<NinjaTeleportSystem>().Update(world.Unmanaged);
                n = State(e); Assert.That(n.Phase, Is.EqualTo(NinjaPhase.ArrivalWarning));
                Assert.That(n.SecondsRemaining, Is.EqualTo(.2f));
                Assert.That(em.GetComponentData<LocalTransform>(e).Position.z, Is.EqualTo(-2).Within(.001));
                Assert.That(n.LockedDirection, Is.EqualTo(math.forward()));
                var p = em.GetComponentData<PlayerSnapshot>(player); p.Position = new float3(5, 0, 3); p.Forward = new float3(1, 0, 0);
                em.SetComponentData(player, p);
                for (int i = 1; i <= 3; i++)
                {
                    Time(2.75 + i * .05); Decide(); world.GetOrCreateSystem<NinjaMovementSystem>().Update(world.Unmanaged);
                    Assert.That(State(e).Phase, Is.EqualTo(NinjaPhase.ArrivalWarning));
                    Assert.That(em.GetComponentData<PhysicsVelocity>(e).Linear.xz, Is.EqualTo(float2.zero));
                }
                Time(2.95); Decide(); Assert.That(State(e).Phase, Is.EqualTo(NinjaPhase.Dashing));
                Assert.That(State(e).LockedDirection, Is.EqualTo(math.forward()));
            }
            finally { physics.Dispose(); }
        }

        [Test]
        public void Ninja002_CompleteCapsuleRejectsThinObstacleBetweenItsEndpointSpheres()
        {
            using var rectangles = new NativeArray<NavigationRectangle>(0, Allocator.Temp);
            using var blob = NavigationGridConstruction.Build(new float2(-10), new float2(10), .5f, new float3(.6f, 1, 1.8f), rectangles, Allocator.Persistent);
            em.SetComponentData(em.CreateEntity(typeof(NavigationGrid)), new NavigationGrid { Data = blob });
            em.SetComponentData(em.CreateEntity(typeof(ArenaBounds)), new ArenaBounds { Extents = new float3(10) });
            // This is static terrain. A dynamic body's broadphase would instead
            // require matching MotionData for the offset WorldFromBody below.
            var physics = new PhysicsWorld(1, 0, 0);
            using var box = Unity.Physics.BoxCollider.Create(new BoxGeometry { Size = new float3(.2f, .02f, .01f), Orientation = quaternion.identity }, CollisionFilter.Default);
            var bodies = physics.Bodies;
            bodies[0] = new RigidBody { Collider = box, Scale = 1,
                WorldFromBody = new RigidTransform(quaternion.identity, new float3(0, .275f, -2.425f)) };
            physics.CollisionWorld.BuildBroadphase(ref physics, .05f, float3.zero, true);
            em.SetComponentData(em.CreateEntity(typeof(PhysicsWorldSingleton)), new PhysicsWorldSingleton { PhysicsWorld = physics });
            var e = Ninja(new float3(0, 0, 8)); var n = State(e); n.Phase = NinjaPhase.TeleportPending; em.SetComponentData(e, n);
            var t = tuning; t.RearArcDegrees = 0; em.SetComponentData(e, t);
            try
            {
                world.GetOrCreateSystem<NinjaTeleportSystem>().Update(world.Unmanaged);
                Assert.That(State(e).Phase, Is.EqualTo(NinjaPhase.Positioning));
                Assert.That(em.GetComponentData<LocalTransform>(e).Position.z, Is.EqualTo(8));
            }
            finally { physics.Dispose(); }
        }

        [Test]
        public void Ninja003_DashLocksDirectionTruncatesLastStepAndStopsAtStaticWall()
        {
            var physics = new PhysicsWorld(0, 1, 0);
            using var box = Unity.Physics.BoxCollider.Create(new BoxGeometry { Center = float3.zero, Size = new float3(8, 4, .2f), Orientation = quaternion.identity }, CollisionFilter.Default);
            var bodies = physics.Bodies;
            bodies[0] = new RigidBody { WorldFromBody = new RigidTransform(quaternion.identity, new float3(0, 0, 3)), Collider = box, Scale = 1 };
            physics.CollisionWorld.BuildBroadphase(ref physics, .02f, float3.zero, true);
            var singleton = em.CreateEntity(typeof(PhysicsWorldSingleton)); em.SetComponentData(singleton, new PhysicsWorldSingleton { PhysicsWorld = physics });
            try
            {
                var e = Ninja(float3.zero); var n = State(e); n.Phase = NinjaPhase.Dashing; n.LockedDirection = math.forward(); em.SetComponentData(e, n);
                Time(1, .1f); world.GetOrCreateSystem<NinjaMovementSystem>().Update(world.Unmanaged);
                n = State(e); Assert.That(n.Phase, Is.EqualTo(NinjaPhase.Recovering));
                Assert.That(n.SegmentEnd.z, Is.InRange(2.4f, 2.5f));
                Assert.That(n.LockedDirection, Is.EqualTo(math.forward()));
                Assert.That(em.GetComponentData<PhysicsVelocity>(e).Linear.x, Is.Zero);
                n.Phase = NinjaPhase.Dashing; n.DistanceTravelled = 4.8f; em.SetComponentData(e, n);
                var p = em.GetComponentData<PlayerSnapshot>(player); p.Position.x = 30; em.SetComponentData(player, p);
                world.GetOrCreateSystem<NinjaMovementSystem>().Update(world.Unmanaged);
                Assert.That(State(e).SegmentEnd.z, Is.EqualTo(.2f).Within(.001f)); Assert.That(State(e).Phase, Is.EqualTo(NinjaPhase.Recovering));
            }
            finally { physics.Dispose(); }
        }

        [Test]
        public void Ninja003_PlayerHitUsesSweptPathOnceAndSharedInvulnerabilityBlocksKnockback()
        {
            var e = Ninja(new float3(0, 0, 2)); var n = State(e);
            n.Phase = NinjaPhase.Dashing; n.HasDashSegment = 1; n.SegmentStart = new float3(0, 0, -2); n.LockedDirection = math.forward(); em.SetComponentData(e, n);
            var go = new GameObject("NINJA-003 bridge"); var health = go.AddComponent<PlayerHealth>();
            var bridge = go.GetComponent<PlayerEcsBridge>(); PlayerBridgeRegistry.Register(bridge);
            // Edit Mode does not invoke ordinary MonoBehaviour lifecycle callbacks.
            foreach (string method in new[] { "Awake", "OnDisable", "OnEnable" })
                typeof(PlayerHealth).GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(health, null);
            health.ResetHealth();
            int pushes = 0; Vector3 impulse = default;
            health.DamageAccepted += value => { pushes++; impulse = value; };
            try
            {
                world.GetOrCreateSystem<NinjaPlayerImpactSystem>().Update(world.Unmanaged);
                Assert.That(health.CurrentHealth, Is.EqualTo(80)); Assert.That(pushes, Is.EqualTo(1));
                Assert.That(impulse.magnitude, Is.EqualTo(18)); Assert.That(State(e).HasHitPlayer, Is.EqualTo(1));
                n = State(e); n.HasDashSegment = 1; em.SetComponentData(e, n);
                world.GetOrCreateSystem<NinjaPlayerImpactSystem>().Update(world.Unmanaged);
                Assert.That(pushes, Is.EqualTo(1));
                var another = Ninja(new float3(0, 0, 2)); n.HasHitPlayer = 0; n.HasDashSegment = 1; em.SetComponentData(another, n);
                world.GetOrCreateSystem<NinjaPlayerImpactSystem>().Update(world.Unmanaged);
                Assert.That(health.CurrentHealth, Is.EqualTo(80)); Assert.That(pushes, Is.EqualTo(1));
                Assert.That(State(another).HasHitPlayer, Is.EqualTo(1));
                Assert.That(NinjaPlayerImpactSystem.Intersects(new float3(-3, 0, -2), new float3(-3, 0, 2), float3.zero, 1.95f), Is.False);
            }
            finally { PlayerBridgeRegistry.Unregister(bridge); UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase(NinjaPhase.Preparing), TestCase(NinjaPhase.ArrivalWarning), TestCase(NinjaPhase.Dashing)]
        public void Ninja005_PunchInterruptsEachPhaseAndUsesOrdinaryLaunchData(NinjaPhase phase)
        {
            var e = Ninja(float3.zero); var n = State(e); n.Phase = phase; n.HasDashSegment = 1; em.SetComponentData(e, n);
            var punch = new PunchSpecification { Origin = new float3(0, 0, -1), Direction = math.forward(), Range = 3, Radius = 2,
                Strength = 20, Damage = 20, AffectActive = 1, ApplyDamage = 1, Cause = EnemyLaunchCause.PlayerPunch };
            Assert.That(PunchResolution.TryApply(em, e, punch), Is.True);
            world.GetOrCreateSystem<NinjaInterruptSystem>().Update(world.Unmanaged);
            n = State(e); Assert.That(n.Phase, Is.EqualTo(NinjaPhase.Positioning)); Assert.That(n.HasDashSegment, Is.Zero);
            Assert.That(n.ReadyAt, Is.EqualTo(7));
            var launch = em.GetComponentData<EnemyLaunchState>(e);
            Assert.That(launch.Owner, Is.EqualTo(EnemyLaunchOwner.Player)); Assert.That(launch.LaunchDamage, Is.EqualTo(20));
            Assert.That(em.HasComponent<DasherSettings>(e), Is.False);
            Assert.That(NinjaDashContactSystem.Suppress(n, EnemyLaunchPhase.Launched, EnemyLaunchPhase.Active, EnemyCombatTier.Normal), Is.False);
        }

        [Test]
        public void Ninja003_PassThroughRetainsLaunchedInterruptionsAndRestoresOrdinaryCollisions()
        {
            var n = new NinjaState { Phase = NinjaPhase.Dashing };
            Assert.That(NinjaDashContactSystem.Suppress(n, EnemyLaunchPhase.Active, EnemyLaunchPhase.Active, EnemyCombatTier.Normal), Is.True);
            Assert.That(NinjaDashContactSystem.Suppress(n, EnemyLaunchPhase.Active, EnemyLaunchPhase.Recovering, EnemyCombatTier.Normal), Is.True);
            Assert.That(NinjaDashContactSystem.Suppress(n, EnemyLaunchPhase.Active, EnemyLaunchPhase.Launched, EnemyCombatTier.Normal), Is.False);
            Assert.That(NinjaDashContactSystem.Suppress(n, EnemyLaunchPhase.Active, EnemyLaunchPhase.Active, EnemyCombatTier.Elite), Is.False);
            Assert.That(NinjaDashContactSystem.Suppress(n, EnemyLaunchPhase.Launched, EnemyLaunchPhase.Active, EnemyCombatTier.Normal), Is.False);
            n.Phase = NinjaPhase.Recovering;
            Assert.That(NinjaDashContactSystem.Suppress(n, EnemyLaunchPhase.Active, EnemyLaunchPhase.Active, EnemyCombatTier.Normal), Is.False);
        }

        [Test]
        public void Ninja006_LegacyTwoFiniteWavesStayOutsideAllPlayerBuilds()
        {
            var first = AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>("Assets/CrowdPunch/Data/Settings/Waves/Progression/CP25_01_First_Ambush.asset");
            var second = AssetDatabase.LoadAssetAtPath<EnemyWaveSettings>("Assets/CrowdPunch/Data/Settings/Waves/Progression/CP25_02_Two_At_A_Time.asset");
            Assert.That(first.TotalEnemyCount, Is.EqualTo(7)); Assert.That(second.TotalEnemyCount, Is.EqualTo(15));
            Assert.That(first.Enemies.Single(x => x.Settings.Archetype == Configuration.EnemyArchetype.Ninja).MinimumCount, Is.EqualTo(1));
            Assert.That(second.Enemies.Single(x => x.Settings.Archetype == Configuration.EnemyArchetype.Ninja).MinimumCount, Is.EqualTo(3));
            Assert.That(first.ActivationMode, Is.EqualTo(EnemyWaveActivationMode.AllCurrentAndPreviousEnemiesDefeated));
            Assert.That(second.ActivationMode, Is.EqualTo(EnemyWaveActivationMode.AllCurrentAndPreviousEnemiesDefeated));
            Assert.That(first.ReplenishWhileBossLives || second.ReplenishWhileBossLives, Is.False);
            Assert.That(EditorBuildSettings.scenes.Any(s => s.path.Contains("Gauntlet_25")), Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/CrowdPunch/Scenes/Gauntlets/Gauntlet_25.unity"), Is.Not.Null);
        }
    }
}

