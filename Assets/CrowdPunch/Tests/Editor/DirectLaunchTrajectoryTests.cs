using System.Linq;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class DirectLaunchTrajectoryTests
    {
        private World world;
        private EntityManager em;
        private BlobAssetReference<Unity.Physics.Collider> sphere;
        private EnemyLaunchSettings settings;
        private Entity source;

        [SetUp]
        public void SetUp()
        {
            world = new World("PLAYER-003 dotted direct trajectories");
            em = world.EntityManager;
            sphere = Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .5f });
            source = Body(float3.zero);
            settings = new EnemyLaunchSettings { UsefulMomentumSpeed = 1, LowMomentumPeriod = .2f };
        }

        [TearDown]
        public void TearDown() { world.Dispose(); sphere.Dispose(); }

        private Entity Body(float3 position)
        {
            Entity entity = em.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(EnemyTier),
                typeof(EnemyLaunchState), typeof(Health), typeof(PhysicsMass), typeof(PhysicsVelocity), typeof(PhysicsCollider));
            em.SetComponentData(entity, LocalTransform.FromPosition(position));
            em.SetComponentData(entity, new Health { Current = 100, Max = 100 });
            em.SetComponentData(entity, new PhysicsMass { InverseMass = 1 });
            em.SetComponentData(entity, new PhysicsCollider { Value = sphere });
            return entity;
        }

        private PhysicsWorld Physics(params Entity[] entities)
        {
            var physics = new PhysicsWorld(entities.Length, 0, 0);
            var bodies = physics.Bodies;
            for (int i = 0; i < entities.Length; i++)
            {
                var pose = em.GetComponentData<LocalTransform>(entities[i]);
                bodies[i] = new RigidBody { Entity = entities[i],
                    Collider = em.GetComponentData<PhysicsCollider>(entities[i]).Value,
                    WorldFromBody = new RigidTransform(pose.Rotation, pose.Position), Scale = pose.Scale };
            }
            physics.CollisionWorld.BuildBroadphase(ref physics, .02f, float3.zero, true);
            return physics;
        }

        private float3[] Trace(PhysicsWorld physics, float speed = 12, float seconds = 8, int steps = 160,
            bool bothPunched = false, Entity other = default)
        {
            var first = new LaunchPreviewSource { Entity = source, Direction = math.forward(), Speed = speed };
            using var punched = new NativeArray<LaunchPreviewSource>(bothPunched
                ? new[] { first, new LaunchPreviewSource { Entity = other, Direction = math.forward(), Speed = speed } }
                : new[] { first }, Allocator.Temp);
            using var points = new NativeList<float3>(Allocator.Temp);
            DirectLaunchTrajectoryPrediction.Trace(em, physics.CollisionWorld, first, punched, settings, seconds, steps, points);
            return points.AsArray().ToArray();
        }

        [Test]
        [TestCase("enemy")]
        [TestCase("barricade")]
        [TestCase("track")]
        [TestCase("pillar")]
        public void Player003_ImpactAccentMarksActualEnemyAndObjectiveContact(string kind)
        {
            Entity target = Body(new float3(0, 0, 6));
            if (kind != "enemy")
            {
                em.RemoveComponent<Enemy>(target);
                em.SetComponentData(target, new PhysicsMass());
                if (kind == "barricade") em.AddComponentData(target, new Barricade { HitsRemaining = 1 });
                if (kind == "track") em.AddComponent<TrackObject>(target);
                if (kind == "pillar") em.AddComponentData(target, new FallingPillar { Phase = PillarPhase.Upright });
            }
            using var physics = Physics(source, target);
            var impacts = TraceImpacts(physics);
            Assert.That(impacts.Length, Is.EqualTo(1));
            Assert.That(impacts[0].Position.z, Is.EqualTo(5).Within(.02f), "Accent belongs on the source's trajectory at contact.");
            Assert.That(impacts[0].Direction, Is.EqualTo(math.forward()));
            Assert.That(em.GetComponentData<LocalTransform>(target).Position.z, Is.EqualTo(6));
        }

        [Test]
        public void Player003_OrdinarySceneryBlocksAccentsForEnemiesBehindIt()
        {
            Entity wall = Body(new float3(0, 0, 3));
            em.RemoveComponent<Enemy>(wall);
            em.SetComponentData(wall, new PhysicsMass());
            Entity target = Body(new float3(0, 0, 6));
            using var physics = Physics(source, wall, target);
            Assert.That(TraceImpacts(physics), Is.Empty);
        }

        [Test]
        public void Player003_TriggersAndBrokenObjectivesHaveNoImpactAccent()
        {
            var material = Unity.Physics.Material.Default;
            material.CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents;
            using var trigger = Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .5f }, CollisionFilter.Default, material);
            Entity zone = Body(new float3(0, 0, 3));
            em.SetComponentData(zone, new PhysicsCollider { Value = trigger });
            Entity broken = Body(new float3(0, 0, 6));
            em.RemoveComponent<Enemy>(broken);
            em.AddComponentData(broken, new Barricade { HitsRemaining = 0 });
            using var physics = Physics(source, zone, broken);
            Assert.That(TraceImpacts(physics), Is.Empty);
        }

        private LaunchTrajectoryImpact[] TraceImpacts(PhysicsWorld physics)
        {
            var initial = new LaunchPreviewSource { Entity = source, Direction = math.forward(), Speed = 12 };
            using var punched = new NativeArray<LaunchPreviewSource>(new[] { initial }, Allocator.Temp);
            using var points = new NativeList<float3>(Allocator.Temp);
            using var impacts = new NativeList<LaunchTrajectoryImpact>(Allocator.Temp);
            DirectLaunchTrajectoryPrediction.Trace(em, physics.CollisionWorld, initial, punched, settings, 1, 40, points, impacts: impacts);
            return impacts.AsArray().ToArray();
        }

        [Test]
        public void Player003_PathEndsAtAuthoredDefeatBounds()
        {
            using var physics = Physics(source);
            var initial = new LaunchPreviewSource { Entity = source, Direction = math.forward(), Speed = 90 };
            using var punched = new NativeArray<LaunchPreviewSource>(new[] { initial }, Allocator.Temp);
            using var points = new NativeList<float3>(Allocator.Temp);
            DirectLaunchTrajectoryPrediction.Trace(em, physics.CollisionWorld, initial, punched, settings, 8, 160, points,
                defeatBounds: new EnemyDefeatBounds { Extents = new float3(10, 10, 10) });
            Assert.That(points[points.Length - 1].z, Is.EqualTo(10).Within(.001f));
        }

        [TestCase(100)]
        [TestCase(1000)]
        public void Player003_MeasuresWholePathCostAcrossRepresentativeCrowds(int crowd)
        {
            var bodies = new Entity[crowd + 8];
            for (int i = 0; i < 8; i++) bodies[i] = i == 0 ? source : Body(new float3(i * 2, 0, 0));
            for (int i = 8; i < bodies.Length; i++)
                bodies[i] = Body(new float3((i % 32) * 2, 0, 10 + (i / 32) * 2));
            using var physics = Physics(bodies);
            using var punched = new NativeArray<LaunchPreviewSource>(8, Allocator.Temp);
            var shots = punched;
            for (int i = 0; i < 8; i++)
                shots[i] = new LaunchPreviewSource { Entity = bodies[i], Direction = math.forward(), Speed = 90 };
            using var points = new NativeList<float3>(Allocator.Temp);
            settings.UsefulMomentumSpeed = 15;
            void Frame()
            {
                foreach (var initial in punched)
                {
                    DirectLaunchTrajectoryPrediction.Trace(em, physics.CollisionWorld, initial, punched, settings, 8, 160, points);
                    Assert.That(points.Length, Is.GreaterThan(1));
                }
            }
            Frame();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10; i++) Frame();
            timer.Stop();
            TestContext.WriteLine($"Dotted trajectory: {crowd} crowd / 8 punched, {timer.Elapsed.TotalMilliseconds / 10:F3} ms per preview frame (Editor, warm).");
        }

        [Test]
        public void Player003_WholeFlightContinuesBeyondArrowLengthAndStopsAtRecovery()
        {
            em.AddComponentData(source, new PhysicsDamping { Linear = .5f });
            using var physics = Physics(source);
            var points = Trace(physics);
            Assert.That(points.Last().z, Is.GreaterThan(20));
            Assert.That(points.Last().z, Is.LessThan(24));
            Assert.That(points.Length, Is.LessThan(161), "Recovery must end the path before the time cap.");
            Assert.That(points.All(p => math.all(math.isfinite(p))), Is.True);
        }

        [Test]
        public void Player003_SweptColliderStopsAtSolidWallEvenAtHighLaunchSpeed()
        {
            Entity wall = Body(new float3(0, 0, 6));
            em.RemoveComponent<Enemy>(wall);
            em.SetComponentData(wall, new PhysicsMass());
            using var physics = Physics(source, wall);
            var points = Trace(physics, 90);
            Assert.That(points.Max(p => p.z), Is.EqualTo(5).Within(.02f));
            Assert.That(points.Last().z, Is.EqualTo(5).Within(.02f));
        }

        [Test]
        public void Player003_BouncySurfaceContinuesTheSameBodyPathAfterImpact()
        {
            var material = Unity.Physics.Material.Default;
            material.Restitution = 1;
            material.RestitutionCombinePolicy = Unity.Physics.Material.CombinePolicy.Maximum;
            using var bounce = Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .5f }, CollisionFilter.Default, material);
            Entity wall = Body(new float3(0, 0, 6));
            em.RemoveComponent<Enemy>(wall);
            em.SetComponentData(wall, new PhysicsMass());
            em.SetComponentData(wall, new PhysicsCollider { Value = bounce });
            using var physics = Physics(source, wall);
            var points = Trace(physics, seconds: 2, steps: 40);
            Assert.That(points.Max(p => p.z), Is.EqualTo(5).Within(.02f));
            Assert.That(points.Last().z, Is.LessThan(0));
        }

        [Test]
        public void Player003_CrowdImpactReducesSourceMomentumWithoutForecastingVictimPath()
        {
            Entity target = Body(new float3(0, 0, 6));
            using var physics = Physics(source, target);
            var points = Trace(physics, seconds: 1, steps: 40);
            Assert.That(points.Last().z, Is.EqualTo(8.5f).Within(.1f));
            Assert.That(em.GetComponentData<PhysicsVelocity>(target).Linear, Is.EqualTo(float3.zero));
            Assert.That(em.GetComponentData<EnemyLaunchState>(target).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
            Assert.That(em.GetComponentData<LocalTransform>(source).Position, Is.EqualTo(float3.zero));
        }

        [Test]
        public void Player003_CopunchedBodiesDepartTogetherInsteadOfBlockingEachOther()
        {
            Entity other = Body(new float3(0, 0, 3));
            using var physics = Physics(source, other);
            var points = Trace(physics, seconds: 1, bothPunched: true, other: other);
            Assert.That(points.Last().z, Is.EqualTo(12).Within(.02f));
        }

        [Test]
        public void Player003_TriggersDoNotTruncateTheFlight()
        {
            var material = Unity.Physics.Material.Default;
            material.CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents;
            using var trigger = Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .5f }, CollisionFilter.Default, material);
            Entity zone = Body(new float3(0, 0, 3));
            em.RemoveComponent<Enemy>(zone);
            em.SetComponentData(zone, new PhysicsCollider { Value = trigger });
            using var physics = Physics(source, zone);
            Assert.That(Trace(physics, seconds: 1).Last().z, Is.EqualTo(12).Within(.02f));
        }

        [Test]
        public void Player003_HomingAndGroundSnapAreEstimatedWithoutMutatingLiveState()
        {
            Entity target = Body(new float3(2, 0, 8));
            em.AddComponentData(source, new PunchAimAssistTarget { Target = target });
            em.AddComponentData(source, new EnemyGroundConstraint { HasGround = 1, Height = 0 });
            settings.LaunchHomingDuration = .25f;
            settings.LaunchHomingDegreesPerSecond = 120;
            settings.LaunchHomingMaximumAngleDegrees = 45;
            using var physics = Physics(source, target);
            var points = Trace(physics, seconds: .5f, steps: 40);
            Assert.That(points.Last().x, Is.GreaterThan(.1f));
            Assert.That(points.All(p => math.abs(p.y) < .001f), Is.True);
            Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).Target, Is.EqualTo(target));
            Assert.That(em.GetComponentData<EnemyGroundConstraint>(source).IsLocked, Is.Zero);
        }

        [Test]
        public void Player003_DottedModeOnlyPublishesDirectEnemiesAndSwitchesBackToArrows()
        {
            Entity victim = Body(new float3(0, 0, 6));
            Entity protectedEnemy = Body(new float3(.8f, 0, 0));
            em.AddComponentData(protectedEnemy, new EnemyArmor { Stages = 2 });
            em.AddComponentData(source, new PunchAimAssistTarget());
            em.AddComponentData(source, new EnemyGroundConstraint { HasGround = 1 });
            em.AddComponentData(em.CreateEntity(), new PlayerSnapshot());
            em.AddComponentData(em.CreateEntity(), settings);
            using var physics = Physics(source, victim, protectedEnemy);
            em.AddComponentData(em.CreateEntity(), new PhysicsWorldSingleton { PhysicsWorld = physics });
            PlayerBridgeRegistry.TryGetBridge(out var previous);
            var root = new GameObject("Dotted preview integration");
            var bridge = root.AddComponent<PlayerEcsBridge>();
            try
            {
                PlayerBridgeRegistry.Register(bridge);
                bridge.PublishPunchPreview(Vector3.back, Vector3.forward, 2, 2, 0, 0, 0, 0,
                    previewStrength: 12, useDottedTrajectory: true, trajectoryMaximumSeconds: 1);
                var system = world.GetOrCreateSystem<PresentationBridgeSystem>();
                system.Update(world.Unmanaged);
                Assert.That(bridge.TrajectoryPreviewSegments.Count, Is.GreaterThan(1));
                Assert.That(bridge.TrajectoryPreviewSegments.All(s => s.IsDotted && !s.IsCollision), Is.True);
                Assert.That(bridge.TrajectoryPreviewSegments.Count(s => s.StartsPath), Is.EqualTo(1));
                Assert.That(bridge.TrajectoryPreviewImpacts.Count, Is.EqualTo(1));
                Assert.That(bridge.TrajectoryPreviewImpacts[0].Position.z, Is.EqualTo(5).Within(.02f));
                Assert.That(em.GetComponentData<PunchAimAssistTarget>(source).PreviewRevision, Is.EqualTo(bridge.TrajectoryPreviewRevision));
                bridge.PublishPunchPreview(Vector3.back, Vector3.forward, 2, 2, 5, 0, 0, 0);
                system.Update(world.Unmanaged);
                Assert.That(bridge.TrajectoryPreviewSegments.Count, Is.EqualTo(1));
                Assert.That(bridge.TrajectoryPreviewSegments[0].IsDotted, Is.False);
                Assert.That(bridge.TrajectoryPreviewImpacts.Count, Is.Zero);
                bridge.ClearPunchPreview();
                system.Update(world.Unmanaged);
                Assert.That(bridge.TrajectoryPreviewSegments.Count, Is.Zero);
                Assert.That(bridge.TrajectoryPreviewImpacts.Count, Is.Zero);
            }
            finally
            {
                PlayerBridgeRegistry.Unregister(bridge);
                if (previous != null) PlayerBridgeRegistry.Register(previous);
                Object.DestroyImmediate(root);
            }
        }
    }
}
