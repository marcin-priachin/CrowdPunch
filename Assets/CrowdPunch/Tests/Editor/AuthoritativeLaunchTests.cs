using System;
using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.InputBridge;
using CrowdPunch.Systems.Physics;
using CrowdPunch.Systems.Presentation;
using CrowdPunch.Utilities;
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
    public sealed class AuthoritativeLaunchTests
    {
        private World world;
        private EntityManager em;
        private readonly List<BlobAssetReference<Unity.Physics.Collider>> colliders = new List<BlobAssetReference<Unity.Physics.Collider>>();
        private Entity source;
        private PlayerEcsBridge bridge, previous;
        private GameObject root;
        private const float Dt = 1f / 60;
        private int tick;

        [SetUp]
        public void SetUp()
        {
            world = new World("PLAYER-003 authoritative displayed path"); em = world.EntityManager; tick = 0;
            world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            world.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
            var types = new List<Type>();
            foreach (var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
                if (type.Namespace == "Unity.Physics.Systems") types.Add(type);
            types.Add(typeof(CoverEnclosureContactSystem));
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, types);
            var step = Unity.Physics.PhysicsStep.Default; step.Gravity = float3.zero;
            em.AddComponentData(em.CreateEntity(), step);
            em.AddComponentData(em.CreateEntity(), new EnemyLaunchSettings { MinimumPropagationImpulse = .1f,
                MinimumDamageImpulse = .1f, BaseCollisionDamageMultiplier = 1, MaximumCollisionDamageMultiplier = 1,
                PropagationAimCorrectionRadius = 6, PropagationAimMaximumAngleDegrees = 20,
                LaunchHomingDuration = .25f, LaunchHomingDegreesPerSecond = 45, LaunchHomingMaximumAngleDegrees = 20,
                UsefulMomentumSpeed = 1, LowMomentumPeriod = .05f, RecoveryDuration = .1f });
            var material = Unity.Physics.Material.Default;
            material.Friction = 0; material.CollisionResponse = CollisionResponsePolicy.CollideRaiseCollisionEvents;
            colliders.Add(Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .5f }, CollisionFilter.Default, material));
            source = Body(float3.zero, colliders[0], true);
            var player = em.CreateEntity(typeof(PlayerSnapshot), typeof(PlayerHealthSnapshot), typeof(PunchRequest));
            em.SetComponentEnabled<PunchRequest>(player, false);
            PlayerBridgeRegistry.TryGetBridge(out previous);
            root = new GameObject("Authoritative path bridge"); bridge = root.AddComponent<PlayerEcsBridge>();
            PlayerBridgeRegistry.Register(bridge);
            bridge.PublishPunchPreview(Vector3.back, Vector3.forward, 1, 2, 5, 0, 22, 12,
                previewStrength: 12, releaseAngleDegrees: 16, useDottedTrajectory: true, trajectoryMaximumSeconds: 1);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerBridgeRegistry.Unregister(bridge); if (previous != null) PlayerBridgeRegistry.Register(previous);
            UnityEngine.Object.DestroyImmediate(root); world.Dispose();
            foreach (var collider in colliders) collider.Dispose(); colliders.Clear();
        }

        private Entity Body(float3 position, BlobAssetReference<Unity.Physics.Collider> collider, bool enemy)
        {
            Entity entity = em.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld), typeof(PhysicsCollider),
                typeof(PhysicsWorldIndex), typeof(Simulate));
            em.SetComponentData(entity, LocalTransform.FromPosition(position)); em.SetComponentData(entity, new PhysicsCollider { Value = collider });
            if (!enemy) return entity;
            em.AddComponent<Enemy>(entity); em.AddComponent<EnemyTier>(entity); em.AddComponent<EnemyLaunchState>(entity);
            em.AddComponent<RespawnRequest>(entity); em.SetComponentEnabled<RespawnRequest>(entity, false);
            em.AddComponent<EnemyLifetime>(entity);
            em.AddComponent<KnockbackRecovery>(entity); em.SetComponentEnabled<KnockbackRecovery>(entity, false);
            em.AddComponentData(entity, new Health { Current = 100, Max = 100 });
            em.AddComponentData(entity, PhysicsMass.CreateDynamic(collider.Value.MassProperties, 1));
            em.AddComponent<PhysicsMassOverride>(entity); em.AddComponent<PhysicsVelocity>(entity);
            em.AddComponent<EnemyDamageState>(entity); em.AddComponent<DeathRequest>(entity); em.SetComponentEnabled<DeathRequest>(entity, false);
            em.AddComponent<EnemyHealthBarVisibility>(entity);
            em.AddComponent<ExternalImpulse>(entity); em.SetComponentEnabled<ExternalImpulse>(entity, false);
            em.AddComponent<DamageRequest>(entity); em.SetComponentEnabled<DamageRequest>(entity, false);
            em.AddBuffer<CollisionDamageHistory>(entity); em.AddComponent<PunchAimAssistTarget>(entity);
            em.AddComponent<CommittedLaunchFlight>(entity); em.SetComponentEnabled<CommittedLaunchFlight>(entity, false);
            em.AddComponent<AuthoritativeLaunchPath>(entity); em.SetComponentEnabled<AuthoritativeLaunchPath>(entity, false);
            em.AddBuffer<DisplayedLaunchRebound>(entity); em.AddBuffer<CommittedLaunchRebound>(entity);
            em.AddBuffer<DisplayedLaunchPathPoint>(entity); em.AddBuffer<CommittedLaunchPathPoint>(entity);
            return entity;
        }

        private Entity Wall(float z, bool bouncy = false)
        {
            var material = Unity.Physics.Material.Default;
            material.Friction = 0; material.Restitution = bouncy ? 1 : 0;
            material.RestitutionCombinePolicy = Unity.Physics.Material.CombinePolicy.Maximum;
            material.CollisionResponse = CollisionResponsePolicy.CollideRaiseCollisionEvents;
            var collider = Unity.Physics.BoxCollider.Create(new BoxGeometry { Size = new float3(20, 10, 1),
                Orientation = quaternion.identity, BevelRadius = 0 }, CollisionFilter.Default, material);
            colliders.Add(collider); return Body(new float3(0, 0, z), collider, false);
        }

        private void PhysicsStep()
        {
            world.SetTime(new TimeData(++tick * Dt, Dt));
            world.GetOrCreateSystemManaged<Unity.Physics.Systems.PhysicsSystemGroup>().Update(); em.CompleteAllTrackedJobs();
        }

        private void Preview()
        {
            PhysicsStep(); world.GetOrCreateSystem<PresentationBridgeSystem>().Update(world.Unmanaged);
        }

        private void Punch()
        {
            Assert.That(PunchAimAssist.TryGetCommittedDirection(em, source, bridge.TrajectoryPreviewRevision, out _, out _), Is.True,
                "The displayed snapshot must remain consumable before input is bridged.");
            bridge.PublishPunch(Vector3.back, Vector3.forward, 1, 2, 12, .1f, 10, 0);
            world.GetOrCreateSystem<PlayerBridgeSystem>().Update(world.Unmanaged);
            using var requestQuery = em.CreateEntityQuery(typeof(PlayerSnapshot));
            Assert.That(em.GetComponentData<PunchRequest>(requestQuery.GetSingletonEntity()).AimRevision, Is.EqualTo(bridge.PunchAimRevision));
            world.GetOrCreateSystem<PunchDetectionSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<ApplyImpulseSystem>().Update(world.Unmanaged);
            Assert.That(em.IsComponentEnabled<AuthoritativeLaunchPath>(source), Is.True,
                $"shown={em.GetComponentData<PunchAimAssistTarget>(source).PreviewRevision}, click={bridge.PunchAimRevision}, "
                + $"points={em.GetBuffer<DisplayedLaunchPathPoint>(source).Length}/{em.GetBuffer<CommittedLaunchPathPoint>(source).Length}, "
                + $"direction={em.GetComponentData<PunchAimAssistTarget>(source).PreviewDirection}, flight={em.IsComponentEnabled<CommittedLaunchFlight>(source)}, phase={em.GetComponentData<EnemyLaunchState>(source).Phase}");
        }

        private bool Step(bool recovery = false)
        {
            world.SetTime(new TimeData((tick + 1) * Dt, Dt));
            world.GetOrCreateSystem<CommittedLaunchValidationSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<EnemyLaunchHomingSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<RotatingCoverSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<AuthoritativeLaunchMotorSystem>().Update(world.Unmanaged);
            bool driven = em.IsComponentEnabled<AuthoritativeLaunchPath>(source);
            float3 expected = em.GetComponentData<AuthoritativeLaunchPath>(source).ScheduledPosition;
            world.GetOrCreateSystem<BarricadeImpactSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<PillarToppleSystem>().Update(world.Unmanaged);
            PhysicsStep();
            world.GetOrCreateSystem<BossCollisionSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<ChickenCollisionSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<RollingCollisionSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<ChickenDamageSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<RollingDamageSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<BarricadeReboundSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<CoverReflectionSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<EnemyLaunchCollisionSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<CommittedLaunchReboundSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<EnemyRicochetAimSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<AuthoritativeLaunchFinishSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            if (driven) Assert.That(math.distance(em.GetComponentData<LocalTransform>(source).Position, expected), Is.LessThan(.001f),
                "The real Unity Physics step must land on the stored path without a transform correction.");
            Assert.That(em.GetComponentData<PhysicsMass>(source).InverseMass, Is.EqualTo(1), "Gameplay mass data stays intact.");
            if (recovery) world.GetOrCreateSystem<EnemyRecoverySystem>().Update(world.Unmanaged);
            return em.IsComponentEnabled<AuthoritativeLaunchPath>(source);
        }

        [TestCase("free")]
        [TestCase("bounce")]
        [TestCase("enemy")]
        public void Player003_RealPhysicsFollowsEveryDisplayedPathVertex(string kind)
        {
            Entity victim = Entity.Null;
            if (kind == "bounce") Wall(6, true);
            if (kind == "enemy") victim = Body(new float3(0, 0, 5), colliders[0], true);
            Preview();
            var displayed = em.GetBuffer<DisplayedLaunchPathPoint>(source).ToNativeArray(Allocator.Temp);
            using (displayed)
            {
                Assert.That(displayed.Length, Is.GreaterThan(2)); Punch();
                em.GetBuffer<DisplayedLaunchPathPoint>(source).Clear();
                int steps = 0;
                while (em.IsComponentEnabled<AuthoritativeLaunchPath>(source) && steps++ < 250) Step();
                Assert.That(steps, Is.LessThan(250));
                Assert.That(math.distance(em.GetComponentData<LocalTransform>(source).Position, displayed[displayed.Length - 1].Value.Position), Is.LessThan(.001f));
                Assert.That(em.GetComponentData<PhysicsMassOverride>(source).IsKinematic, Is.Zero);
                Assert.That(em.GetComponentData<EnemyLaunchState>(source).LaunchSequence, Is.EqualTo(1));
                if (victim != Entity.Null)
                {
                    Assert.That(em.GetComponentData<EnemyLaunchState>(victim).Phase, Is.EqualTo(EnemyLaunchPhase.Launched));
                    Assert.That(em.GetComponentData<EnemyLaunchState>(victim).Owner, Is.EqualTo(EnemyLaunchOwner.Player));
                    Assert.That(em.GetBuffer<CollisionDamageHistory>(victim).Length, Is.EqualTo(1));
                }
            }
        }

        private BlobAssetReference<Unity.Physics.Collider> EmptyCollider()
        {
            var shape = Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .5f }, new CollisionFilter());
            colliders.Add(shape); return shape;
        }

        private Entity Gate(int hits = 3)
        {
            Entity gate = Wall(3.05f);
            em.AddComponentData(gate, new Barricade { HitsRemaining = hits, RequiredHits = hits,
                Sources = BarricadeLaunchSources.All, Size = new float3(20, 10, 1), ReboundMultiplier = 1,
                IntactCollider = em.GetComponentData<PhysicsCollider>(gate).Value, BrokenCollider = EmptyCollider() });
            em.AddBuffer<BarricadeHitHistory>(gate); em.AddBuffer<BarricadeRebound>(gate);
            return gate;
        }

        private Entity RotatingTarget(float angle, out Entity cover, out Entity enclosure)
        {
            float3 center = new float3(0,0,8);
            var material = Unity.Physics.Material.Default;
            material.Friction = 0; material.CollisionResponse = CollisionResponsePolicy.CollideRaiseCollisionEvents;
            var targetShape = Unity.Physics.BoxCollider.Create(new BoxGeometry { Size = new float3(1.2f,4,1.2f),
                Orientation = quaternion.identity, BevelRadius = .01f }, CollisionFilter.Default, material);
            colliders.Add(targetShape);
            Entity target = Body(center, targetShape, false);
            em.AddComponentData(target, new Barricade { RequiredHits = 4, HitsRemaining = 4, CompleteOnDestruction = 1,
                Size = new float3(1.2f,4,1.2f), Sources = BarricadeLaunchSources.All, ReboundMultiplier = 1,
                IntactCollider = targetShape, BrokenCollider = EmptyCollider() });
            em.AddBuffer<BarricadeHitHistory>(target); em.AddBuffer<BarricadeRebound>(target);
            var data = new RotatingCover { Target = target, Radius = 5, Height = 4, Thickness = .45f,
                OpeningRadians = math.radians(75), InitialAngle = angle, ReflectionMultiplier = 1 };
            var pieces = new NativeArray<CompoundCollider.ColliderBlobInstance>(CoverGeometry.PanelCount, Allocator.Temp);
            for (int i = 0; i < pieces.Length; i++)
            {
                CoverGeometry.Panel(data, i, out var position, out var rotation, out var size);
                var shape = Unity.Physics.BoxCollider.Create(new BoxGeometry { Size = size, Orientation = quaternion.identity,
                    BevelRadius = .01f }, CollisionFilter.Default, material);
                colliders.Add(shape);
                pieces[i] = new CompoundCollider.ColliderBlobInstance { Collider = shape, CompoundFromChild = new RigidTransform(rotation, position) };
            }
            var compound = CompoundCollider.Create(pieces); colliders.Add(compound); pieces.Dispose();
            cover = Body(center, compound, false);
            em.SetComponentData(cover, LocalTransform.FromPositionRotation(center, quaternion.RotateY(angle)));
            em.AddComponentData(cover, data); em.AddComponentData(cover, new RotatingCoverState { Angle = angle });
            em.AddBuffer<CoverReflection>(cover);
            var cylinder = Unity.Physics.CylinderCollider.Create(new CylinderGeometry { Height = data.Height,
                Radius = data.Radius - data.Thickness, Orientation = quaternion.RotateX(math.PI * .5f), SideCount = 32,
                BevelRadius = .01f }, CollisionFilter.Default, material);
            colliders.Add(cylinder); enclosure = Body(center, cylinder, false); em.AddComponent<CoverEnclosure>(enclosure);
            var wall = em.GetComponentData<Barricade>(target); wall.Cover = cover; em.SetComponentData(target, wall);
            return target;
        }

        [TestCase(true, false)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(false, true)]
        public void Cover001_AuthoritativeLaunchUsesTheRealOpeningAndProtectedTarget(bool opening, bool explosive)
        {
            Entity target = RotatingTarget(opening ? math.PI : 0, out var cover, out var enclosure);
            if (explosive)
            {
                em.AddComponent<ExplosiveEnemyState>(source); em.AddComponent<ExplosiveDetonationRequest>(source);
                em.SetComponentEnabled<ExplosiveDetonationRequest>(source, false);
            }
            Preview();
            bool predictedCore = false, predictedShield = false;
            foreach (var p in em.GetBuffer<DisplayedLaunchPathPoint>(source))
            {
                Assert.That(p.Value.Surface, Is.Not.EqualTo(enclosure), "The walking-only enclosure must never become a flight vertex.");
                predictedCore |= p.Value.Surface == target; predictedShield |= p.Value.Surface == cover;
            }
            Assert.That(predictedCore, Is.EqualTo(opening)); Assert.That(predictedShield, Is.EqualTo(!opening));
            Punch(); int steps = 0;
            while (em.IsComponentEnabled<AuthoritativeLaunchPath>(source) && steps++ < 250) Step();
            Assert.That(steps, Is.LessThan(250));
            Assert.That(em.GetComponentData<Barricade>(target).HitsRemaining, Is.EqualTo(opening ? 3 : 4));
            Assert.That(em.GetBuffer<BarricadeHitHistory>(target).Length, Is.EqualTo(opening ? 1 : 0));
            if (explosive) Assert.That(em.IsComponentEnabled<ExplosiveDetonationRequest>(source), Is.EqualTo(opening));
        }

        [Test]
        public void Cover003_ClosingOpeningAfterPreviewReflectsTheRealImpactWithoutCoreDamage()
        {
            Entity target = RotatingTarget(math.PI, out var cover, out _); Preview(); Punch();
            var motion = em.GetComponentData<RotatingCoverState>(cover); motion.Angle = 0; em.SetComponentData(cover, motion);
            int steps = 0; while (em.IsComponentEnabled<AuthoritativeLaunchPath>(source) && steps++ < 250) Step();
            Assert.That(steps, Is.LessThan(250));
            Assert.That(em.GetComponentData<AuthoritativeLaunchPath>(source).Contact, Is.EqualTo(cover));
            Assert.That(em.GetComponentData<Barricade>(target).HitsRemaining, Is.EqualTo(4));
            Assert.That(em.GetComponentData<PhysicsVelocity>(source).Linear.z, Is.LessThan(0));
            Assert.That(em.GetComponentData<EnemyLaunchState>(source).Owner, Is.EqualTo(EnemyLaunchOwner.Player));
            Assert.That(em.GetComponentData<EnemyLaunchState>(source).LaunchSequence, Is.EqualTo(1));
        }

        [TestCase(0f)]
        [TestCase(3f)]
        public void Cover003_ReturnShotUsesPlayerAtImpactInsteadOfOverwritingItWithTheSavedPath(float playerX)
        {
            Entity target = RotatingTarget(0, out var cover, out _); Preview(); Punch();
            float3 playerPosition = new float3(playerX,0,-2);
            using var query = em.CreateEntityQuery(typeof(PlayerSnapshot));
            em.SetComponentData(query.GetSingletonEntity(), new PlayerSnapshot { IsAvailable = true, Position = playerPosition });
            int steps = 0;
            while (em.GetComponentData<AuthoritativeLaunchPath>(source).Contact != cover && steps++ < 120) Step();
            Assert.That(steps, Is.LessThan(120));
            float3 position = em.GetComponentData<LocalTransform>(source).Position;
            float3 velocity = em.GetComponentData<PhysicsVelocity>(source).Linear;
            Assert.That(math.dot(math.normalizesafe(velocity.xz), math.normalizesafe((playerPosition - position).xz)), Is.GreaterThan(.9999f));
            Assert.That(math.length(velocity.xz), Is.EqualTo(12).Within(.001f));
            Assert.That(em.GetComponentData<Barricade>(target).HitsRemaining, Is.EqualTo(4));
            Assert.That(em.GetComponentData<EnemyLaunchState>(source).LaunchSequence, Is.EqualTo(1));
            Assert.That(em.IsComponentEnabled<AuthoritativeLaunchPath>(source), Is.EqualTo(playerX == 0));
        }

        [Test]
        public void Cover001_RealWalkingEnclosureStillBlocksUnlaunchedEnemies()
        {
            Entity target = RotatingTarget(math.PI, out _, out _);
            em.SetComponentData(source, new PhysicsVelocity { Linear = new float3(0,0,12) });
            for (int i = 0; i < 60; i++) PhysicsStep();
            Assert.That(em.GetComponentData<LocalTransform>(source).Position.z, Is.LessThan(3.1f));
            Assert.That(em.GetComponentData<Barricade>(target).HitsRemaining, Is.EqualTo(4));
        }

        [TestCase("gate")]
        [TestCase("destroying-gate")]
        [TestCase("shell")]
        [TestCase("core")]
        [TestCase("track")]
        [TestCase("cover")]
        [TestCase("pillar")]
        [TestCase("anchor")]
        [TestCase("gate", true)]
        [TestCase("core", true)]
        [TestCase("pillar", true)]
        public void Combat012_AuthoritativeContactsUseEveryObjectiveRule(string kind, bool unexpected = false)
        {
            Entity target = Gate(kind == "destroying-gate" || kind == "anchor" ? 1 : 3);
            Entity boss = Entity.Null;
            if (kind == "shell" || kind == "core")
                em.AddComponentData(target, new ShellTarget { RequiredExplosions = 2,
                    ExplosionsRemaining = kind == "shell" ? 2 : 0, CoreHealth = 30, CoreMaxHealth = 30 });
            if (kind == "track")
            {
                em.AddComponentData(target, new TrackObject { Start = new float3(0,0,3.05f), Direction = math.forward(),
                    Length = 10, RequiredNetHits = 5, SlideDuration = .4f });
                em.AddComponent<TrackObjectState>(target); em.AddComponent<PhysicsVelocity>(target);
                em.AddBuffer<TrackPushHistory>(target); em.AddBuffer<TrackPushContact>(target);
            }
            if (kind == "cover")
            {
                em.AddComponentData(target, new RotatingCover { ReflectionMultiplier = 1 }); em.AddBuffer<CoverReflection>(target);
            }
            if (kind == "pillar")
            {
                boss = em.CreateEntity(typeof(DinoBoss), typeof(LocalTransform));
                em.SetComponentData(boss, LocalTransform.FromPosition(new float3(0,0,10)));
                em.AddComponentData(target, new FallingPillar { Boss = boss });
                em.AddComponentData(target, new PillarTuning { InitialPosition = new float3(0,0,3.05f),
                    UnavailableCollider = EmptyCollider() }); em.AddBuffer<PillarFallHit>(target);
            }
            if (kind == "anchor")
            {
                boss = em.CreateEntity(typeof(FinaleBoss), typeof(FinaleTuning), typeof(LocalTransform));
                em.SetComponentData(boss, new FinaleBoss { Phase = FinalePhase.Flying });
                em.SetComponentData(boss, new FinaleTuning { FallSeconds = 2 });
                em.AddComponentData(target, new FinaleAnchor { Boss = boss });
            }
            // Last contact segment is shorter than one tick; its motor speed must not replace impact speed.
            using var settingsQuery = em.CreateEntityQuery(typeof(EnemyLaunchSettings));
            var settings = settingsQuery.GetSingleton<EnemyLaunchSettings>(); settings.MinimumDamageImpulse = 10;
            em.SetComponentData(settingsQuery.GetSingletonEntity(), settings);
            if (unexpected)
            {
                var initial = em.GetComponentData<LocalTransform>(target);
                em.SetComponentData(target, LocalTransform.FromPosition(new float3(30,0,3.05f)));
                Preview(); em.SetComponentData(target, initial); PhysicsStep();
            }
            else Preview();
            Punch();
            int steps = 0; while (em.IsComponentEnabled<AuthoritativeLaunchPath>(source) && steps++ < 250) Step();
            Assert.That(steps, Is.LessThan(250));
            if (kind == "gate") Assert.That(em.GetComponentData<Barricade>(target).HitsRemaining, Is.EqualTo(2));
            if (kind == "destroying-gate") Assert.That(em.GetComponentData<Barricade>(target).HitsRemaining, Is.Zero);
            if (kind == "shell") Assert.That(em.GetComponentData<ShellTarget>(target).CoreHealth, Is.EqualTo(30));
            if (kind == "core") Assert.That(em.GetComponentData<ShellTarget>(target).CoreHealth, Is.EqualTo(20));
            if (kind == "track") Assert.That(em.GetComponentData<TrackObjectState>(target).TargetStep, Is.EqualTo(1));
            if (kind == "cover") Assert.That(em.GetComponentData<LocalTransform>(source).Position.z, Is.LessThan(2));
            if (kind == "pillar")
            {
                Assert.That(em.GetComponentData<FallingPillar>(target).Phase, Is.EqualTo(PillarPhase.Falling));
                if (!unexpected) Assert.That(em.GetComponentData<LocalTransform>(source).Position.z, Is.GreaterThan(4), "A toppling pillar lets its source pass through.");
            }
            if (kind == "anchor") Assert.That(em.GetComponentData<FinaleBoss>(boss).Phase, Is.EqualTo(FinalePhase.Falling));
            if (kind == "gate" || kind == "core" || kind == "track")
                Assert.That(em.GetBuffer<BarricadeHitHistory>(target).Length, Is.EqualTo(1), "Exactly one hit for a continuous launch.");
        }

        [TestCase("head")]
        [TestCase("hand")]
        [TestCase("active-hand")]
        [TestCase("chicken")]
        [TestCase("rolling")]
        [TestCase("rolling-protected")]
        [TestCase("finale")]
        [TestCase("finale-protected")]
        [TestCase("dino")]
        [TestCase("head", true)]
        [TestCase("chicken", true)]
        [TestCase("rolling", true)]
        [TestCase("finale", true)]
        [TestCase("head", false, true)]
        [TestCase("chicken", false, true)]
        [TestCase("rolling", false, true)]
        [TestCase("head", false, false, 1)]
        [TestCase("chicken", false, false, 1)]
        [TestCase("rolling", false, false, 1)]
        [TestCase("head", false, false, 2)]
        [TestCase("chicken", false, false, 2)]
        [TestCase("rolling", false, false, 2)]
        public void Combat012_AuthoritativeContactsRespectBossDamageAndOwnership(string kind, bool unexpected = false, bool dasher = false, int safety = 0)
        {
            Entity boss = kind.StartsWith("finale") ? Gate() : Wall(3.05f, true);
            if (kind == "head" || kind.Contains("hand"))
            {
                em.AddComponentData(boss, new BossEncounter { Stage = 1, Cycle = BossCycle.Attacking });
                em.AddComponentData(boss, new BossTuning { StageTwoThreshold = .5f, StageThreeThreshold = .25f,
                    StaggerMinimumImpulse = kind == "active-hand" ? 1000 : .1f, StaggerDuration = 1,
                    ScatterSpeed = 15, ScatterDamage = 5 });
                em.AddComponentData(boss, new BossPart { Encounter = boss, Kind = kind == "head" ? BossPartKind.Head : BossPartKind.LeftHand });
                em.AddComponent<BossImpactFeedback>(boss); em.AddBuffer<CollisionDamageHistory>(boss);
                if (kind != "head")
                {
                    em.AddComponentData(boss, new BossHand { Phase = kind == "active-hand" ? BossHandPhase.Active : BossHandPhase.Ready,
                        AttackSequence = 1, Direction = math.forward() }); em.AddBuffer<BossScatterHistory>(boss);
                }
            }
            else if (kind == "chicken")
            {
                em.AddComponentData(boss, new ChickenBoss { Stage = 1, Phase = ChickenPhase.Pause });
                em.AddComponentData(boss, new ChickenTuning { BodyDamageMultiplier = 1, StageTwoThreshold = .5f, StageThreeThreshold = .25f });
                em.AddComponent<PhysicsVelocity>(boss); em.AddBuffer<ChickenHit>(boss); em.AddBuffer<ChickenHitHistory>(boss);
            }
            else if (kind.StartsWith("rolling"))
            {
                em.AddComponentData(boss, new RollingBoss { Stage = 1, Phase = kind == "rolling" ? RollingPhase.Pause : RollingPhase.Roll,
                    RollSequence = 1, Direction = math.forward() });
                em.AddComponentData(boss, new RollingTuning { BodyDamageMultiplier = 1, StageTwoThreshold = .5f, StageThreeThreshold = .25f,
                    LaunchSpeed = 15, CrowdDamage = 5 });
                em.AddComponent<PhysicsVelocity>(boss); em.AddBuffer<RollingHit>(boss); em.AddBuffer<RollingHitHistory>(boss);
                em.AddBuffer<RollingCrowdContact>(boss);
            }
            else if (kind.StartsWith("finale"))
            {
                em.AddComponentData(boss, new FinaleBoss { Health = 100, Phase = kind == "finale" ? FinalePhase.Downed : FinalePhase.Flying });
                em.AddComponentData(boss, new FinaleTuning { ReboundSafetyDegrees = 35 }); em.AddBuffer<FinaleHit>(boss);
            }
            else em.AddComponent<DinoBoss>(boss);
            em.AddComponentData(boss, new Health { Current = 100, Max = 100 });
            if (dasher)
            {
                em.AddComponentData(source, new DasherSettings { BossDamage = 23, DashSpeed = 12 }); em.AddComponent<DasherState>(source);
            }
            em.AddComponent<ExplosiveEnemyState>(source); em.AddComponent<ExplosiveDetonationRequest>(source);
            em.SetComponentEnabled<ExplosiveDetonationRequest>(source, false);
            if (safety != 0)
            {
                using var query = em.CreateEntityQuery(typeof(PlayerSnapshot));
                em.SetComponentData(query.GetSingletonEntity(), new PlayerSnapshot { IsAvailable = true, Position = new float3(0,0,safety == 1 ? 10 : -10) });
            }
            if (unexpected)
            {
                var initial = em.GetComponentData<LocalTransform>(boss);
                em.SetComponentData(boss, LocalTransform.FromPosition(new float3(30,0,3.05f)));
                Preview(); em.SetComponentData(boss, initial); PhysicsStep();
            }
            else Preview();
            var shown = em.GetBuffer<DisplayedLaunchPathPoint>(source);
            float3 endpoint = shown[shown.Length - 1].Value.Position;
            Punch();
            if (safety != 0)
            {
                using var query = em.CreateEntityQuery(typeof(PlayerSnapshot));
                em.SetComponentData(query.GetSingletonEntity(), new PlayerSnapshot { IsAvailable = true, Position = new float3(0,0,-10) });
            }
            int steps = 0; while (em.IsComponentEnabled<AuthoritativeLaunchPath>(source) && steps++ < 250) Step();
            Assert.That(steps, Is.LessThan(250));
            if (kind == "head" || kind == "chicken" || kind == "rolling")
                Assert.That(em.GetComponentData<Health>(boss).Current, Is.EqualTo(dasher ? 77 : 90));
            if (kind == "hand") Assert.That(em.GetComponentData<BossHand>(boss).Phase, Is.EqualTo(BossHandPhase.Staggered));
            if (kind == "active-hand" || kind == "rolling-protected")
                Assert.That(em.GetComponentData<EnemyLaunchState>(source).Owner, Is.EqualTo(EnemyLaunchOwner.Boss));
            if (kind == "finale") Assert.That(em.GetComponentData<FinaleBoss>(boss).Health, Is.EqualTo(90));
            if (kind == "finale-protected") Assert.That(em.GetComponentData<FinaleBoss>(boss).Health, Is.EqualTo(100));
            if (kind == "dino" || kind == "rolling-protected") Assert.That(em.GetComponentData<Health>(boss).Current, Is.EqualTo(100));
            if (kind == "head" || kind == "chicken" || kind == "rolling" || kind == "finale")
                Assert.That(em.IsComponentEnabled<ExplosiveDetonationRequest>(source), Is.True);
            if (safety == 2)
            {
                Assert.That(math.length(em.GetComponentData<PhysicsVelocity>(source).Linear), Is.Zero);
                Assert.That(math.distance(em.GetComponentData<LocalTransform>(source).Position, endpoint), Is.LessThan(.001f));
            }
            if (safety == 1)
            {
                float3 position = em.GetComponentData<LocalTransform>(source).Position;
                float3 velocity = em.GetComponentData<PhysicsVelocity>(source).Linear;
                Assert.That(math.length(velocity.xz), Is.GreaterThan(1));
                Assert.That(math.dot(velocity.xz, math.normalizesafe((new float3(0,0,-10) - position).xz)), Is.LessThanOrEqualTo(.0001f));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Info005_LaunchStopsAtTheLastDisplayedPointEvenWhenTheForecastBudgetEnds(bool bounce)
        {
            if (bounce) Wall(6, true);
            Preview();
            var displayed = em.GetBuffer<DisplayedLaunchPathPoint>(source);
            float3 endpoint = displayed[displayed.Length - 1].Value.Position;
            var segments = bridge.TrajectoryPreviewSegments;
            Assert.That(math.distance(segments[segments.Count - 1].End, endpoint), Is.LessThan(.001f));
            Punch(); int steps = 0;
            while (em.IsComponentEnabled<AuthoritativeLaunchPath>(source) && steps++ < 250) Step();
            Assert.That(steps, Is.LessThan(250));
            Assert.That(math.length(em.GetComponentData<PhysicsVelocity>(source).Linear), Is.Zero);
            for (int i = 0; i < 20; i++) Step(true);
            Assert.That(math.distance(em.GetComponentData<LocalTransform>(source).Position, endpoint), Is.LessThan(.001f));
            Assert.That(em.GetComponentData<EnemyLaunchState>(source).Phase, Is.Not.EqualTo(EnemyLaunchPhase.Launched));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Combat012_BossCanEscapeTheSavedContactUsingItsLivePoseOrVelocity(bool velocity)
        {
            Entity boss = Body(new float3(0,0,3), colliders[0], false);
            em.AddComponentData(boss, new ChickenBoss { Phase = ChickenPhase.Pause, Stage = 1 });
            em.AddComponentData(boss, new ChickenTuning { BodyDamageMultiplier = 1 });
            em.AddComponentData(boss, new Health { Current = 100, Max = 100 });
            em.AddComponent<PhysicsVelocity>(boss); em.AddBuffer<ChickenHit>(boss); em.AddBuffer<ChickenHitHistory>(boss);
            Preview(); Punch();
            while (em.GetComponentData<LocalTransform>(source).Position.z < 1.79f) Step();
            if (velocity) em.SetComponentData(boss, new PhysicsVelocity { Linear = new float3(30,0,0) });
            else em.SetComponentData(boss, LocalTransform.FromPosition(new float3(30,0,3)));
            int steps = 0; while (em.IsComponentEnabled<AuthoritativeLaunchPath>(source) && steps++ < 250) Step();
            Assert.That(steps, Is.LessThan(250));
            Assert.That(em.GetComponentData<Health>(boss).Current, Is.EqualTo(100));
            Assert.That(em.GetBuffer<ChickenHitHistory>(boss).Length, Is.Zero);
        }

        [Test]
        public void Barricade002_RicochetDamagesGateOnceWithTheCommittedLaunch()
        {
            Wall(3, true); Entity gate = Gate();
            em.SetComponentData(gate, LocalTransform.FromPosition(new float3(0,0,-2)));
            Preview(); Punch(); int steps = 0;
            while (em.IsComponentEnabled<AuthoritativeLaunchPath>(source) && steps++ < 250) Step();
            Assert.That(steps, Is.LessThan(250));
            Assert.That(em.GetComponentData<Barricade>(gate).HitsRemaining, Is.EqualTo(2));
            Assert.That(em.GetBuffer<BarricadeHitHistory>(gate).Length, Is.EqualTo(1));
        }

        [Test]
        public void Combat012_ForecastContactDoesNotDamageAGateThatMovedAway()
        {
            Entity gate = Gate(); Preview();
            em.SetComponentData(gate, LocalTransform.FromPosition(new float3(30,0,3.05f)));
            PhysicsStep(); Punch(); int steps = 0;
            while (em.IsComponentEnabled<AuthoritativeLaunchPath>(source) && steps++ < 250) Step();
            Assert.That(steps, Is.LessThan(250));
            Assert.That(em.GetComponentData<Barricade>(gate).HitsRemaining, Is.EqualTo(3));
            Assert.That(em.GetBuffer<BarricadeHitHistory>(gate).Length, Is.Zero);
        }

        [Test]
        public void Player003_LaterTargetRemovalDoesNotInvalidateTheDisplayedPath()
        {
            Entity target = Body(new float3(.3f, 0, 5), colliders[0], true);
            em.SetComponentData(source, new PunchAimAssistTarget { Target = target, LaunchSpeed = 12,
                LeadSeconds = .2f, LeadMaximumDistance = 2, Facing = math.forward(), DirectionLimitDegrees = 16 });
            Preview();
            em.DestroyEntity(target); PhysicsStep(); Punch();
            int steps = 0; while (em.IsComponentEnabled<AuthoritativeLaunchPath>(source) && steps++ < 250) Step();
            Assert.That(steps, Is.LessThan(250)); Assert.That(em.GetComponentData<PhysicsMassOverride>(source).IsKinematic, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Player003_NewSolidOrBossInterruptsTheMotorAndRestoresDynamicPhysics(bool boss)
        {
            Preview(); Entity obstacle = Wall(2);
            if (boss) em.AddComponent<ChickenBoss>(obstacle);
            PhysicsStep(); Punch();
            int steps = 0; while (em.IsComponentEnabled<AuthoritativeLaunchPath>(source) && steps++ < 30) Step();
            Assert.That(steps, Is.LessThan(30));
            Assert.That(em.GetComponentData<LocalTransform>(source).Position.z, Is.LessThanOrEqualTo(1.01f));
            Assert.That(em.GetComponentData<PhysicsMassOverride>(source).IsKinematic, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Player003_PathCompletionUsesNormalRecoveryOrDeferredDefeat(bool dead)
        {
            em.AddComponentData(source, new PhysicsDamping { Linear = 5 });
            Preview(); Punch();
            if (dead) em.SetComponentData(source, new Health());
            int steps = 0;
            while (em.GetComponentData<EnemyLaunchState>(source).Phase == EnemyLaunchPhase.Launched && steps++ < 200) Step(true);
            Assert.That(steps, Is.LessThan(200));
            Assert.That(em.GetComponentData<EnemyLaunchState>(source).Phase, Is.EqualTo(dead ? EnemyLaunchPhase.Defeated : EnemyLaunchPhase.Recovering));
            Assert.That(em.GetComponentData<PhysicsMassOverride>(source).IsKinematic, Is.Zero);
        }

        [Test]
        public void Player003_RepunchAndResetReleaseTheOldPhysicsOverride()
        {
            Preview(); Punch(); Step();
            Assert.That(em.GetComponentData<PhysicsMassOverride>(source).IsKinematic, Is.EqualTo(1));
            var oldStart = em.GetBuffer<CommittedLaunchPathPoint>(source)[0].Value.Position;
            Preview(); Punch();
            Assert.That(em.GetComponentData<EnemyLaunchState>(source).LaunchSequence, Is.EqualTo(2));
            Assert.That(em.GetComponentData<AuthoritativeLaunchPath>(source).NextPoint, Is.EqualTo(1));
            Assert.That(em.GetBuffer<CommittedLaunchPathPoint>(source)[0].Value.Position.z, Is.GreaterThan(oldStart.z));
            Assert.That(em.GetComponentData<PhysicsMassOverride>(source).IsKinematic, Is.Zero);
            Step();
            CommittedLaunchPlan.Reset(em, source);
            Assert.That(em.IsComponentEnabled<AuthoritativeLaunchPath>(source), Is.False);
            Assert.That(em.GetComponentData<PhysicsMassOverride>(source).IsKinematic, Is.Zero);
            Assert.That(em.GetBuffer<CommittedLaunchPathPoint>(source).Length, Is.Zero);
        }

        [Test]
        public void Player003_MovingCoverUsesItsCurrentPoseBeforeTheCollisionWorldRefreshes()
        {
            Entity cover = Wall(100);
            em.AddComponent<RotatingCover>(cover);
            Preview(); Punch(); Step(); Step(); Step();
            float z = em.GetComponentData<LocalTransform>(source).Position.z;
            em.SetComponentData(cover, LocalTransform.FromPosition(new float3(0, 0, z + 1)));
            Step();
            Assert.That(em.IsComponentEnabled<AuthoritativeLaunchPath>(source), Is.False);
            Assert.That(em.GetComponentData<PhysicsMassOverride>(source).IsKinematic, Is.Zero);
            Assert.That(em.GetComponentData<LocalTransform>(source).Position.z, Is.LessThanOrEqualTo(z + .01f));
        }

        [TestCase(100)]
        [TestCase(1000)]
        public void Player003_ProfileAuthoritativePathAcrossCrowdSizes(int count)
        {
            for (int i = 1; i < count; i++) Body(new float3(30 + i % 32 * 2, 0, i / 32 * 2), colliders[0], true);
            bridge.PublishPunchPreview(Vector3.back, Vector3.forward, 1, 2, 5, 0, 22, 12,
                previewStrength: 12, releaseAngleDegrees: 16, useDottedTrajectory: true, trajectoryMaximumSeconds: 8);
            Preview();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10; i++) world.GetOrCreateSystem<PresentationBridgeSystem>().Update(world.Unmanaged);
            timer.Stop();
            TestContext.WriteLine($"Authoritative preview: {count} bodies / one source, {timer.Elapsed.TotalMilliseconds / 10:F3} ms/update (warm Editor).");
            Punch();
            var original = em.GetComponentData<AuthoritativeLaunchPath>(source);
            var motor = world.GetOrCreateSystem<AuthoritativeLaunchMotorSystem>();
            var finish = world.GetOrCreateSystem<AuthoritativeLaunchFinishSystem>();
            timer.Restart();
            for (int i = 0; i < 100; i++)
            {
                em.SetComponentData(source, LocalTransform.FromPosition(float3.zero));
                em.SetComponentData(source, original);
                motor.Update(world.Unmanaged);
                var scheduled = em.GetComponentData<AuthoritativeLaunchPath>(source).ScheduledPosition;
                em.SetComponentData(source, LocalTransform.FromPosition(scheduled));
                finish.Update(world.Unmanaged);
            }
            timer.Stop();
            Assert.That(em.IsComponentEnabled<AuthoritativeLaunchPath>(source), Is.True);
            TestContext.WriteLine($"Authoritative motor/finish: {count} bodies / one source, {timer.Elapsed.TotalMilliseconds / 100:F3} ms/update (Editor; physics excluded).");
        }

        [Test]
        public void Player003_AuthoritativeMotorIsOrderedBeforeSweepsAndFinishBeforeRecovery()
        {
            var pre = world.GetOrCreateSystemManaged<GamePrePhysicsGroup>();
            var post = world.GetOrCreateSystemManaged<GamePostPhysicsGroup>();
            var members = new List<Type>();
            foreach (var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
            {
                if (type.Namespace == null || !type.Namespace.StartsWith("CrowdPunch.Systems")) continue;
                foreach (UpdateInGroupAttribute group in type.GetCustomAttributes(typeof(UpdateInGroupAttribute), true))
                    if (group.GroupType == typeof(GamePrePhysicsGroup) || group.GroupType == typeof(GamePostPhysicsGroup))
                    { members.Add(type); break; }
            }
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, members);
            var motor = world.GetOrCreateSystem<AuthoritativeLaunchMotorSystem>();
            var homing = world.GetOrCreateSystem<EnemyLaunchHomingSystem>();
            var pair = world.GetOrCreateSystem<LinkedPairConstraintSystem>();
            var ground = world.GetOrCreateSystem<EnemyGroundConstraintSystem>();
            var barrier = world.GetOrCreateSystem<BarricadeImpactSystem>();
            var cover = world.GetOrCreateSystem<RotatingCoverSystem>();
            pre.SortSystems(); using var before = pre.GetAllSystems();
            Assert.That(Index(before, homing), Is.LessThan(Index(before, motor)));
            Assert.That(Index(before, pair), Is.LessThan(Index(before, motor)));
            Assert.That(Index(before, cover), Is.LessThan(Index(before, motor)));
            Assert.That(Index(before, motor), Is.LessThan(Index(before, barrier)));
            var finish = world.GetOrCreateSystem<AuthoritativeLaunchFinishSystem>();
            var rebound = world.GetOrCreateSystem<CommittedLaunchReboundSystem>();
            var fallback = world.GetOrCreateSystem<EnemyRicochetAimSystem>();
            var recovery = world.GetOrCreateSystem<EnemyRecoverySystem>();
            post.SortSystems(); using var after = post.GetAllSystems();
            Assert.That(Index(after, rebound), Is.LessThan(Index(after, finish)));
            Assert.That(Index(after, fallback), Is.LessThan(Index(after, finish)));
            Assert.That(Index(after, finish), Is.LessThan(Index(after, recovery)));

            int Index(NativeList<SystemHandle> systems, SystemHandle handle)
            {
                for (int i = 0; i < systems.Length; i++) if (systems[i] == handle) return i;
                return -1;
            }
        }
    }
}
