using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.InputBridge;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Physics;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class CommittedLaunchTests
    {
        private World world;
        private EntityManager em;
        private PhysicsWorld physics;
        private BlobAssetReference<Unity.Physics.Collider> sphere, bounce;
        private Entity source, target, wall, physicsEntity;
        private readonly List<Entity> bodies = new List<Entity>();
        private GameObject root;
        private PlayerEcsBridge bridge, previous;
        private EnemyLaunchSettings settings;

        [SetUp]
        public void SetUp()
        {
            world = new World("PLAYER-004 committed aim and rebounds"); em = world.EntityManager;
            world.SetTime(new TimeData(0, 1f / 60));
            sphere = Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .5f });
            var material = Unity.Physics.Material.Default;
            material.Restitution = 1; material.RestitutionCombinePolicy = Unity.Physics.Material.CombinePolicy.Maximum;
            bounce = Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .5f }, CollisionFilter.Default, material);
            source = Body(float3.zero, true); target = Body(new float3(1.2f, 0, 2), true);
            wall = Body(new float3(0, 0, 8), false);
            em.SetComponentData(wall, new PhysicsCollider { Value = bounce });
            physicsEntity = em.CreateEntity(typeof(PhysicsWorldSingleton));
            physics = new PhysicsWorld(0, 0, 0);
            Rebuild();
            settings = new EnemyLaunchSettings { PropagationAimCorrectionRadius = 6, PropagationAimMaximumAngleDegrees = 20,
                MinimumPropagationImpulse = .1f, LaunchHomingDuration = .25f, LaunchHomingDegreesPerSecond = 45,
                LaunchHomingMaximumAngleDegrees = 20, AimLeadSeconds = .2f, AimLeadMaximumDistance = 2, UsefulMomentumSpeed = .1f };
            em.AddComponentData(em.CreateEntity(), settings);
            var step = PhysicsStep.Default; step.Gravity = float3.zero;
            em.AddComponentData(em.CreateEntity(), step);
            Entity player = em.CreateEntity(typeof(PlayerSnapshot), typeof(PlayerHealthSnapshot), typeof(PunchRequest));
            em.SetComponentEnabled<PunchRequest>(player, false);
            PlayerBridgeRegistry.TryGetBridge(out previous);
            root = new GameObject("Committed flight bridge"); bridge = root.AddComponent<PlayerEcsBridge>();
            PlayerBridgeRegistry.Register(bridge);
            bridge.PublishPunchPreview(Vector3.back, Vector3.forward, 1, 2, 5, 0, 22, 12,
                previewStrength: 12, releaseAngleDegrees: 16, useDottedTrajectory: true);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerBridgeRegistry.Unregister(bridge); if (previous != null) PlayerBridgeRegistry.Register(previous);
            Object.DestroyImmediate(root); world.Dispose(); physics.Dispose(); sphere.Dispose(); bounce.Dispose(); bodies.Clear();
        }

        private Entity Body(float3 position, bool enemy)
        {
            Entity body = em.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider), typeof(PhysicsMass));
            em.SetComponentData(body, LocalTransform.FromPosition(position));
            em.SetComponentData(body, new PhysicsCollider { Value = sphere });
            if (enemy)
            {
                em.AddComponent<Enemy>(body); em.AddComponent<EnemyTier>(body); em.AddComponent<EnemyLaunchState>(body);
                em.AddComponentData(body, new EnemyLifetime { Generation = 1 });
                em.AddComponentData(body, new Health { Current = 100, Max = 100 });
                em.AddComponent<PhysicsVelocity>(body); em.AddComponent<PunchAimAssistTarget>(body);
                em.AddComponent<CommittedLaunchFlight>(body); em.SetComponentEnabled<CommittedLaunchFlight>(body, false);
                em.AddBuffer<DisplayedLaunchRebound>(body); em.AddBuffer<CommittedLaunchRebound>(body);
                em.AddComponent<ExternalImpulse>(body); em.SetComponentEnabled<ExternalImpulse>(body, false);
                em.AddComponent<DamageRequest>(body); em.SetComponentEnabled<DamageRequest>(body, false);
                em.SetComponentData(body, PhysicsMass.CreateDynamic(sphere.Value.MassProperties, 1));
            }
            bodies.Add(body); return body;
        }

        private void Rebuild()
        {
            if (physics.Bodies.IsCreated) physics.Dispose();
            physics = new PhysicsWorld(bodies.Count, 0, 0);
            var rigidBodies = physics.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                var pose = em.GetComponentData<LocalTransform>(bodies[i]);
                rigidBodies[i] = new RigidBody { Entity = bodies[i], Collider = em.GetComponentData<PhysicsCollider>(bodies[i]).Value,
                    WorldFromBody = new RigidTransform(pose.Rotation, pose.Position), Scale = 1 };
            }
            physics.CollisionWorld.BuildBroadphase(ref physics, 1f / 60, float3.zero, true);
            em.SetComponentData(physicsEntity, new PhysicsWorldSingleton { PhysicsWorld = physics });
        }

        private void Preview() => world.GetOrCreateSystem<PresentationBridgeSystem>().Update(world.Unmanaged);
        private void Punch()
        {
            bridge.PublishPunch(Vector3.back, Vector3.forward, 1, 2, 12, .1f, 10, 0);
            world.GetOrCreateSystem<PlayerBridgeSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<PunchDetectionSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<ApplyImpulseSystem>().Update(world.Unmanaged);
        }

        [Test]
        public void Player004_ClickCopiesTheLastDisplayedAimPointAndDoesNotChaseLaterTargetMotion()
        {
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(.4f, 0, 8)));
            em.SetComponentData(target, new PhysicsVelocity { Linear = new float3(4, 0, 0) });
            em.SetComponentData(wall, LocalTransform.FromPosition(new float3(100, 0, 100)));
            em.SetComponentData(source, new PunchAimAssistTarget { Target = target, LaunchSpeed = 12,
                LeadSeconds = .2f, LeadMaximumDistance = 2, DirectionLimitDegrees = 16, Facing = math.forward() });
            Rebuild(); Preview();
            var shown = em.GetComponentData<PunchAimAssistTarget>(source);
            Assert.That(shown.PreviewAimPoint.x, Is.GreaterThan(.4f));
            Punch();
            var flight = em.GetComponentData<CommittedLaunchFlight>(source);
            Assert.That(em.IsComponentEnabled<CommittedLaunchFlight>(source), Is.True);
            Assert.That(flight.AimPoint, Is.EqualTo(shown.PreviewAimPoint));
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(-1, 0, 8)));
            em.SetComponentData(target, new PhysicsVelocity { Linear = new float3(-4, 0, 0) });
            em.SetComponentData(source, new PhysicsVelocity { Linear = math.forward() * 12 });
            Rebuild();
            world.GetOrCreateSystem<CommittedLaunchValidationSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<EnemyLaunchHomingSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<PhysicsVelocity>(source).Linear.x, Is.GreaterThan(0), "Aim at the displayed point on the right.");
            Assert.That(em.GetComponentData<CommittedLaunchFlight>(source).AimPoint, Is.EqualTo(shown.PreviewAimPoint));
        }

        [Test]
        public void Player004_PendingClickPreservesDisplayedPointWhenTheCurrentLockChanges()
        {
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(.4f, 0, 8)));
            em.SetComponentData(wall, LocalTransform.FromPosition(new float3(100, 0, 100)));
            em.SetComponentData(source, new PunchAimAssistTarget { Target = target, LaunchSpeed = 12,
                LeadSeconds = .2f, LeadMaximumDistance = 2, DirectionLimitDegrees = 16, Facing = math.forward() });
            Rebuild(); Preview();
            var shown = em.GetComponentData<PunchAimAssistTarget>(source);
            bridge.PublishPunch(Vector3.back, Vector3.forward, 1, 2, 12, .1f, 10, 0);
            Entity replacement = Body(new float3(-.5f, 0, 8), true);
            var aim = shown; aim.Target = replacement; em.SetComponentData(source, aim);
            Preview();
            world.GetOrCreateSystem<PlayerBridgeSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<PunchDetectionSystem>().Update(world.Unmanaged);
            var flight = em.GetComponentData<CommittedLaunchFlight>(source);
            Assert.That(flight.Target, Is.EqualTo(target)); Assert.That(flight.AimPoint, Is.EqualTo(shown.PreviewAimPoint));
        }

        [Test]
        public void Player004_ActualExpectedContactUsesSavedReboundAndPointDespiteLaterPreviewAndTargetChanges()
        {
            Preview();
            var shown = em.GetBuffer<DisplayedLaunchRebound>(source);
            Assert.That(shown.Length, Is.GreaterThan(0));
            var expected = shown[0].Value;
            Assert.That(expected.Surface, Is.EqualTo(wall)); Assert.That(expected.Target, Is.EqualTo(target));
            Punch();
            em.GetBuffer<DisplayedLaunchRebound>(source).Clear();
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(2, 0, 2)));
            em.SetComponentData(source, LocalTransform.FromPosition(new float3(0, 0, 6.8f)));
            em.SetComponentData(source, new PhysicsVelocity { Linear = new float3(0, 3, -10) });
            var launch = em.GetComponentData<EnemyLaunchState>(source);
            uint sequence = launch.LaunchSequence; float damage = launch.LaunchDamage;
            launch.RicochetSurface = wall; launch.RicochetNormal = -math.forward(); em.SetComponentData(source, launch);
            CommittedLaunchPlan.Contact(em, source, launch, wall, -math.forward());
            Rebuild();
            world.GetOrCreateSystem<CommittedLaunchReboundSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<EnemyRicochetAimSystem>().Update(world.Unmanaged);
            var velocity = em.GetComponentData<PhysicsVelocity>(source).Linear;
            Assert.That(math.distance(math.normalizesafe(velocity.xz), math.normalizesafe(expected.Direction.xz)), Is.LessThan(.0001f));
            Assert.That(math.length(velocity.xz), Is.EqualTo(10).Within(.0001f)); Assert.That(velocity.y, Is.EqualTo(3));
            var flight = em.GetComponentData<CommittedLaunchFlight>(source);
            Assert.That(flight.AimPoint, Is.EqualTo(expected.AimPoint)); Assert.That(flight.NextRebound, Is.EqualTo(1));
            Assert.That(flight.Cancelled, Is.Zero);
            launch = em.GetComponentData<EnemyLaunchState>(source);
            Assert.That(launch.LaunchSequence, Is.EqualTo(sequence)); Assert.That(launch.LaunchDamage, Is.EqualTo(damage));
            Assert.That(launch.Owner, Is.EqualTo(EnemyLaunchOwner.Player));
        }

        [TestCase("surface")]
        [TestCase("normal")]
        [TestCase("turn")]
        [TestCase("target")]
        [TestCase("generation")]
        [TestCase("obstruction")]
        [TestCase("multiple")]
        public void Player004_IncompatibleReboundCancelsWithoutReacquiringOrChangingPhysicalVelocity(string reason)
        {
            Preview(); var planned = em.GetBuffer<DisplayedLaunchRebound>(source)[0].Value; Punch();
            em.SetComponentData(source, LocalTransform.FromPosition(new float3(0, 0, 6.8f)));
            float3 physical = new float3(0, 2, -10);
            em.SetComponentData(source, new PhysicsVelocity { Linear = physical });
            Entity hit = reason == "surface" ? target : wall;
            float3 normal = reason == "normal" ? math.right() : -math.forward();
            if (reason == "turn")
            {
                planned.Direction = math.normalize(new float3(1, 0, -.5f));
                var active = em.GetBuffer<CommittedLaunchRebound>(source);
                active[0] = new CommittedLaunchRebound { Value = planned };
            }
            if (reason == "target") em.SetComponentData(target, new Health { Current = 0 });
            if (reason == "generation") em.SetComponentData(target, new EnemyLifetime { Generation = 2 });
            if (reason == "obstruction") Body(new float3(.6f, 0, 4.4f), false);
            var launch = em.GetComponentData<EnemyLaunchState>(source);
            launch.RicochetSurface = hit; launch.RicochetNormal = normal; em.SetComponentData(source, launch);
            CommittedLaunchPlan.Contact(em, source, launch, hit, normal);
            if (reason == "multiple") CommittedLaunchPlan.Contact(em, source, launch, target, -math.forward());
            Rebuild();
            world.GetOrCreateSystem<CommittedLaunchReboundSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<EnemyRicochetAimSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<CommittedLaunchFlight>(source).Cancelled, Is.EqualTo(1));
            Assert.That(em.GetComponentData<EnemyLaunchState>(source).HomingTarget, Is.EqualTo(Entity.Null));
            Assert.That(em.GetComponentData<PhysicsVelocity>(source).Linear, Is.EqualTo(physical));
        }

        [Test]
        public void Player004_RepunchReplacesBothCommittedBuffersAndCancelledState()
        {
            Preview(); Punch();
            var flight = em.GetComponentData<CommittedLaunchFlight>(source); flight.Cancelled = 1;
            em.SetComponentData(source, flight);
            uint first = em.GetComponentData<EnemyLaunchState>(source).LaunchSequence;
            Preview(); Punch();
            Assert.That(em.GetComponentData<EnemyLaunchState>(source).LaunchSequence, Is.EqualTo(first + 1));
            Assert.That(em.GetComponentData<CommittedLaunchFlight>(source).Cancelled, Is.Zero);
            Assert.That(em.GetComponentData<CommittedLaunchFlight>(source).NextRebound, Is.Zero);
        }

        [TestCase("target")]
        [TestCase("generation")]
        [TestCase("obstruction")]
        [TestCase("expiry")]
        [TestCase("lifecycle")]
        public void Player004_FlightValidationCancelsInvalidPlansAndDisablesEndedLaunches(string reason)
        {
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(0, 0, 8)));
            em.SetComponentData(wall, LocalTransform.FromPosition(new float3(100, 0, 100)));
            em.SetComponentData(source, new PunchAimAssistTarget { Target = target, LaunchSpeed = 12,
                LeadSeconds = .2f, LeadMaximumDistance = 2, DirectionLimitDegrees = 16, Facing = math.forward() });
            Rebuild(); Preview(); Punch();
            if (reason == "target") em.SetComponentData(target, new Health { Current = 0 });
            if (reason == "generation") em.SetComponentData(target, new EnemyLifetime { Generation = 2 });
            if (reason == "obstruction") Body(new float3(0, 0, 4), false);
            if (reason == "expiry")
            {
                var flight = em.GetComponentData<CommittedLaunchFlight>(source); flight.SecondsRemaining = 0;
                em.SetComponentData(source, flight);
            }
            if (reason == "lifecycle")
            {
                var launch = em.GetComponentData<EnemyLaunchState>(source); launch.Phase = EnemyLaunchPhase.Recovering;
                em.SetComponentData(source, launch);
            }
            Rebuild(); world.GetOrCreateSystem<CommittedLaunchValidationSystem>().Update(world.Unmanaged);
            if (reason == "lifecycle") Assert.That(em.IsComponentEnabled<CommittedLaunchFlight>(source), Is.False);
            else
            {
                Assert.That(em.GetComponentData<CommittedLaunchFlight>(source).Cancelled, Is.EqualTo(1));
                Assert.That(em.GetComponentData<EnemyLaunchState>(source).HomingTarget, Is.EqualTo(Entity.Null));
            }
        }

        [TestCase(100)]
        [TestCase(1000)]
        public void Player004_ProfileCommittedPreviewAndFlightValidation(int crowd)
        {
            for (int i = 3; i < crowd; i++) Body(new float3(30 + i % 32 * 2, 0, i / 32 * 2), true);
            Rebuild(); Preview();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10; i++) Preview();
            timer.Stop();
            TestContext.WriteLine($"Committed preview: {crowd} bodies, one direct source, {timer.Elapsed.TotalMilliseconds / 10:F3} ms/update (warm Editor).");
            Punch();
            var validate = world.GetOrCreateSystem<CommittedLaunchValidationSystem>();
            var homing = world.GetOrCreateSystem<EnemyLaunchHomingSystem>();
            var rebound = world.GetOrCreateSystem<CommittedLaunchReboundSystem>();
            var committed = em.GetComponentData<CommittedLaunchFlight>(source);
            timer.Restart();
            for (int i = 0; i < 100; i++)
            {
                em.SetComponentData(source, committed);
                validate.Update(world.Unmanaged); homing.Update(world.Unmanaged); rebound.Update(world.Unmanaged);
            }
            timer.Stop();
            Assert.That(em.IsComponentEnabled<CommittedLaunchFlight>(source), Is.True);
            TestContext.WriteLine($"Committed validation/homing/contact: {crowd} bodies, one planned source, {timer.Elapsed.TotalMilliseconds / 100:F3} ms/update (Editor).");
        }

        [Test]
        public void Player004_ResetDisablesCommitmentAndClearsBothSnapshots()
        {
            Preview(); Punch();
            Assert.That(em.GetBuffer<CommittedLaunchRebound>(source).Length, Is.GreaterThan(0));
            CommittedLaunchPlan.Reset(em, source);
            Assert.That(em.IsComponentEnabled<CommittedLaunchFlight>(source), Is.False);
            Assert.That(em.GetBuffer<CommittedLaunchRebound>(source).Length, Is.Zero);
            Assert.That(em.GetBuffer<DisplayedLaunchRebound>(source).Length, Is.Zero);
        }

        [Test]
        public void Player004_SystemGroupsOrderCommitmentAroundPhysicsAndRecovery()
        {
            var pre = world.GetOrCreateSystemManaged<GamePrePhysicsGroup>();
            var impulse = world.GetOrCreateSystem<ApplyImpulseSystem>();
            var validation = world.GetOrCreateSystem<CommittedLaunchValidationSystem>();
            var homing = world.GetOrCreateSystem<EnemyLaunchHomingSystem>();
            pre.AddSystemToUpdateList(homing); pre.AddSystemToUpdateList(validation); pre.AddSystemToUpdateList(impulse);
            pre.SortSystems();
            using var before = pre.GetAllSystems();
            Assert.That(Index(before, impulse), Is.LessThan(Index(before, validation)));
            Assert.That(Index(before, validation), Is.LessThan(Index(before, homing)));
            var post = world.GetOrCreateSystemManaged<GamePostPhysicsGroup>();
            var collision = world.GetOrCreateSystem<EnemyLaunchCollisionSystem>();
            var dash = world.GetOrCreateSystem<DasherEnemyImpactSystem>();
            var boss = world.GetOrCreateSystem<BossHeadBounceSystem>();
            var committed = world.GetOrCreateSystem<CommittedLaunchReboundSystem>();
            var fallback = world.GetOrCreateSystem<EnemyRicochetAimSystem>();
            var recovery = world.GetOrCreateSystem<EnemyRecoverySystem>();
            post.AddSystemToUpdateList(recovery); post.AddSystemToUpdateList(committed); post.AddSystemToUpdateList(fallback);
            post.AddSystemToUpdateList(collision); post.AddSystemToUpdateList(dash); post.AddSystemToUpdateList(boss);
            post.SortSystems();
            using var after = post.GetAllSystems();
            Assert.That(Index(after, collision), Is.LessThan(Index(after, committed)));
            Assert.That(Index(after, dash), Is.LessThan(Index(after, committed)));
            Assert.That(Index(after, boss), Is.LessThan(Index(after, committed)));
            Assert.That(Index(after, committed), Is.LessThan(Index(after, fallback)));
            Assert.That(Index(after, fallback), Is.LessThan(Index(after, recovery)));

            int Index(NativeList<SystemHandle> systems, SystemHandle handle)
            {
                for (int i = 0; i < systems.Length; i++) if (systems[i] == handle) return i;
                return -1;
            }
        }
    }
}
