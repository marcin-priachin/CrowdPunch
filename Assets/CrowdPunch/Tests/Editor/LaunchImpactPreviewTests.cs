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
    public sealed class LaunchImpactPreviewTests
    {
        private World world;
        private EntityManager em;
        private BlobAssetReference<Unity.Physics.Collider> sphere;
        private Entity source, target;
        private EnemyLaunchSettings settings;

        [SetUp]
        public void SetUp()
        {
            world = new World("PLAYER-003 collision preview");
            em = world.EntityManager;
            sphere = Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .5f });
            source = Body(float3.zero);
            target = Body(new float3(0, 0, 6));
            settings = new EnemyLaunchSettings { MinimumPropagationImpulse = 1 };
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

        private bool Predict(PhysicsWorld physics, out float3 direction, Entity extraCandidate = default,
            bool targetPunched = false, float distance = 18, float strength = 12)
        {
            var initial = new LaunchPreviewSource { Entity = source, Direction = math.forward() };
            using var punched = new NativeArray<LaunchPreviewSource>(targetPunched
                ? new[] { initial, new LaunchPreviewSource { Entity = target, Direction = math.forward() } }
                : new[] { initial }, Allocator.Temp);
            using var candidates = new NativeArray<Entity>(new[] { source, target, extraCandidate }, Allocator.Temp);
            using var poses = new NativeArray<LocalTransform>(new[] {
                em.GetComponentData<LocalTransform>(source), em.GetComponentData<LocalTransform>(target),
                extraCandidate == Entity.Null ? LocalTransform.Identity : em.GetComponentData<LocalTransform>(extraCandidate) }, Allocator.Temp);
            using var hits = new NativeList<ColliderCastHit>(Allocator.Temp);
            bool result = LaunchImpactPrediction.TryPredict(em, physics.CollisionWorld, punched[0], punched,
                candidates, poses, distance, strength, settings, 0, hits, out Entity hit, out direction);
            if (result) Assert.That(hit, Is.EqualTo(target));
            return result;
        }

        [Test]
        public void Player003_FirstImpactStopsAtNearestBodyAndDoesNotMutateSimulation()
        {
            Entity farther = Body(new float3(0, 0, 10));
            using var physics = Physics(source, target, farther);
            Assert.That(Predict(physics, out var direction), Is.True);
            Assert.That(math.distance(direction, math.forward()), Is.LessThan(.001f));
            Assert.That(em.GetComponentData<EnemyLaunchState>(target).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
            Assert.That(em.GetComponentData<PhysicsVelocity>(target).Linear, Is.EqualTo(float3.zero));
        }

        [Test]
        public void Player003_OffCentreCollisionShowsTransferredDirectionRatherThanIncomingDirection()
        {
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(.65f, 0, 6)));
            using var physics = Physics(source, target);
            Assert.That(Predict(physics, out var direction), Is.True);
            Assert.That(direction.x, Is.GreaterThan(.5f));
            Assert.That(direction.z, Is.GreaterThan(.5f));
        }

        [Test]
        public void Player003_CollisionDirectionUsesPropagationAimCorrection()
        {
            Entity next = Body(new float3(2, 0, 10));
            settings.PropagationAimCorrectionRadius = 10;
            settings.PropagationAimMaximumAngleDegrees = 180;
            using var physics = Physics(source, target, next);
            Assert.That(Predict(physics, out var direction, next), Is.True);
            Assert.That(math.distance(direction, math.normalize(new float3(2, 0, 4))), Is.LessThan(.001f));
            settings.PropagationAimCorrectionRadius = 0;
            Assert.That(Predict(physics, out direction, next), Is.True);
            Assert.That(math.distance(direction, math.forward()), Is.LessThan(.001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Player003_WallsBlockPredictionButTriggersDoNot(bool trigger)
        {
            Entity wall = em.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider));
            em.SetComponentData(wall, LocalTransform.FromPosition(new float3(0, 0, 3)));
            var material = Unity.Physics.Material.Default;
            material.CollisionResponse = trigger ? CollisionResponsePolicy.RaiseTriggerEvents : CollisionResponsePolicy.Collide;
            using var wallCollider = Unity.Physics.BoxCollider.Create(new BoxGeometry {
                Size = new float3(4, 4, .3f), Orientation = quaternion.identity }, CollisionFilter.Default, material);
            em.SetComponentData(wall, new PhysicsCollider { Value = wallCollider });
            using var physics = Physics(source, target, wall);
            Assert.That(Predict(physics, out _), Is.EqualTo(trigger));
        }

        [TestCase(0f, 30f, false)]
        [TestCase(2f, -30f, true)]
        public void Player003_CollisionPredictionAccountsForBodiesMovingAcrossFlight(float x, float lateralSpeed, bool hit)
        {
            em.SetComponentData(target, LocalTransform.FromPosition(new float3(x, 0, 6)));
            em.SetComponentData(target, new PhysicsVelocity { Linear = new float3(lateralSpeed, 0, 0) });
            settings.AimLeadSeconds = .2f; settings.AimLeadMaximumDistance = 2;
            using var physics = Physics(source, target);
            Assert.That(Predict(physics, out _, strength: 90), Is.EqualTo(hit));
        }

        [Test]
        public void Player003_PropagationCorrectionNeverSelectsBehindItsConfiguredCone()
        {
            Entity next = Body(new float3(4, 0, 7));
            settings.PropagationAimCorrectionRadius = 6; settings.PropagationAimMaximumAngleDegrees = 20;
            using var physics = Physics(source, target, next);
            Assert.That(Predict(physics, out var direction, next), Is.True);
            Assert.That(math.distance(direction, math.forward()), Is.LessThan(.001f));
        }

        [TestCase(EnemyLaunchPhase.Launched, false)]
        [TestCase(EnemyLaunchPhase.Defeated, false)]
        [TestCase(EnemyLaunchPhase.Recovering, true)]
        public void Player003_CollisionPreviewUsesPropagationLifecycle(EnemyLaunchPhase phase, bool visible)
        {
            em.SetComponentData(target, new EnemyLaunchState { Phase = phase });
            using var physics = Physics(source, target);
            Assert.That(Predict(physics, out _), Is.EqualTo(visible));
        }

        [Test]
        public void Player003_ProtectedArmorAndInvulnerabilityDoNotAdvertiseLaunches()
        {
            em.AddComponentData(target, new EnemyArmor { Stages = 2 });
            using var physics = Physics(source, target);
            Assert.That(Predict(physics, out _), Is.False);
            em.SetComponentData(target, new EnemyArmor { ProtectedUntil = 2 });
            Assert.That(Predict(physics, out _), Is.False);
            em.SetComponentData(target, new EnemyArmor());
            Assert.That(Predict(physics, out _), Is.True);
        }

        [Test]
        public void Player003_ThresholdDistanceAndSimultaneousPunchesGateCollisionArrows()
        {
            using var physics = Physics(source, target);
            Assert.That(Predict(physics, out _, targetPunched: true), Is.False);
            Assert.That(Predict(physics, out _, distance: 4), Is.False);
            Assert.That(Predict(physics, out _, strength: 0), Is.False);
            settings.MinimumPropagationImpulse = 7;
            Assert.That(Predict(physics, out _), Is.False);
            // Punch strength is a velocity change, not impulse divided by mass.
            em.SetComponentData(source, new PhysicsMass { InverseMass = .5f });
            Assert.That(Predict(physics, out _), Is.True);
        }

        [TestCase(0f)]
        [TestCase(5f)]
        public void Player003_CollisionArrowLengthIsIndependentOfDirectArrowLength(float directLength)
        {
            using var physics = Physics(source, target);
            em.AddComponentData(em.CreateEntity(), new PlayerSnapshot());
            em.AddComponentData(em.CreateEntity(), settings);
            em.AddComponentData(em.CreateEntity(), new PhysicsWorldSingleton { PhysicsWorld = physics });
            PlayerBridgeRegistry.TryGetBridge(out var previous);
            var root = new GameObject("Collision arrow regression");
            var bridge = root.AddComponent<PlayerEcsBridge>();
            try
            {
                PlayerBridgeRegistry.Register(bridge);
                bridge.PublishPunchPreview(Vector3.back, Vector3.forward, 2, 2, directLength, 0, 0, 0, 2.5f, 18, 12);
                var system = world.GetOrCreateSystem<PresentationBridgeSystem>();
                system.Update(world.Unmanaged);
                Assert.That(bridge.TrajectoryPreviewSegments.Count, Is.EqualTo(directLength > 0 ? 2 : 1));
                var segment = bridge.TrajectoryPreviewSegments[bridge.TrajectoryPreviewSegments.Count - 1];
                Assert.That(segment.IsCollision, Is.True);
                Assert.That(segment.Start, Is.EqualTo(new float3(0, 0, 6)));
                Assert.That(math.distance(segment.Start, segment.End), Is.EqualTo(2.5f).Within(.001f));
                bridge.ClearPunchPreview();
                system.Update(world.Unmanaged);
                Assert.That(bridge.TrajectoryPreviewSegments.Count, Is.Zero);
            }
            finally
            {
                PlayerBridgeRegistry.Unregister(bridge);
                if (previous != null) PlayerBridgeRegistry.Register(previous);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Player003_ArrowStylesExposeIndependentWidthsColoursAndAlpha()
        {
            var asset = ScriptableObject.CreateInstance<PlayerPunchSettings>();
            try
            {
                var serialized = new UnityEditor.SerializedObject(asset);
                serialized.FindProperty("launchArrowWidth").floatValue = .6f;
                serialized.FindProperty("collisionArrowWidth").floatValue = .2f;
                serialized.FindProperty("launchArrowColor").colorValue = new Color(1, 0, 0, .2f);
                serialized.FindProperty("collisionArrowColor").colorValue = new Color(0, 1, 0, .8f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(asset.LaunchArrowWidth, Is.EqualTo(.6f));
                Assert.That(asset.CollisionArrowWidth, Is.EqualTo(.2f));
                Assert.That(asset.LaunchArrowColor, Is.EqualTo(new Color(1, 0, 0, .2f)));
                Assert.That(asset.CollisionArrowColor, Is.EqualTo(new Color(0, 1, 0, .8f)));
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [TestCase(100)]
        [TestCase(1000)]
        public void Player003_CrowdPreviewDeduplicatesTargetsAndMeasuresPresentationCost(int crowdSize)
        {
            var entities = new Entity[crowdSize];
            entities[0] = source; entities[1] = target;
            em.SetComponentData(source, LocalTransform.FromPosition(new float3(-7, 0, 0)));
            // Eight simultaneously punched sources converge on one first-impact target.
            for (int i = 2; i < 9; i++)
                entities[i] = Body(new float3(i * 2 - 9, 0, 0));
            for (int i = 9; i < crowdSize; i++)
                entities[i] = Body(new float3(10 + i % 30 * 1.2f, 0, 4 + i / 30 * 1.2f));
            for (int i = 0; i < 9; i++)
            {
                if (i == 1) continue;
                em.AddComponentData(entities[i], new PunchAimAssistTarget { Target = target, IsAiming = 1 });
            }
            using var physics = Physics(entities);
            em.AddComponentData(em.CreateEntity(), new PlayerSnapshot());
            settings.PropagationAimCorrectionRadius = 12;
            settings.PropagationAimMaximumAngleDegrees = 20;
            settings.AimLeadSeconds = .2f;
            settings.AimLeadMaximumDistance = 2;
            em.AddComponentData(em.CreateEntity(), settings);
            em.AddComponentData(em.CreateEntity(), new PhysicsWorldSingleton { PhysicsWorld = physics });
            PlayerBridgeRegistry.TryGetBridge(out var previous);
            var root = new GameObject("Crowd collision preview benchmark");
            var bridge = root.AddComponent<PlayerEcsBridge>();
            try
            {
                PlayerBridgeRegistry.Register(bridge);
                bridge.PublishPunchPreview(Vector3.back, Vector3.forward, 8, 2, 5, 0, 18, 12, 3, 18, 90);
                var system = world.GetOrCreateSystem<PresentationBridgeSystem>();
                for (int i = 0; i < 5; i++) system.Update(world.Unmanaged);
                var timer = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 100; i++) system.Update(world.Unmanaged);
                timer.Stop();
                int collisionCount = 0;
                foreach (var segment in bridge.TrajectoryPreviewSegments)
                    if (segment.IsCollision) collisionCount++;
                Assert.That(collisionCount, Is.EqualTo(1));
                TestContext.WriteLine($"{crowdSize} bodies, eight sources: {timer.Elapsed.TotalMilliseconds / 100:F3} ms/update (Editor, no rendering/physics step).");
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
