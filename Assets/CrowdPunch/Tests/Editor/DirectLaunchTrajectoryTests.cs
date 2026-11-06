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

        private float3[] Trace(PhysicsWorld physics, float speed = 12, float seconds = 8, int steps = 400,
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

        [TestCase(false)]
        [TestCase(true)]
        public void Player003_FirstContactClipsOnlyPublishedGeometry(bool clip)
        {
            Entity target = Body(new float3(0, 0, 6));
            using var physics = Physics(source, target);
            var initial = new LaunchPreviewSource { Entity = source, Direction = math.forward(), Speed = 12 };
            using var punched = new NativeArray<LaunchPreviewSource>(new[] { initial }, Allocator.Temp);
            using var points = new NativeList<float3>(Allocator.Temp);
            using var impacts = new NativeList<LaunchTrajectoryImpact>(Allocator.Temp);
            using var contact = new NativeList<LaunchTrajectoryContact>(Allocator.Temp);
            using var path = new NativeList<LaunchPathPoint>(Allocator.Temp);
            DirectLaunchTrajectoryPrediction.Trace(em, physics.CollisionWorld, initial, punched, settings,
                2, 160, points, impacts: impacts, path: path, firstContact: contact);
            Assert.That(contact.Length, Is.EqualTo(1));
            Assert.That(contact[0].Position.z, Is.EqualTo(5).Within(.02f));
            Assert.That(points.Length - 1, Is.GreaterThan(contact[0].EndPointIndex));
            int savedPathLength = path.Length;
            var root = new GameObject("First contact preview");
            try
            {
                var bridge = root.AddComponent<PlayerEcsBridge>();
                bridge.PublishPunchPreview(Vector3.zero, Vector3.forward, 2, 3, 0, 0, 0, 0,
                    useDottedTrajectory: true, trajectoryRenderToFirstContact: clip);
                PresentationBridgeSystem.PublishDottedTrajectory(bridge, points, impacts, contact);
                var segments = bridge.TrajectoryPreviewSegments;
                Assert.That(segments.Count, Is.EqualTo(clip ? contact[0].EndPointIndex : points.Length - 1));
                Assert.That(segments[segments.Count - 1].End,
                    Is.EqualTo(clip ? contact[0].Position : points[points.Length - 1]));
                Assert.That(path.Length, Is.EqualTo(savedPathLength), "Rendering never truncates the authoritative path.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Player003_FirstContactIncludesSceneryWithoutAccent()
        {
            Entity wall = Body(new float3(0, 0, 3));
            em.RemoveComponent<Enemy>(wall);
            em.SetComponentData(wall, new PhysicsMass());
            using var physics = Physics(source, wall);
            var initial = new LaunchPreviewSource { Entity = source, Direction = math.forward(), Speed = 12 };
            using var punched = new NativeArray<LaunchPreviewSource>(new[] { initial }, Allocator.Temp);
            using var points = new NativeList<float3>(Allocator.Temp);
            using var impacts = new NativeList<LaunchTrajectoryImpact>(Allocator.Temp);
            using var contact = new NativeList<LaunchTrajectoryContact>(Allocator.Temp);
            DirectLaunchTrajectoryPrediction.Trace(em, physics.CollisionWorld, initial, punched, settings,
                1, 80, points, impacts: impacts, firstContact: contact);
            Assert.That(contact.Length, Is.EqualTo(1));
            Assert.That(contact[0].Position.z, Is.EqualTo(2).Within(.02f));
            Assert.That(impacts.Length, Is.Zero);
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

        [TestCase(3, false, false, 0)]
        [TestCase(3, true, false, 1)]
        [TestCase(3, true, true, 0)]
        [TestCase(0, false, false, 1)]
        [TestCase(0, true, false, 1)]
        public void Player003_ShellAccentsRequireAnEffectiveSource(int shellHits, bool explosive, bool spent, int accents)
        {
            // SHELL-002/003: an intact shell needs a fresh blast; exposed cores accept ordinary bodies.
            Entity target = Body(new float3(0, 0, 6));
            em.RemoveComponent<Enemy>(target);
            em.SetComponentData(target, new PhysicsMass());
            em.AddComponentData(target, new Barricade { HitsRemaining = 1 });
            em.AddComponentData(target, new ShellTarget { ExplosionsRemaining = shellHits, CoreHealth = 100 });
            if (explosive) em.AddComponentData(source, new ExplosiveEnemyState { HasExploded = spent ? (byte)1 : (byte)0 });
            using var physics = Physics(source, target);
            Assert.That(TraceImpacts(physics).Length, Is.EqualTo(accents));
            AssertContactStillBlocks(physics);
            Assert.That(em.GetComponentData<ShellTarget>(target).ExplosionsRemaining, Is.EqualTo(shellHits));
        }

        [TestCase(RollingPhase.Pause, false, 1)]
        [TestCase(RollingPhase.WindUp, false, 1)]
        [TestCase(RollingPhase.Roll, false, 0)]
        [TestCase(RollingPhase.Pause, true, 0)]
        [TestCase(RollingPhase.WindUp, true, 0)]
        [TestCase(RollingPhase.Defeated, false, 0)]
        public void Player003_RollingAccentsFollowDamageProtection(RollingPhase phase, bool protectedHit, int accents)
        {
            // ROLL-001: both the committed roll and accepted-hit protection reject damage.
            Entity target = Body(new float3(0, 0, 6));
            em.RemoveComponent<Enemy>(target);
            em.SetComponentData(target, new PhysicsMass());
            em.AddComponentData(target, new RollingBoss { Phase = phase, InvulnerableUntil = protectedHit ? 10 : 0 });
            using var physics = Physics(source, target);
            Assert.That(TraceImpacts(physics).Length, Is.EqualTo(accents));
            AssertContactStillBlocks(physics);
            if (protectedHit)
            {
                world.SetTime(new Unity.Core.TimeData(10, .02f));
                Assert.That(TraceImpacts(physics).Length, Is.EqualTo(1), "Accents return when hit protection expires.");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Player003_DinoHasNoBodyHitAccent(bool explosive)
        {
            // PILLAR-001: only falling pillars affect Dino, including when the body is explosive.
            Entity target = Body(new float3(0, 0, 6));
            em.RemoveComponent<Enemy>(target);
            em.SetComponentData(target, new PhysicsMass());
            em.AddComponent<DinoBoss>(target);
            if (explosive) em.AddComponent<ExplosiveEnemyState>(source);
            using var physics = Physics(source, target);
            Assert.That(TraceImpacts(physics), Is.Empty);
            AssertContactStillBlocks(physics);
        }

        [TestCase("inner", 0)]
        [TestCase("tip", 1)]
        [TestCase("protectedHead", 0)]
        [TestCase("exposedHead", 1)]
        [TestCase("detached", 1)]
        public void Player003_SnakeAccentsFollowExposedPart(string part, int accents)
        {
            // SNAKE-002/004: peeling the tip transfers eligibility; detached foreign orbs remain eligible.
            Entity target = Body(new float3(0, 0, 6));
            Entity head = em.CreateEntity();
            em.AddComponentData(head, new SnakeHead { AttachedCount = 3, Generation = 1 });
            if (part == "protectedHead" || part == "exposedHead")
                em.AddComponentData(target, new SnakeHead { AttachedCount = part == "protectedHead" ? 3 : 0 });
            else em.AddComponentData(target, new SnakeSegment { Head = head, Generation = 1,
                Index = part == "tip" ? 2 : 0, Detached = part == "detached" ? (byte)1 : (byte)0 });
            using var physics = Physics(source, target);
            Assert.That(TraceImpacts(physics).Length, Is.EqualTo(accents));
            AssertContactStillBlocks(physics);
            if (part == "inner")
            {
                em.SetComponentData(head, new SnakeHead { AttachedCount = 1, Generation = 1 });
                Assert.That(TraceImpacts(physics).Length, Is.EqualTo(1), "The new tip must regain its accent.");
            }
        }

        private void AssertContactStillBlocks(PhysicsWorld physics)
        {
            Assert.That(Trace(physics, seconds: 1, steps: 40).Any(p => math.abs(p.z - 5) < .02f), Is.True,
                "Suppressing an accent must preserve the solid contact in the forecast path.");
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
            Assert.That(points.Length, Is.LessThan(401), "Recovery must end the path before the step cap.");
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
            var points = Trace(physics, seconds: 2, steps: 120);
            Assert.That(points.Max(p => p.z), Is.EqualTo(5).Within(.02f));
            Assert.That(points.Last().z, Is.LessThan(0));
        }

        [Test]
        public void Player003_CrowdImpactReducesSourceMomentumWithoutForecastingVictimPath()
        {
            Entity target = Body(new float3(0, 0, 6));
            using var physics = Physics(source, target);
            var points = Trace(physics, seconds: 1, steps: 60);
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
        public void Player003_AllSavedRotatingObjectiveFloorsHavePreviewSurfaces()
        {
            string coverGuid = UnityEditor.AssetDatabase.AssetPathToGUID(
                "Assets/CrowdPunch/Scripts/Authoring/RotatingCoverAuthoring.cs");
            int checkedFloors = 0;
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Scene", new[] { "Assets/CrowdPunch/Scenes" }))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (!System.IO.File.ReadAllText(path).Contains(coverGuid)) continue;
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var floors = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                        .Where(t => t.name == "Central enclosure plinth" || t.name == "Village training turntable");
                    foreach (var floor in floors)
                    {
                        var surface = floor.GetComponent<CrowdPunch.Authoring.TrajectoryPreviewSurfaceAuthoring>();
                        Assert.That(surface, Is.Not.Null, path + ": " + floor.name);
                        float top = floor.TransformPoint(Vector3.up * surface.heightOffset).y;
                        float radius = surface.radius * Mathf.Max(Mathf.Abs(floor.lossyScale.x), Mathf.Abs(floor.lossyScale.z));
                        var renderer = floor.GetComponent<MeshRenderer>();
                        if (renderer == null)
                            renderer = floor.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "Inset blue turntable");
                        Assert.That(top, Is.EqualTo(renderer.bounds.max.y).Within(.0001f), path + ": floor top");
                        Assert.That(radius, Is.GreaterThanOrEqualTo(Mathf.Max(renderer.bounds.extents.x,
                            renderer.bounds.extents.z) - .0001f), path + ": floor footprint");
                        checkedFloors++;
                    }
                }
                finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
            }
            Assert.That(checkedFloors, Is.GreaterThanOrEqualTo(10), "Check campaign and legacy rotating floors.");
        }

        [Test]
        public void Player003_DotsAndAccentsClearRaisedFloorsWithoutMovingForecastSamples()
        {
            var root = new GameObject("Raised floor preview");
            var bridge = root.AddComponent<PlayerEcsBridge>();
            var preview = root.AddComponent<PunchTrajectoryPreview>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(PunchTrajectoryPreview);
            try
            {
                type.GetMethod("Awake", flags).Invoke(preview, null);
                bridge.BeginTrajectoryPreview(-1f);
                bridge.AddTrajectoryPreviewSurface(new TrajectoryPreviewSurface
                    { Center = float2.zero, Radius = 2f, Height = -.87f });
                bridge.AddDottedTrajectory(new float3(-4, 3, 0), new float3(4, 3, 0), true);
                type.GetMethod("DrawDottedTrajectories", flags).Invoke(preview,
                    new object[] { bridge.TrajectoryPreviewSegments });
                var mesh = (Mesh)type.GetField("dottedMesh", flags).GetValue(preview);
                var vertices = mesh.vertices;
                Assert.That(vertices.Length, Is.GreaterThan(0));
                bool raised = false, baseFloor = false;
                for (int i = 0; i < vertices.Length; i += 9)
                {
                    bool onPlatform = Mathf.Abs(vertices[i].x) <= 2f + .11f;
                    Assert.That(vertices[i].y, Is.EqualTo(onPlatform ? -.855f : -.985f).Within(.0001f));
                    raised |= onPlatform; baseFloor |= !onPlatform;
                }
                Assert.That(raised && baseFloor, Is.True, "The path enters and exits the raised floor.");
                Assert.That(bridge.TrajectoryPreviewSegments[0].Start.y, Is.EqualTo(3f));
                Assert.That(bridge.TrajectoryPreviewSegments[0].End.y, Is.EqualTo(3f));
                var arrowStart = (Vector3)type.GetMethod("ArrowGroundPosition", flags).Invoke(preview,
                    new object[] { new float3(-4, 3, 0), new float3(4, 3, 0), .6f });
                Assert.That(arrowStart.y, Is.EqualTo(-.855f).Within(.0001f));

                bridge.BeginTrajectoryPreview(-1f);
                bridge.AddTrajectoryPreviewSurface(new TrajectoryPreviewSurface
                    { Center = float2.zero, Radius = 2f, Height = -.87f });
                bridge.AddTrajectoryImpact(new float3(0, 3, 0), math.forward());
                type.GetMethod("DrawDottedTrajectories", flags).Invoke(preview,
                    new object[] { bridge.TrajectoryPreviewSegments });
                Assert.That(mesh.vertices.All(p => math.abs(p.y + .855f) < .0001f), Is.True);

                bridge.ClearPunchPreview();
                Assert.That(bridge.TrajectoryPreviewSurfaces.Count, Is.Zero);
            }
            finally
            {
                foreach (string name in new[] { "lineMaterial", "arrowMesh", "collisionArrowMesh", "dottedMesh" })
                {
                    var field = type.GetField(name, flags);
                    Object.DestroyImmediate((Object)field.GetValue(preview));
                    field.SetValue(preview, null);
                }
                Object.DestroyImmediate(root);
            }
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
            em.AddComponentData(em.CreateEntity(), new ArenaGroundPlane { SurfaceHeight = -.75f });
            Entity surface = em.CreateEntity(typeof(TrajectoryPreviewSurface));
            em.SetComponentData(surface, new TrajectoryPreviewSurface
                { Center = float2.zero, Radius = 5f, Height = -.62f });
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
                Assert.That(bridge.TrajectoryPreviewSurfaces.Count, Is.EqualTo(1));
                Assert.That(bridge.TrajectoryPreviewSurfaces[0].Height, Is.EqualTo(-.62f));
                em.DestroyEntity(surface);
                system.Update(world.Unmanaged);
                Assert.That(bridge.TrajectoryPreviewSurfaces.Count, Is.Zero);
                Assert.That(bridge.TrajectoryGroundHeight, Is.EqualTo(-.75f));
                Assert.That(bridge.TrajectoryPreviewSegments[0].Start.y, Is.EqualTo(0f),
                    "Ground projection must not change the body-height forecast used for collisions.");
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
