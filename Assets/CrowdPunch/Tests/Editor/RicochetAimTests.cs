using System.Linq;
using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Physics;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class RicochetAimTests
    {
        private World world;
        private EntityManager em;
        private BlobAssetReference<Collider> sphere, bounce;
        private EnemyLaunchSettings settings;
        private Entity source, surface, target;

        [SetUp]
        public void SetUp()
        {
            world = new World("PLAYER-003/004 ricochet aiming"); em = world.EntityManager;
            sphere = Sphere(false); bounce = Sphere(true);
            source = Body(float3.zero);
            surface = Body(new float3(0, 0, 6));
            em.RemoveComponent<Enemy>(surface);
            em.SetComponentData(surface, new PhysicsMass());
            em.SetComponentData(surface, new PhysicsCollider { Value = bounce });
            target = Body(new float3(1.2f, 0, 1));
            settings = new EnemyLaunchSettings { PropagationAimCorrectionRadius = 6,
                PropagationAimMaximumAngleDegrees = 20, LaunchHomingDuration = .25f,
                LaunchHomingDegreesPerSecond = 45, LaunchHomingMaximumAngleDegrees = 20,
                UsefulMomentumSpeed = 1, LowMomentumPeriod = .2f, MinimumPropagationImpulse = .1f };
            em.AddComponentData(em.CreateEntity(), settings);
        }

        [TearDown]
        public void TearDown() { world.Dispose(); sphere.Dispose(); bounce.Dispose(); }

        private static BlobAssetReference<Collider> Sphere(bool bouncy)
        {
            var material = Unity.Physics.Material.Default;
            material.Friction = 0;
            material.Restitution = bouncy ? 1 : 0;
            material.RestitutionCombinePolicy = Unity.Physics.Material.CombinePolicy.Maximum;
            material.CollisionResponse = CollisionResponsePolicy.CollideRaiseCollisionEvents;
            return Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .5f }, CollisionFilter.Default, material);
        }

        private Entity Body(float3 position)
        {
            Entity body = em.CreateEntity(typeof(Enemy), typeof(EnemyLaunchState), typeof(Health),
                typeof(LocalTransform), typeof(PhysicsCollider), typeof(PhysicsMass), typeof(PhysicsVelocity));
            em.SetComponentData(body, LocalTransform.FromPosition(position));
            em.SetComponentData(body, new Health { Current = 100, Max = 100 });
            em.SetComponentData(body, new PhysicsCollider { Value = sphere });
            em.SetComponentData(body, PhysicsMass.CreateDynamic(sphere.Value.MassProperties, 1));
            return body;
        }

        private PhysicsWorld Physics(params Entity[] entities)
        {
            var physics = new PhysicsWorld(entities.Length, 0, 0);
            var bodies = physics.Bodies;
            for (int i = 0; i < entities.Length; i++)
            {
                var pose = em.GetComponentData<LocalTransform>(entities[i]);
                bodies[i] = new RigidBody { Entity = entities[i], Collider = em.GetComponentData<PhysicsCollider>(entities[i]).Value,
                    WorldFromBody = new RigidTransform(pose.Rotation, pose.Position), Scale = 1 };
            }
            physics.CollisionWorld.BuildBroadphase(ref physics, .02f, float3.zero, true);
            return physics;
        }

        [Test]
        public void Player004_ReboundReacquiresOnceAndPreservesFlightIdentitySpeedAndHeight()
        {
            em.SetComponentData(source, LocalTransform.FromPosition(new float3(0, 0, 5)));
            var launch = new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, Owner = EnemyLaunchOwner.Player,
                LaunchSequence = 7, ContinuousFlight = 2, LaunchDamage = 13, RicochetSurface = surface, RicochetNormal = -math.forward() };
            em.SetComponentData(source, launch);
            em.SetComponentData(source, new PhysicsVelocity { Linear = new float3(0, 3, -12) });
            using var physics = Physics(source, surface, target);
            em.AddComponentData(em.CreateEntity(), new PhysicsWorldSingleton { PhysicsWorld = physics });
            var system = world.GetOrCreateSystem<EnemyRicochetAimSystem>(); system.Update(world.Unmanaged);
            var result = em.GetComponentData<EnemyLaunchState>(source);
            float3 velocity = em.GetComponentData<PhysicsVelocity>(source).Linear;
            Assert.That(result.HomingTarget, Is.EqualTo(target));
            Assert.That(result.HomingSecondsRemaining, Is.EqualTo(.25f));
            Assert.That(result.RicochetSurface, Is.EqualTo(Entity.Null));
            Assert.That(result.LaunchSequence, Is.EqualTo(7)); Assert.That(result.ContinuousFlight, Is.EqualTo(2));
            Assert.That(result.LaunchDamage, Is.EqualTo(13)); Assert.That(result.Owner, Is.EqualTo(EnemyLaunchOwner.Player));
            Assert.That(math.length(velocity.xz), Is.EqualTo(12).Within(.0001f)); Assert.That(velocity.y, Is.EqualTo(3));
            Assert.That(velocity.x, Is.GreaterThan(0)); Assert.That(velocity.z, Is.LessThan(0));
            system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<PhysicsVelocity>(source).Linear, Is.EqualTo(velocity), "No repeated correction without a new contact.");
        }

        [TestCase("range")]
        [TestCase("angle")]
        [TestCase("behind")]
        [TestCase("dead")]
        [TestCase("blocked")]
        [TestCase("incoming")]
        public void Player004_ReboundRejectsDisabledInvalidAndObstructedSolutions(string reason)
        {
            if (reason == "range") settings.PropagationAimCorrectionRadius = 0;
            if (reason == "angle") settings.PropagationAimMaximumAngleDegrees = 0;
            if (reason == "behind") em.SetComponentData(target, LocalTransform.FromPosition(new float3(0, 0, 9)));
            if (reason == "dead") em.SetComponentData(target, new Health { Current = 0 });
            Entity blocker = Body(new float3(.6f, 0, 3));
            em.RemoveComponent<Enemy>(blocker);
            em.SetComponentData(blocker, new PhysicsMass());
            using var physics = reason == "blocked" ? Physics(source, surface, target, blocker) : Physics(source, surface, target);
            using var nearbyStorage = new NativeList<int>(Allocator.Temp);
            using var rayStorage = new NativeList<RaycastHit>(Allocator.Temp);
            var nearby = nearbyStorage; var rays = rayStorage;
            float3 original = reason == "incoming" ? new float3(0, 3, 12) : new float3(0, 3, -12);
            float3 velocity = original;
            Assert.That(RicochetAimCorrection.TryCorrect(em, physics.CollisionWorld, source, surface,
                new float3(0, 0, 5), -math.forward(), ref velocity, settings, ref nearby, ref rays, out _), Is.False);
            Assert.That(velocity, Is.EqualTo(original));
        }

        [Test]
        public void Player003_DottedPathUsesAssistedReboundAndMarksItsTarget()
        {
            using var physics = Physics(source, surface, target);
            var initial = new LaunchPreviewSource { Entity = source, Direction = math.forward(), Speed = 12 };
            using var punched = new NativeArray<LaunchPreviewSource>(new[] { initial }, Allocator.Temp);
            using var points = new NativeList<float3>(Allocator.Temp);
            using var impacts = new NativeList<LaunchTrajectoryImpact>(Allocator.Temp);
            DirectLaunchTrajectoryPrediction.Trace(em, physics.CollisionWorld, initial, punched, settings, 1.5f, 120, points, impacts: impacts);
            Assert.That(points.AsArray().ToArray().Max(p => p.z), Is.EqualTo(5).Within(.02f));
            Assert.That(impacts.Length, Is.EqualTo(1), "Only the rebound victim, not ordinary scenery, receives an accent.");
            Assert.That(impacts[0].Position.x, Is.GreaterThan(.6f));
            Assert.That(impacts[0].Direction.x, Is.GreaterThan(0)); Assert.That(impacts[0].Direction.z, Is.LessThan(0));
            Assert.That(em.GetComponentData<EnemyLaunchState>(source).HomingTarget, Is.EqualTo(Entity.Null), "Preview must remain read only.");
            settings.PropagationAimCorrectionRadius = 0;
            DirectLaunchTrajectoryPrediction.Trace(em, physics.CollisionWorld, initial, punched, settings, 1.5f, 120, points, impacts: impacts);
            Assert.That(impacts.Length, Is.Zero, "Unassisted rebound misses the off-axis body.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Player004_AuthoredReboundQueuesAssistanceWithoutStartingAnotherLaunch(bool cover)
        {
            em.SetComponentData(source, LocalTransform.FromPosition(new float3(0, 0, 5)));
            em.SetComponentData(source, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched,
                Owner = EnemyLaunchOwner.Player, LaunchSequence = 4, HomingTarget = surface });
            if (cover)
            {
                em.AddComponentData(surface, new RotatingCover { ReflectionMultiplier = 1 });
                em.AddBuffer<CoverReflection>(surface).Add(new CoverReflection { Source = source, LaunchSequence = 4,
                    ContactCenter = new float3(0, 0, 5), Normal = -math.forward(), IncomingVelocity = math.forward() * 12,
                    PlayerPosition = float3.zero });
                world.GetOrCreateSystem<CoverReflectionSystem>().Update(world.Unmanaged);
            }
            else
            {
                em.AddComponentData(surface, new Barricade { HitsRemaining = 3, ReboundMultiplier = 1 });
                em.AddBuffer<BarricadeRebound>(surface).Add(new BarricadeRebound { Source = source, LaunchSequence = 4,
                    ContactCenter = new float3(0, 0, 5), Normal = -math.forward(), IncomingVelocity = math.forward() * 12 });
                world.GetOrCreateSystem<BarricadeReboundSystem>().Update(world.Unmanaged);
            }
            using var physics = Physics(source, surface, target);
            em.AddComponentData(em.CreateEntity(), new PhysicsWorldSingleton { PhysicsWorld = physics });
            world.GetOrCreateSystem<EnemyRicochetAimSystem>().Update(world.Unmanaged);
            var result = em.GetComponentData<EnemyLaunchState>(source);
            Assert.That(result.HomingTarget, Is.EqualTo(target)); Assert.That(result.LaunchSequence, Is.EqualTo(4));
            Assert.That(em.GetComponentData<PhysicsVelocity>(source).Linear.x, Is.GreaterThan(0));
        }

        [TestCase(100)]
        [TestCase(1000)]
        public void Player003_ProfileAssistedRicochetForecast(int crowd)
        {
            var bodies = new Entity[crowd + 3];
            bodies[0] = source; bodies[1] = surface; bodies[2] = target;
            for (int i = 3; i < bodies.Length; i++) bodies[i] = Body(new float3(20 + i % 32 * 2, 0, i / 32 * 2));
            using var physics = Physics(bodies);
            var initial = new LaunchPreviewSource { Entity = source, Direction = math.forward(), Speed = 12 };
            using var punched = new NativeArray<LaunchPreviewSource>(new[] { initial }, Allocator.Temp);
            using var points = new NativeList<float3>(Allocator.Temp);
            void Forecast() => DirectLaunchTrajectoryPrediction.Trace(em, physics.CollisionWorld, initial,
                punched, settings, 8, 160, points);
            Forecast();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 80; i++) Forecast();
            timer.Stop();
            Assert.That(points.Length, Is.GreaterThan(2));
            TestContext.WriteLine($"{crowd} crowd, eight assisted ricochet forecasts: {timer.Elapsed.TotalMilliseconds / 10:F3} ms per preview frame (warm Editor).");
            em.AddComponentData(em.CreateEntity(), new PhysicsWorldSingleton { PhysicsWorld = physics });
            em.SetComponentData(source, LocalTransform.FromPosition(new float3(0, 0, 5)));
            var system = world.GetOrCreateSystem<EnemyRicochetAimSystem>();
            void ReboundFrame()
            {
                em.SetComponentData(source, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched,
                    Owner = EnemyLaunchOwner.Player, RicochetSurface = surface, RicochetNormal = -math.forward() });
                em.SetComponentData(source, new PhysicsVelocity { Linear = -math.forward() * 12 });
                system.Update(world.Unmanaged);
            }
            ReboundFrame(); timer.Restart();
            for (int i = 0; i < 100; i++) ReboundFrame();
            timer.Stop();
            Assert.That(em.GetComponentData<EnemyLaunchState>(source).HomingTarget, Is.EqualTo(target));
            TestContext.WriteLine($"{crowd} crowd, runtime mailbox scan and one assisted rebound: {timer.Elapsed.TotalMilliseconds / 100:F3} ms/update (warm Editor).");
        }

        [Test]
        public void Player004_RealSolverWallContactQueuesReboundAssistance()
        {
            world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            world.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
            var types = new System.Collections.Generic.List<System.Type>();
            foreach (var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
                if (type.Namespace == "Unity.Physics.Systems") types.Add(type);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, types);
            var step = PhysicsStep.Default; step.Gravity = float3.zero;
            em.AddComponentData(em.CreateEntity(), step);
            // Only the source is dynamic in this solver check. The distant target remains an assist candidate.
            em.RemoveComponent<PhysicsVelocity>(surface); em.RemoveComponent<PhysicsMass>(surface);
            foreach (Entity entity in new[] { source, surface, target })
            {
                em.AddComponent<LocalToWorld>(entity); em.AddComponent<PhysicsWorldIndex>(entity); em.AddComponent<Simulate>(entity);
            }
            em.SetComponentData(target, PhysicsMass.CreateKinematic(sphere.Value.MassProperties));
            em.SetComponentData(source, LocalTransform.FromPosition(new float3(0, 0, 4.95f)));
            em.SetComponentData(source, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, Owner = EnemyLaunchOwner.Player, LaunchSequence = 1 });
            em.SetComponentData(source, new PhysicsVelocity { Linear = new float3(0, 0, 12) });
            world.SetTime(new TimeData(.02, .02f));
            world.GetOrCreateSystemManaged<Unity.Physics.Systems.PhysicsSystemGroup>().Update();
            world.GetOrCreateSystem<EnemyLaunchCollisionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            Assert.That(em.GetComponentData<EnemyLaunchState>(source).RicochetSurface, Is.EqualTo(surface));
            Assert.That(em.GetComponentData<EnemyLaunchState>(source).RicochetNormal.z, Is.LessThan(0), "Normal must point out from the surface.");
            world.GetOrCreateSystem<EnemyRicochetAimSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyLaunchState>(source).HomingTarget, Is.EqualTo(target),
                $"Normal/velocity: {em.GetComponentData<PhysicsVelocity>(source).Linear}, position: {em.GetComponentData<LocalTransform>(source).Position}");
            Assert.That(em.GetComponentData<PhysicsVelocity>(source).Linear.x, Is.GreaterThan(0));
        }
    }
}
