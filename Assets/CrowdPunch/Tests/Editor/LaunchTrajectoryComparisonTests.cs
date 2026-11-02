using System;
using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Physics;
using CrowdPunch.Systems.Presentation;
using CrowdPunch.Mono.Player;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    // PLAYER-003/004, COMBAT-012: freeze the shown estimate, then run the real ECS/physics path.
    public sealed class LaunchTrajectoryComparisonTests
    {
        private World world;
        private EntityManager em;
        private readonly List<BlobAssetReference<Collider>> colliders = new List<BlobAssetReference<Collider>>();
        private EnemyLaunchSettings settings;
        private Entity source, wall, target;
        private const float Speed = 12;

        [SetUp]
        public void SetUp()
        {
            world = new World("PLAYER-003 forecast versus actual flight"); em = world.EntityManager;
            source = wall = target = Entity.Null;
            world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            world.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
            var types = new List<Type>();
            foreach (var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
                if (type.Namespace == "Unity.Physics.Systems") types.Add(type);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, types);
            var physicsStep = PhysicsStep.Default; physicsStep.Gravity = float3.zero;
            em.AddComponentData(em.CreateEntity(), physicsStep);
            settings = new EnemyLaunchSettings { MinimumPropagationImpulse = .1f, PropagationAimCorrectionRadius = 6,
                PropagationAimMaximumAngleDegrees = 20, LaunchHomingDuration = .25f, LaunchHomingDegreesPerSecond = 45,
                LaunchHomingMaximumAngleDegrees = 20, AimLeadSeconds = .2f, AimLeadMaximumDistance = 2,
                UsefulMomentumSpeed = .1f, LowMomentumPeriod = .2f, MinimumDamageImpulse = .1f,
                BaseCollisionDamageMultiplier = 1, MaximumCollisionDamageMultiplier = 1 };
            em.AddComponentData(em.CreateEntity(), settings);
            var material = Unity.Physics.Material.Default;
            material.Friction = 0; material.CollisionResponse = CollisionResponsePolicy.CollideRaiseCollisionEvents;
            var sphere = Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .5f }, CollisionFilter.Default, material);
            colliders.Add(sphere);
            source = Body(float3.zero, sphere, true);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose(); foreach (var collider in colliders) collider.Dispose(); colliders.Clear();
        }

        private Entity Body(float3 position, BlobAssetReference<Collider> collider, bool enemy)
        {
            Entity body = em.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld), typeof(PhysicsCollider),
                typeof(PhysicsWorldIndex), typeof(Simulate));
            em.SetComponentData(body, LocalTransform.FromPosition(position));
            em.SetComponentData(body, new PhysicsCollider { Value = collider });
            if (enemy)
            {
                em.AddComponent<Enemy>(body); em.AddComponent<EnemyTier>(body); em.AddComponent<EnemyLaunchState>(body);
                em.AddComponentData(body, new Health { Current = 100, Max = 100 });
                em.AddComponentData(body, PhysicsMass.CreateDynamic(collider.Value.MassProperties, 1));
                em.AddComponent<PhysicsVelocity>(body); em.AddComponent<ExternalImpulse>(body); em.AddComponent<DamageRequest>(body);
                em.AddComponent<PunchAimAssistTarget>(body);
                em.AddComponent<CommittedLaunchFlight>(body); em.SetComponentEnabled<CommittedLaunchFlight>(body, false);
                em.AddBuffer<DisplayedLaunchRebound>(body); em.AddBuffer<CommittedLaunchRebound>(body);
                em.SetComponentEnabled<ExternalImpulse>(body, false); em.SetComponentEnabled<DamageRequest>(body, false);
            }
            return body;
        }

        private Entity Wall(bool bouncy, bool side = false)
        {
            var material = Unity.Physics.Material.Default;
            material.Friction = 0; material.Restitution = bouncy ? 1 : 0;
            material.RestitutionCombinePolicy = Unity.Physics.Material.CombinePolicy.Maximum;
            material.CollisionResponse = CollisionResponsePolicy.CollideRaiseCollisionEvents;
            var box = Unity.Physics.BoxCollider.Create(new BoxGeometry { Orientation = quaternion.identity,
                Size = side ? new float3(1, 10, 20) : new float3(20, 10, 1), BevelRadius = 0 }, CollisionFilter.Default, material);
            colliders.Add(box);
            return Body(side ? new float3(6, 0, 0) : new float3(0, 0, 6), box, false);
        }

        [TestCase("free", 1f / 60)]
        [TestCase("free", .02f)]
        [TestCase("damping", 1f / 60)]
        [TestCase("stationary", 1f / 60)]
        [TestCase("moving", 1f / 60)]
        [TestCase("wall", 1f / 60)]
        [TestCase("assisted-wall", 1f / 60)]
        [TestCase("cover", 1f / 60)]
        [TestCase("boss", 1f / 60)]
        [TestCase("finale", 1f / 60)]
        [TestCase("corner", 1f / 60)]
        [TestCase("barricade", 1f / 60)]
        [TestCase("destroy", 1f / 60)]
        [TestCase("restricted", 1f / 60)]
        [TestCase("recorded", 1f / 60)]
        [TestCase("shell", 1f / 60)]
        [TestCase("protected-shell", 1f / 60)]
        public void Player003_RecordedForecastMatchesRuntimeSamplesAndFirstContact(string scenario, float dt)
        {
            float3 direction = math.forward();
            if (scenario == "damping") em.AddComponentData(source, new PhysicsDamping { Linear = .8f });
            if (scenario == "stationary" || scenario == "moving")
            {
                target = Body(new float3(.3f, 0, 5), colliders[0], true);
                if (scenario == "moving") em.SetComponentData(target, new PhysicsVelocity { Linear = new float3(.25f, 0, 0) });
                em.SetComponentData(source, new PunchAimAssistTarget { Target = target });
                direction = math.normalizesafe(LaunchAimPrediction.Point(float3.zero,
                    em.GetComponentData<LocalTransform>(target).Position, em.GetComponentData<PhysicsVelocity>(target).Linear,
                    Speed, settings.AimLeadSeconds, settings.AimLeadMaximumDistance));
            }
            if (scenario == "wall" || scenario == "assisted-wall" || scenario == "corner" || scenario == "boss") wall = Wall(true);
            if (scenario == "assisted-wall") target = Body(new float3(1.2f, 0, 1), colliders[0], true);
            if (scenario == "corner") { Wall(true, true); direction = math.normalize(new float3(1, 0, 1)); }
            if (scenario == "barricade" || scenario == "destroy" || scenario == "restricted" || scenario == "recorded"
                || scenario == "shell" || scenario == "protected-shell" || scenario == "finale")
            {
                wall = Wall(false);
                var collider = em.GetComponentData<PhysicsCollider>(wall).Value;
                em.AddComponentData(wall, new Barricade { HitsRemaining = scenario == "destroy" || scenario == "restricted" || scenario == "recorded" ? 1 : 3,
                    RequiredHits = 3, IntactCollider = collider, ReboundMultiplier = .85f,
                    Sources = scenario == "restricted" ? BarricadeLaunchSources.Enemy : BarricadeLaunchSources.Player,
                    Size = new float3(20, 10, 1) });
                em.AddBuffer<BarricadeHitHistory>(wall); em.AddBuffer<BarricadeRebound>(wall);
                if (scenario == "recorded") em.GetBuffer<BarricadeHitHistory>(wall).Add(new BarricadeHitHistory { Source = source, LaunchSequence = 1 });
                if (scenario == "shell" || scenario == "protected-shell") em.AddComponentData(wall, new ShellTarget {
                    CoreHealth = 5, CoreMaxHealth = 5, ExplosionsRemaining = scenario == "protected-shell" ? 1 : 0 });
            }
            var player = new PlayerSnapshot { IsAvailable = true, Position = float3.zero };
            if (scenario == "cover" || scenario == "boss" || scenario == "finale")
                em.AddComponentData(em.CreateEntity(), player);
            if (scenario == "boss") em.AddComponent<ChickenBoss>(wall);
            if (scenario == "finale")
            {
                em.AddComponentData(wall, new FinaleBoss { Phase = FinalePhase.Flying });
                em.AddComponentData(wall, new FinaleTuning { ReboundSafetyDegrees = 30 });
            }
            if (scenario == "cover")
            {
                wall = Wall(false);
                em.AddComponentData(wall, new RotatingCover { ReflectionMultiplier = .85f });
                em.AddBuffer<CoverReflection>(wall);
                // The real cover sweep runs in arenas containing an intact target.
                Entity objective = Wall(false);
                em.SetComponentData(objective, LocalTransform.FromPosition(new float3(0, 0, 100)));
                em.AddComponentData(objective, new Barricade { HitsRemaining = 1,
                    IntactCollider = em.GetComponentData<PhysicsCollider>(objective).Value });
            }
            var simulation = world.GetOrCreateSystemManaged<Unity.Physics.Systems.PhysicsSystemGroup>();
            world.SetTime(new TimeData(dt, dt)); simulation.Update(); em.CompleteAllTrackedJobs();
            using var query = em.CreateEntityQuery(typeof(PhysicsWorldSingleton));
            var physics = query.GetSingleton<PhysicsWorldSingleton>();
            using var points = new NativeList<float3>(Allocator.Temp);
            using var samples = new NativeList<LaunchTrajectorySample>(Allocator.Temp);
            using var rebounds = new NativeList<LaunchReboundPlan>(Allocator.Temp);
            var launch = new LaunchPreviewSource { Entity = source, Direction = direction, Speed = Speed, Damage = 10 };
            using var punched = new NativeArray<LaunchPreviewSource>(new[] { launch }, Allocator.Temp);
            DirectLaunchTrajectoryPrediction.Trace(em, physics.CollisionWorld, launch, punched, settings,
                1, 160, points, player: player, fixedTimestep: dt, samples: samples, rebounds: rebounds);
            var displayed = em.GetBuffer<DisplayedLaunchRebound>(source);
            foreach (var rebound in rebounds) displayed.Add(new DisplayedLaunchRebound { Value = rebound });
            var aim = em.GetComponentData<PunchAimAssistTarget>(source);
            aim.PreviewRevision = 1; aim.PreviewDirection = direction; aim.PreviewPlanSeconds = 1;
            aim.PreviewTarget = aim.Target; aim.PreviewHasAimPoint = aim.Target != Entity.Null ? (byte)1 : (byte)0;
            if (aim.Target != Entity.Null)
                aim.PreviewAimPoint = LaunchAimPrediction.Point(float3.zero, em.GetComponentData<LocalTransform>(aim.Target).Position,
                    em.GetComponentData<PhysicsVelocity>(aim.Target).Linear, Speed, settings.AimLeadSeconds, settings.AimLeadMaximumDistance);
            em.SetComponentData(source, aim);
            Assert.That(PunchResolution.TryApply(em, source, new PunchSpecification {
                Origin = -direction, Direction = direction, Range = 2, Radius = 2, Strength = Speed, Damage = 10,
                Cause = EnemyLaunchCause.PlayerPunch, AffectActive = 1, ApplyDamage = 1, HasAssistedLaunchDirection = 1,
                AssistedLaunchDirection = direction, AssistedLaunchTarget = scenario == "assisted-wall" ? Entity.Null : target,
                HomingDuration = settings.LaunchHomingDuration, CommitDisplayedPlan = 1, AimRevision = 1 }), Is.True);
            world.GetOrCreateSystem<ApplyImpulseSystem>().Update(world.Unmanaged);
            using var contacts = new NativeList<Entity>(Allocator.TempJob);
            float maximumError = 0f, squaredError = 0f, angleError = 0f, preContactError = 0f;
            int actualContact = -1, predictedContact = -1;
            Entity actualVictim = Entity.Null, predictedVictim = Entity.Null;
            bool actualGuidance = false, predictedGuidance = false;
            for (int i = 1; i < samples.Length; i++)
            {
                world.SetTime(new TimeData((i + 1) * dt, dt));
                uint beforeHit = wall != Entity.Null && em.HasComponent<Barricade>(wall)
                    ? em.GetComponentData<Barricade>(wall).HitSequence : 0;
                world.GetOrCreateSystem<CommittedLaunchValidationSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<EnemyLaunchHomingSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<BarricadeImpactSystem>().Update(world.Unmanaged);
                bool sweptHit = wall != Entity.Null && em.HasComponent<Barricade>(wall)
                    && em.GetComponentData<Barricade>(wall).HitSequence != beforeHit;
                simulation.Update();
                em.CompleteAllTrackedJobs();
                contacts.Clear();
                var capture = new CaptureContacts { Source = source, Contacts = contacts };
                var completedSimulation = querySimulation();
                capture.Schedule(completedSimulation, completedSimulation.AsSimulation().FinalSimulationJobHandle).Complete();
                world.GetOrCreateSystem<BarricadeReboundSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<CoverReflectionSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<EnemyLaunchCollisionSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<BossHeadBounceSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<CommittedLaunchReboundSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<EnemyRicochetAimSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();
                float error = math.distance(samples[i].Position, em.GetComponentData<LocalTransform>(source).Position);
                maximumError = math.max(maximumError, error); squaredError += error * error;
                if (predictedContact < 0 && actualContact < 0 && samples[i].Contact == Entity.Null && contacts.Length == 0 && !sweptHit)
                    preContactError = math.max(preContactError, error);
                if (scenario == "assisted-wall")
                {
                    actualGuidance |= em.GetComponentData<EnemyLaunchState>(source).HomingTarget == target;
                    predictedGuidance |= samples[i].GuidanceTarget == target;
                    if (samples[i].Contact == wall)
                        Assert.That(samples[i].GuidanceSeconds, Is.EqualTo(settings.LaunchHomingDuration),
                            "The rebound budget must not be consumed inside its contact step.");
                }
                if (predictedContact < 0 && samples[i].Contact != Entity.Null)
                { predictedContact = i; predictedVictim = samples[i].Contact; }
                if (actualContact < 0 && (contacts.Length > 0 || sweptHit))
                {
                    actualContact = i; actualVictim = sweptHit ? wall : contacts[0];
                    float3 actualVelocity = em.GetComponentData<PhysicsVelocity>(source).Linear;
                    // The forecast is sampled at this same time, not at its independently estimated impact time.
                    if (math.lengthsq(actualVelocity.xz) > .001f && math.lengthsq(samples[i].Velocity.xz) > .001f)
                        angleError = math.degrees(math.acos(math.clamp(math.dot(math.normalize(actualVelocity.xz),
                            math.normalize(samples[i].Velocity.xz)), -1f, 1f)));
                }
            }
            float rms = math.sqrt(squaredError / math.max(1, samples.Length - 1));
            TestContext.WriteLine($"{scenario}, dt={dt:F5}: pre-contact={preContactError:F4}m, max={maximumError:F4}m, rms={rms:F4}m, first predicted={predictedContact * dt:F4}s actual={actualContact * dt:F4}s, outgoing angle={angleError:F3}deg");
            float tolerance = scenario == "free" || scenario == "damping" || scenario == "destroy" || scenario == "shell" ? .001f
                : scenario == "stationary" ? .01f : scenario == "moving" ? .15f : scenario == "wall" ? .025f : .4f;
            if (scenario == "moving") Assert.That(preContactError, Is.LessThan(.001f), "Committed steering must match until the physical impact.");
            Assert.That(maximumError, Is.LessThan(tolerance));
            if (scenario == "assisted-wall") { Assert.That(actualGuidance, Is.True); Assert.That(predictedGuidance, Is.True); }
            Assert.That(actualContact >= 0, Is.EqualTo(predictedContact >= 0));
            if (actualContact >= 0)
            {
                if (scenario != "corner") Assert.That(actualVictim, Is.EqualTo(predictedVictim));
                Assert.That(math.abs(actualContact - predictedContact) * dt, Is.LessThanOrEqualTo(dt * 2.01f));
                Assert.That(angleError, Is.LessThan(scenario == "corner" ? 15f : 8f));
            }

            SimulationSingleton querySimulation()
            {
                using var simulationQuery = em.CreateEntityQuery(typeof(SimulationSingleton));
                return simulationQuery.GetSingleton<SimulationSingleton>();
            }
        }

        private struct CaptureContacts : ICollisionEventsJob
        {
            public Entity Source;
            public NativeList<Entity> Contacts;
            public void Execute(CollisionEvent collision)
            {
                if (collision.EntityA == Source) Contacts.Add(collision.EntityB);
                else if (collision.EntityB == Source) Contacts.Add(collision.EntityA);
            }
        }

        [Test]
        public void Player003_PresentationUsesTheWorldFixedTimestepAndPublishesDamage()
        {
            world.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>().Timestep = .01f;
            var simulation = world.GetOrCreateSystemManaged<Unity.Physics.Systems.PhysicsSystemGroup>();
            world.SetTime(new TimeData(.01, .01f)); simulation.Update(); em.CompleteAllTrackedJobs();
            em.AddComponentData(em.CreateEntity(), new PlayerSnapshot());
            PlayerBridgeRegistry.TryGetBridge(out var previous);
            var root = new UnityEngine.GameObject("Forecast fixed-step integration");
            var bridge = root.AddComponent<PlayerEcsBridge>();
            try
            {
                PlayerBridgeRegistry.Register(bridge);
                bridge.PublishPunchPreview(UnityEngine.Vector3.back, UnityEngine.Vector3.forward, 1, 2, 0, 0, 0, 0,
                    previewStrength: Speed, useDottedTrajectory: true, trajectoryMaximumSeconds: 8, trajectorySteps: 16, previewDamage: 13);
                world.GetOrCreateSystem<PresentationBridgeSystem>().Update(world.Unmanaged);
                Assert.That(bridge.PunchPreviewDamage, Is.EqualTo(13));
                var segments = bridge.TrajectoryPreviewSegments;
                Assert.That(segments.Count, Is.EqualTo(16));
                Assert.That(segments[0].End.z, Is.EqualTo(Speed * .01f).Within(.0001f));
                Assert.That(segments[15].End.z, Is.EqualTo(Speed * .01f * 16).Within(.0001f));
            }
            finally
            {
                PlayerBridgeRegistry.Unregister(bridge); if (previous != null) PlayerBridgeRegistry.Register(previous);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(1f / 60)]
        [TestCase(.02f)]
        public void Player003_StepBudgetTruncatesDurationWithoutStretchingPhysicsSteps(float dt)
        {
            using var physics = new PhysicsWorld(0, 0, 0);
            var copy = physics;
            copy.CollisionWorld.BuildBroadphase(ref copy, dt, float3.zero, true);
            using var points = new NativeList<float3>(Allocator.Temp);
            using var samples = new NativeList<LaunchTrajectorySample>(Allocator.Temp);
            var initial = new LaunchPreviewSource { Entity = source, Direction = math.forward(), Speed = Speed };
            using var punched = new NativeArray<LaunchPreviewSource>(new[] { initial }, Allocator.Temp);
            DirectLaunchTrajectoryPrediction.Trace(em, physics.CollisionWorld, initial, punched, settings, 8, 16, points,
                fixedTimestep: dt, samples: samples);
            Assert.That(samples.Length, Is.EqualTo(17));
            Assert.That(samples[16].Seconds, Is.EqualTo(dt * 16).Within(.00001f));
            Assert.That(samples[16].Position.z, Is.EqualTo(Speed * dt * 16).Within(.0001f));
        }
    }
}
