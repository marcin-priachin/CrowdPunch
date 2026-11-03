using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.InputBridge;
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
    public sealed class StableLaunchAimTests
    {
        private World world;
        private EntityManager em;
        private PhysicsWorld physics;
        private BlobAssetReference<Unity.Physics.Collider> sphere;
        private Entity source, target, other, player, tuningEntity, physicsEntity;
        private PlayerEcsBridge bridge, previous;
        private GameObject root;
        private EnemyLaunchSettings tuning;

        [SetUp]
        public void SetUp()
        {
            world = new World("PLAYER-004 stable aiming"); em = world.EntityManager;
            physics = new PhysicsWorld(0, 0, 0);
            physics.CollisionWorld.BuildBroadphase(ref physics, .02f, float3.zero, true);
            physicsEntity = em.CreateEntity(typeof(PhysicsWorldSingleton));
            em.SetComponentData(physicsEntity, new PhysicsWorldSingleton { PhysicsWorld = physics });
            sphere = Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .5f });
            source = Body(float3.zero); target = Body(new float3(.5f, 0, 8)); other = Body(new float3(1.5f, 0, 8));
            player = em.CreateEntity(typeof(PlayerSnapshot), typeof(PlayerHealthSnapshot), typeof(PunchRequest));
            em.SetComponentEnabled<PunchRequest>(player, false);
            tuning = new EnemyLaunchSettings { AimLeadSeconds = .2f, AimLeadMaximumDistance = 2,
                LaunchHomingDegreesPerSecond = 45, LaunchHomingDuration = .25f, LaunchHomingMaximumAngleDegrees = 20,
                PropagationAimCorrectionRadius = 6, PropagationAimMaximumAngleDegrees = 20, MinimumPropagationImpulse = 1 };
            tuningEntity = em.CreateEntity(typeof(EnemyLaunchSettings)); em.SetComponentData(tuningEntity, tuning);
            PlayerBridgeRegistry.TryGetBridge(out previous);
            root = new GameObject("Stable aim regression"); bridge = root.AddComponent<PlayerEcsBridge>();
            PlayerBridgeRegistry.Register(bridge);
            bridge.PublishPunchPreview(Vector3.back, Vector3.forward, 1, 2, 5, 0, 22, 12, 3, 18, 90, 16, .12f, .35f);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerBridgeRegistry.Unregister(bridge); if (previous != null) PlayerBridgeRegistry.Register(previous);
            Object.DestroyImmediate(root); world.Dispose(); physics.Dispose(); sphere.Dispose();
        }

        private Entity Body(float3 position)
        {
            Entity entity = em.CreateEntity(typeof(Enemy), typeof(EnemyTier), typeof(LocalTransform), typeof(Health),
                typeof(EnemyLaunchState), typeof(PunchAimAssistTarget), typeof(PhysicsVelocity), typeof(PhysicsMass),
                typeof(PhysicsCollider), typeof(ExternalImpulse), typeof(DamageRequest));
            em.SetComponentData(entity, LocalTransform.FromPosition(position));
            em.SetComponentData(entity, new Health { Current = 100, Max = 100 });
            em.SetComponentData(entity, PhysicsMass.CreateDynamic(sphere.Value.MassProperties, 1));
            em.SetComponentData(entity, new PhysicsCollider { Value = sphere });
            em.SetComponentEnabled<ExternalImpulse>(entity, false); em.SetComponentEnabled<DamageRequest>(entity, false);
            return entity;
        }

        private void Aim(double time)
        {
            world.SetTime(new TimeData(time, .02f));
            world.GetOrCreateSystem<PunchAimAssistSystem>().Update(world.Unmanaged);
        }

        private Entity GatekeeperHead(float3 position)
        {
            Entity head = em.CreateEntity(typeof(BossEncounter), typeof(BossPart), typeof(LocalTransform),
                typeof(PhysicsCollider), typeof(PhysicsVelocity));
            em.SetComponentData(head, LocalTransform.FromPosition(position));
            em.SetComponentData(head, new BossPart { Encounter = head, Kind = BossPartKind.Head });
            em.SetComponentData(head, new PhysicsCollider { Value = sphere });
            return head;
        }

        private Entity GatekeeperHand(Entity head, BossPartKind kind, float3 position)
        {
            Entity hand = em.CreateEntity(typeof(BossPart), typeof(LocalTransform), typeof(PhysicsCollider));
            em.SetComponentData(hand, new BossPart { Encounter = head, Kind = kind });
            em.SetComponentData(hand, LocalTransform.FromPosition(position));
            em.SetComponentData(hand, new PhysicsCollider { Value = sphere });
            return hand;
        }

        private void SetCollisionBodies(params Entity[] entities)
        {
            physics.Dispose(); physics = new PhysicsWorld(entities.Length, 0, 0);
            var bodies = physics.Bodies;
            for (int i = 0; i < entities.Length; i++)
                bodies[i] = new RigidBody { Entity = entities[i], Collider = em.GetComponentData<PhysicsCollider>(entities[i]).Value,
                    WorldFromBody = new RigidTransform(quaternion.identity, em.GetComponentData<LocalTransform>(entities[i]).Position), Scale = 1 };
            physics.CollisionWorld.BuildBroadphase(ref physics, .02f, float3.zero, true);
            em.SetComponentData(physicsEntity, new PhysicsWorldSingleton { PhysicsWorld = physics });
        }

        [TestCase(BossPartKind.LeftHand)]
        [TestCase(BossPartKind.RightHand)]
        public void Boss001_Player004_HeadWinsThroughOwnHandAndOverFacingRayEnemy(BossPartKind kind)
        {
            Entity head = GatekeeperHead(new float3(.8f, 0, 10));
            Entity hand = GatekeeperHand(head, kind, new float3(.4f, 0, 5));
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(0, 0, 8)));
            SetCollisionBodies(head, hand, target);
            Aim(0);
            Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(head));

            world.GetOrCreateSystem<PresentationBridgeSystem>().Update(world.Unmanaged);
            bridge.PublishPunch(Vector3.back, Vector3.forward, 1, 2, 90, .1f, 10, 0);
            world.GetOrCreateSystem<PlayerBridgeSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<PunchDetectionSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyLaunchState>(source).HomingTarget, Is.EqualTo(head));
            float3 direction = math.normalize(em.GetComponentData<ExternalImpulse>(source).Value);
            Assert.That(math.distance(direction, math.normalize(new float3(.8f, 0, 10))), Is.LessThan(.0001f));
            Assert.That(physics.CollisionWorld.CastRay(new RaycastInput { Start = float3.zero,
                End = new float3(.8f, 0, 10), Filter = CollisionFilter.Default }, out Unity.Physics.RaycastHit hit), Is.True);
            Assert.That(hit.Entity, Is.EqualTo(hand), "BOSS-002: selection must preserve physical hand interception.");
        }

        [Test]
        public void Boss001_Player004_HeadChallengerUsesNormalDelayAndRetainsReleaseConePriority()
        {
            Aim(0);
            Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(target));
            Entity head = GatekeeperHead(new float3(.8f, 0, 10));
            Aim(.01); Aim(.1);
            Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(target));
            Aim(.14);
            Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(head));
            em.SetComponentData(head, LocalTransform.FromPosition(new float3(math.tan(math.radians(14)) * 10, 0, 10)));
            Aim(.2); Aim(.4);
            Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(head));
            em.SetComponentData(head, LocalTransform.FromPosition(new float3(math.tan(math.radians(17)) * 10, 0, 10)));
            Aim(.42);
            Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(target));
        }

        [TestCase(0f, 23f, BossCycle.Opening)]
        [TestCase(4f, 10f, BossCycle.Opening)]
        [TestCase(0f, 10f, BossCycle.Defeated)]
        public void Boss001_Player004_IneligibleHeadFallsBackToOrdinaryTarget(float x, float z, BossCycle cycle)
        {
            Entity head = GatekeeperHead(new float3(x, 0, z));
            em.SetComponentData(head, new BossEncounter { Cycle = cycle });
            Aim(0);
            Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(target));
        }

        [Test]
        public void Boss001_Player004_HeadPriorityDoesNotBypassWallsOrOtherEncountersHands()
        {
            Entity head = GatekeeperHead(new float3(.8f, 0, 10));
            Entity hand = GatekeeperHand(head, BossPartKind.LeftHand, new float3(.4f, 0, 5));
            using var wallCollider = Unity.Physics.BoxCollider.Create(new BoxGeometry {
                Size = new float3(5, 5, .5f), Orientation = quaternion.identity });
            Entity wall = em.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider));
            em.SetComponentData(wall, LocalTransform.FromPosition(new float3(0, 0, 7)));
            em.SetComponentData(wall, new PhysicsCollider { Value = wallCollider });
            SetCollisionBodies(head, hand, wall);
            Aim(0);
            Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(Entity.Null));

            var part = em.GetComponentData<BossPart>(hand); part.Encounter = em.CreateEntity();
            em.SetComponentData(hand, part);
            SetCollisionBodies(head, hand);
            Aim(.1);
            Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.Not.EqualTo(head));
        }

        [Test]
        public void Boss001_Player004_ZeroAssistRangeClearsHeadSelection()
        {
            Entity head = GatekeeperHead(new float3(.8f, 0, 10));
            Aim(0);
            Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(head));
            bridge.PublishPunchPreview(Vector3.back, Vector3.forward, 1, 2, 5, 0, 0, 12, 3, 18, 90, 16, .12f, .35f);
            Aim(.02);
            Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(Entity.Null));
        }

        [Test]
        public void Player004_ChallengerMustPersistAndJitterRestartsItsDelay()
        {
            var aim = new PunchAimAssistTarget { Target = target };
            LaunchAimPrediction.UpdateLock(ref aim, other, true, 1, .12f);
            LaunchAimPrediction.UpdateLock(ref aim, other, true, 1.1, .12f);
            Assert.That(aim.Target, Is.EqualTo(target));
            LaunchAimPrediction.UpdateLock(ref aim, target, true, 1.11, .12f);
            LaunchAimPrediction.UpdateLock(ref aim, other, true, 1.12, .12f);
            LaunchAimPrediction.UpdateLock(ref aim, other, true, 1.2, .12f);
            Assert.That(aim.Target, Is.EqualTo(target));
            LaunchAimPrediction.UpdateLock(ref aim, other, true, 1.24, .12f);
            Assert.That(aim.Target, Is.EqualTo(other));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Snake006_ProtectedPartsClearLocksAndPublishNoLaunchPreview(bool isHead)
        {
            Entity head = em.CreateEntity(typeof(SnakeHead));
            em.SetComponentData(head, new SnakeHead { AttachedCount = 2, Generation = 1 });
            if (isHead) em.AddComponentData(source, new SnakeHead { AttachedCount = 2, Generation = 1 });
            else em.AddComponentData(source, new SnakeSegment { Head = head, Index = 0, Generation = 1 });
            em.SetComponentData(source, new PunchAimAssistTarget { Target = target, IsAiming = 1 });
            Aim(0);
            Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(Entity.Null));
            world.GetOrCreateSystem<PresentationBridgeSystem>().Update(world.Unmanaged);
            Assert.That(bridge.TrajectoryPreviewSegments.Count, Is.Zero);

            if (isHead) em.SetComponentData(source, new SnakeHead { AttachedCount = 0, Generation = 1 });
            else em.SetComponentData(head, new SnakeHead { AttachedCount = 1, Generation = 1 });
            Aim(.02);
            Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(target));
            world.GetOrCreateSystem<PresentationBridgeSystem>().Update(world.Unmanaged);
            Assert.That(bridge.TrajectoryPreviewSegments.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Snake006_ProtectedSegmentDoesNotMaskEligibleFacingRayTarget()
        {
            Entity head = em.CreateEntity(typeof(SnakeHead));
            em.SetComponentData(head, new SnakeHead { AttachedCount = 2, Generation = 1 });
            Entity protectedSegment = Body(new float3(0, 0, 4));
            em.AddComponentData(protectedSegment, new SnakeSegment { Head = head, Index = 0, Generation = 1 });
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(.2f, 0, 6)));
            em.SetComponentData(other, LocalTransform.FromPosition(new float3(0, 0, 18)));
            SetCollisionBodies(protectedSegment, other);
            Aim(0);
            Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(other),
                "The valid facing-ray hit takes priority over the nearer fallback after skipping protection.");
            Assert.That(PunchAimAssist.IsValidTarget(em, source, protectedSegment), Is.False);
        }

        [Test]
        public void Player004_SystemKeepsLockAtReleaseBoundaryAndRequiresPersistentReplacement()
        {
            Aim(0); Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(target));
            em.SetComponentData(other, LocalTransform.FromPosition(new float3(0, 0, 8)));
            Aim(.01); Aim(.1);
            Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(target));
            Aim(.14); Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(other));
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(100, 0, 100)));
            em.SetComponentData(other, LocalTransform.FromPosition(new float3(math.tan(math.radians(14)) * 8, 0, 8)));
            Aim(.2); Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(other));
            em.SetComponentData(other, LocalTransform.FromPosition(new float3(math.tan(math.radians(17)) * 8, 0, 8)));
            Aim(.22); Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(Entity.Null));
        }

        [Test]
        public void Player004_FallbackPrefersUsefulNearbyTargetOverDistantPerfectAlignment()
        {
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(0, 0, 21)));
            em.SetComponentData(other, LocalTransform.FromPosition(new float3(.12f, 0, 7)));
            Aim(0); Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(other));
        }

        [Test]
        public void Player004_LeadIsBoundedAndZeroDisablesPrediction()
        {
            float3 result = LaunchAimPrediction.Point(float3.zero, new float3(0, 0, 90), new float3(100, 10, 0), 90, .2f, 2);
            Assert.That(result, Is.EqualTo(new float3(2, 0, 90)));
            result = LaunchAimPrediction.Point(float3.zero, new float3(0, 0, 9), new float3(4, 10, 0), 90, .2f, 2);
            Assert.That(result.x, Is.InRange(.4f, .41f)); Assert.That(result.y, Is.Zero);
            Assert.That(LaunchAimPrediction.Point(float3.zero, new float3(0, 0, 9), math.right(), 90, 0, 2), Is.EqualTo(new float3(0, 0, 9)));
        }

        [Test]
        public void Player004_ClickConsumesDisplayedDirectionEvenWhenCurrentLockChanges()
        {
            em.SetComponentData(target, new PhysicsVelocity { Linear = new float3(4, 0, 0) });
            Aim(0);
            var presentation = world.GetOrCreateSystem<PresentationBridgeSystem>(); presentation.Update(world.Unmanaged);
            var shown = bridge.TrajectoryPreviewSegments[0];
            float3 expected = math.normalize(shown.End - shown.Start);
            Assert.That(expected.x, Is.GreaterThan(.05f), "The test must consume a genuine assisted direction.");
            uint revision = bridge.TrajectoryPreviewRevision;
            bridge.PublishPunch(Vector3.back, Vector3.forward, 1, 2, 90, .1f, 10, 0);
            var aim = em.GetComponentData<PunchAimAssistTarget>(source); aim.Target = other; em.SetComponentData(source, aim);
            presentation.Update(world.Unmanaged);
            Assert.That(bridge.TrajectoryPreviewRevision, Is.EqualTo(revision), "Pending click must preserve its snapshot.");
            world.GetOrCreateSystem<PlayerBridgeSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<PunchDetectionSystem>().Update(world.Unmanaged);
            Assert.That(math.distance(math.normalize(em.GetComponentData<ExternalImpulse>(source).Value), expected), Is.LessThan(.0001f));
            var launch = em.GetComponentData<EnemyLaunchState>(source);
            Assert.That(launch.HomingTarget, Is.EqualTo(target)); Assert.That(launch.HomingSecondsRemaining, Is.EqualTo(.25f));
        }

        [Test]
        public void Player004_InvalidDisplayedTargetAndPauseRevisionCannotReuseStaleAim()
        {
            Aim(0); world.GetOrCreateSystem<PresentationBridgeSystem>().Update(world.Unmanaged);
            uint revision = bridge.TrajectoryPreviewRevision;
            em.SetComponentData(target, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            Assert.That(PunchAimAssist.TryGetCommittedDirection(em, source, revision, out _, out _), Is.False);
            bridge.ClearPunchPreview();
            Assert.That(bridge.TrajectoryPreviewRevision, Is.Not.EqualTo(revision));
        }

        [Test]
        public void Combat012_HomingRespectsRemainingTimeAndPreservesSpeedAndVerticalVelocity()
        {
            em.SetComponentData(source, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, HomingTarget = target, HomingSecondsRemaining = .025f });
            em.SetComponentData(source, new PhysicsVelocity { Linear = new float3(0, 3, 10) });
            world.SetTime(new TimeData(.1, .1f));
            var homing = world.GetOrCreateSystem<EnemyLaunchHomingSystem>(); homing.Update(world.Unmanaged);
            var velocity = em.GetComponentData<PhysicsVelocity>(source).Linear;
            Assert.That(math.degrees(math.atan2(velocity.x, velocity.z)), Is.EqualTo(1.125f).Within(.001f));
            Assert.That(math.length(velocity.xz), Is.EqualTo(10).Within(.001f)); Assert.That(velocity.y, Is.EqualTo(3));
            Assert.That(em.GetComponentData<EnemyLaunchState>(source).HomingTarget, Is.EqualTo(Entity.Null));
            homing.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<PhysicsVelocity>(source).Linear, Is.EqualTo(velocity));
        }

        [TestCase(EnemyLaunchPhase.Launched)]
        [TestCase(EnemyLaunchPhase.Defeated)]
        public void Combat012_HomingClearsInvalidTarget(EnemyLaunchPhase phase)
        {
            em.SetComponentData(source, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, HomingTarget = target, HomingSecondsRemaining = .25f });
            em.SetComponentData(target, new EnemyLaunchState { Phase = phase });
            em.SetComponentData(source, new PhysicsVelocity { Linear = new float3(0, 0, 10) });
            world.SetTime(new TimeData(.02, .02f));
            world.GetOrCreateSystem<EnemyLaunchHomingSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyLaunchState>(source).HomingTarget, Is.EqualTo(Entity.Null));
        }

        [Test]
        public void Player004_ObstructionPreventsAcquisitionAndStopsHoming()
        {
            using var wallCollider = Unity.Physics.BoxCollider.Create(new BoxGeometry { Size = new float3(5, 5, .5f), Orientation = quaternion.identity });
            Entity wall = em.CreateEntity();
            physics.Dispose(); physics = new PhysicsWorld(1, 0, 0);
            var bodies = physics.Bodies;
            bodies[0] = new RigidBody { Entity = wall, Collider = wallCollider,
                WorldFromBody = new RigidTransform(quaternion.identity, new float3(0, 0, 4)), Scale = 1 };
            physics.CollisionWorld.BuildBroadphase(ref physics, .02f, float3.zero, true);
            em.SetComponentData(physicsEntity, new PhysicsWorldSingleton { PhysicsWorld = physics });
            Aim(0); Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(Entity.Null));
            em.SetComponentData(source, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, HomingTarget = target, HomingSecondsRemaining = .25f });
            em.SetComponentData(source, new PhysicsVelocity { Linear = new float3(0, 0, 10) });
            world.GetOrCreateSystem<EnemyLaunchHomingSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyLaunchState>(source).HomingTarget, Is.EqualTo(Entity.Null));
        }

        [TestCase(12f, true)]
        [TestCase(60f, false)]
        [TestCase(180f, false)]
        public void Combat012_ActualSolverImpactStopsSourceHomingAndBoundsNewChainDirection(float nextAngle, bool corrects)
        {
            using var collisionWorld = new World("COMBAT-012 first impact homing cutoff");
            var material = Unity.Physics.Material.Default;
            material.CollisionResponse = CollisionResponsePolicy.CollideRaiseCollisionEvents;
            using var collisionSphere = Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .5f }, CollisionFilter.Default, material);
            var types = new System.Collections.Generic.List<System.Type>();
            foreach (var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
                if (type.Namespace == "Unity.Physics.Systems") types.Add(type);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(collisionWorld, types);
            var manager = collisionWorld.EntityManager;
            var step = PhysicsStep.Default; step.Gravity = float3.zero;
            manager.AddComponentData(manager.CreateEntity(), step);
            manager.AddComponentData(manager.CreateEntity(), tuning);
            Entity incoming = Create(float3.zero), victim = Create(new float3(0, 0, .95f));
            Entity destination = manager.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(Health), typeof(EnemyLaunchState));
            manager.SetComponentData(destination, LocalTransform.FromPosition(new float3(
                math.sin(math.radians(nextAngle)) * 4, 0, .95f + math.cos(math.radians(nextAngle)) * 4)));
            manager.SetComponentData(destination, new Health { Current = 100, Max = 100 });
            manager.SetComponentData(incoming, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched,
                Owner = EnemyLaunchOwner.Player, HomingTarget = destination, HomingSecondsRemaining = 1 });
            manager.SetComponentData(incoming, new PhysicsVelocity { Linear = new float3(0, 0, 15) });
            var simulation = collisionWorld.GetOrCreateSystemManaged<Unity.Physics.Systems.PhysicsSystemGroup>();
            var contacts = collisionWorld.GetOrCreateSystem<EnemyLaunchCollisionSystem>();
            for (int i = 0; i < 3; i++)
            {
                collisionWorld.SetTime(new TimeData((i + 1) * .02, .02f)); simulation.Update();
                contacts.Update(collisionWorld.Unmanaged); manager.CompleteAllTrackedJobs();
            }
            Assert.That(manager.GetComponentData<EnemyLaunchState>(incoming).HomingTarget, Is.EqualTo(Entity.Null));
            Assert.That(manager.GetComponentData<EnemyLaunchState>(incoming).HomingSecondsRemaining, Is.Zero);
            Assert.That(manager.GetComponentData<EnemyLaunchState>(victim).Phase, Is.EqualTo(EnemyLaunchPhase.Launched));
            Assert.That(manager.GetComponentData<EnemyLaunchState>(victim).Owner, Is.EqualTo(EnemyLaunchOwner.Player));
            Assert.That(manager.GetComponentData<EnemyLaunchState>(victim).HomingTarget,
                Is.EqualTo(corrects ? destination : Entity.Null));
            if (corrects)
            {
                var velocity = manager.GetComponentData<PhysicsVelocity>(victim).Linear;
                Assert.That(math.degrees(math.atan2(velocity.x, velocity.z)), Is.InRange(0f, 20.001f));
            }

            Entity Create(float3 position)
            {
                Entity body = manager.CreateEntity(typeof(Enemy), typeof(EnemyTier), typeof(EnemyLaunchState), typeof(Health),
                    typeof(LocalTransform), typeof(LocalToWorld), typeof(PhysicsCollider), typeof(PhysicsMass),
                    typeof(PhysicsVelocity), typeof(PhysicsWorldIndex), typeof(Simulate));
                manager.SetComponentData(body, LocalTransform.FromPosition(position));
                manager.SetComponentData(body, new Health { Current = 100, Max = 100 });
                manager.SetComponentData(body, new PhysicsCollider { Value = collisionSphere });
                manager.SetComponentData(body, PhysicsMass.CreateDynamic(collisionSphere.Value.MassProperties, 1));
                return body;
            }
        }

        [TestCase(100)]
        [TestCase(1000)]
        public void Player004_ProfileStableAimAndMovingCollisionPreviewAtCrowdSize(int crowdSize)
        {
            for (int i = 0; i < 7; i++) Body(new float3(i < 3 ? i - 3 : i - 2, 0, 0));
            for (int i = 10; i < crowdSize; i++) Body(new float3(10 + i % 30 * 1.2f, 0, 4 + i / 30 * 1.2f));
            using var query = em.CreateEntityQuery(typeof(PhysicsCollider), typeof(LocalTransform));
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var poses = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            physics.Dispose(); physics = new PhysicsWorld(entities.Length, 0, 0);
            var bodies = physics.Bodies;
            for (int i = 0; i < entities.Length; i++)
                bodies[i] = new RigidBody { Entity = entities[i], Collider = sphere,
                    WorldFromBody = new RigidTransform(poses[i].Rotation, poses[i].Position), Scale = 1 };
            physics.CollisionWorld.BuildBroadphase(ref physics, .02f, float3.zero, true);
            em.SetComponentData(physicsEntity, new PhysicsWorldSingleton { PhysicsWorld = physics });
            bridge.ConfigureCollisionPreview(UnityEditor.AssetDatabase.LoadAssetAtPath<CrowdPunch.Configuration.PlayerPunchSettings>(
                "Assets/CrowdPunch/Data/Settings/PlayerPunchSettings.asset").CollisionPreview);
            bridge.PublishPunchPreview(Vector3.back, Vector3.forward, 5, 2, 5, 0, 22, 12, 3, 18, 90, 16, .12f, .35f);
            var aim = world.GetOrCreateSystem<PunchAimAssistSystem>();
            var presentation = world.GetOrCreateSystem<PresentationBridgeSystem>();
            for (int i = 0; i < 5; i++) { aim.Update(world.Unmanaged); presentation.Update(world.Unmanaged); }
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                world.SetTime(new TimeData(i * .02, .02f));
                aim.Update(world.Unmanaged); presentation.Update(world.Unmanaged);
            }
            timer.Stop();
            Assert.That(bridge.TrajectoryPreviewSegments.Count, Is.GreaterThanOrEqualTo(8));
            TestContext.WriteLine($"{crowdSize} bodies / eight sources, aim plus preview: {timer.Elapsed.TotalMilliseconds / 100:F3} ms/update (Editor, no simulation/rendering).");
        }
    }
}
