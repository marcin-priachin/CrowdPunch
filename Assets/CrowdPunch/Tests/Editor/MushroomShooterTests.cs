using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Lifetime;
using CrowdPunch.Systems.Physics;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class MushroomShooterTests
    {
        private World world;
        private EntityManager em;
        private Entity player, arena, physicsEntity;
        private PhysicsWorld physics;
        private BlobAssetReference<Unity.Physics.Collider> ball;
        private readonly List<Entity> bodies = new List<Entity>();
        private PlayerEcsBridge bridge, previousBridge;
        private GameObject go;
        private int playerHits;
        private float playerDamage, playerProtection;
        private double now;

        [SetUp]
        public void SetUp()
        {
            world = new World("Mushroom Shooter");
            em = world.EntityManager;
            player = em.CreateEntity(typeof(PlayerSnapshot), typeof(PunchRequest));
            em.SetComponentEnabled<PunchRequest>(player, false);
            arena = em.CreateEntity(typeof(ArenaGroundPlane));
            physicsEntity = em.CreateEntity(typeof(PhysicsWorldSingleton));
            ball = Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .5f });
            MovePlayer(new float3(0, 1, 12));
            RebuildCollision();
            go = new GameObject("Test player");
            bridge = go.AddComponent<PlayerEcsBridge>();
            bridge.PublishPlayerHealth(100f, 100f);
            PlayerBridgeRegistry.TryGetBridge(out previousBridge);
            PlayerBridgeRegistry.Register(bridge);
            bridge.EnemyContactHitReceived += (damage, protection, push) =>
            {
                playerDamage = damage * bridge.MaxHealth;
                playerProtection = protection;
                playerHits++;
            };
            playerHits = 0;
            now = 0;
        }

        [TearDown]
        public void TearDown()
        {
            PlayerBridgeRegistry.Unregister(bridge);
            if (previousBridge != null) PlayerBridgeRegistry.Register(previousBridge);
            Object.DestroyImmediate(go);
            world.Dispose();
            physics.Dispose();
            ball.Dispose();
            bodies.Clear();
        }

        [Test]
        public void WarningCommitsAimAtClearStartAndBeamSharesItsCoveredLine()
        {
            var audioEntity = em.CreateSingletonBuffer<EnemyAttackAudioEvent>();
            var shooter = Shooter();
            var cover = Body(new float3(0, .95f, 5), false);
            RebuildCollision();
            Attack(.05f);
            Assert.That(AttackOf(shooter).Phase, Is.EqualTo(RangedAttackPhase.Ready));
            Assert.That(em.GetBuffer<EnemyAttackAudioEvent>(audioEntity).IsEmpty, Is.True);
            Move(cover, new float3(8, .95f, 5));
            RebuildCollision();
            Attack(.05f);
            Assert.That(AttackOf(shooter).Phase, Is.EqualTo(RangedAttackPhase.WindUp));
            var initial = Lane(shooter);
            Assert.That(initial.Direction, Is.EqualTo(math.forward()));
            Assert.That(initial.Length, Is.EqualTo(45f));
            MovePlayer(new float3(3, 1, 12));
            Move(shooter, new float3(.2f, 1, 0));
            Move(cover, new float3(0, .95f, 5));
            RebuildCollision();
            Attack(.15f);
            Assert.That(Lane(shooter).Direction, Is.EqualTo(initial.Direction));
            Assert.That(Lane(shooter).Origin, Is.EqualTo(initial.Origin));
            Attack(.59f);
            var warning = Lane(shooter);
            Assert.That(AttackOf(shooter).ProjectilesSpawned, Is.Zero);
            Assert.That(em.GetBuffer<EnemyAttackAudioEvent>(audioEntity).Length, Is.EqualTo(1));
            Assert.That(warning.Direction, Is.EqualTo(initial.Direction));
            Assert.That(warning.Origin, Is.EqualTo(initial.Origin));
            Assert.That(warning.Length, Is.LessThan(6f));
            Attack(.01f);
            using var q = em.CreateEntityQuery(typeof(MushroomBeam));
            var shot = q.GetSingleton<MushroomBeam>();
            Assert.That(shot.Direction, Is.EqualTo(warning.Direction));
            Assert.That(shot.Origin, Is.EqualTo(warning.Origin));
            Assert.That(shot.ExpiresAt - now, Is.EqualTo(.25).Within(.0001));
            var audio = em.GetBuffer<EnemyAttackAudioEvent>(audioEntity);
            Assert.That(audio.Length, Is.EqualTo(2));
            Assert.That(audio[0].Kind, Is.EqualTo(EnemyAttackAudioKind.MushroomWindup));
            Assert.That(audio[1].Kind, Is.EqualTo(EnemyAttackAudioKind.MushroomFire));
            Assert.That(audio[1].Position, Is.EqualTo(warning.Origin));
            Beam(.01f);
            Assert.That(q.GetSingleton<MushroomBeam>().Length, Is.EqualTo(warning.Length).Within(.001f));
            Beam(.25f);
            Assert.That(q.CalculateEntityCount(), Is.Zero);
            Assert.That(playerHits, Is.Zero);
        }

        [Test]
        public void WarningDrawsOneTranslucentStrip()
        {
            using var warning = new MushroomLanePresentation(go.transform);
            warning.Begin();
            warning.Add(new ProjectileVisualSample
            {
                Kind = ProjectileVisualKind.MushroomLane, Velocity = Vector3.forward, LaneLength = 14f, Radius = .55f
            });
            warning.End();
            var mesh = go.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.That(go.GetComponentInChildren<MeshRenderer>().enabled, Is.True);
            Assert.That(mesh.vertexCount, Is.EqualTo(4));
            foreach (var color in mesh.colors)
                Assert.That(color.a, Is.EqualTo(.24f).Within(.001f));
        }

        [TestCase(.1f)]
        [TestCase(.74f)]
        public void LaunchCancelsWarningAndCooldownWaitsForRecovery(float windUpElapsed)
        {
            var shooter = Shooter();
            Attack(.05f);
            Attack(windUpElapsed);
            Assert.That(AttackOf(shooter).Phase, Is.EqualTo(RangedAttackPhase.WindUp));
            Phase(shooter, EnemyLaunchPhase.Launched);
            Attack(1f);
            Assert.That(AttackOf(shooter).CancelledWindUps, Is.EqualTo(1u));
            Assert.That(AttackOf(shooter).ProjectilesSpawned, Is.Zero);
            Assert.That(Lane(shooter).Length, Is.Zero);
            Phase(shooter, EnemyLaunchPhase.Recovering);
            Attack(3f);
            Assert.That(AttackOf(shooter).SecondsRemaining, Is.EqualTo(2f));
            Phase(shooter, EnemyLaunchPhase.Active);
            Attack(1.95f);
            Assert.That(AttackOf(shooter).Phase, Is.EqualTo(RangedAttackPhase.Cooldown));
            Attack(.05f);
            Assert.That(AttackOf(shooter).Phase, Is.EqualTo(RangedAttackPhase.WindUp));
        }

        [Test]
        public void PunchingShooterReplacesHostileBeamWithEnemyOnlyBeamAlongLaunch()
        {
            MovePlayer(new float3(0, 1, 0));
            var shooter = Shooter(new float3(0, .95f, 2));
            var first = Body(new float3(0, .95f, 4), true);
            var second = Body(new float3(0, .95f, 5), true);
            em.SetComponentData(second, new Health { Current = 10, Max = 10 });
            var armored = Body(new float3(0, .95f, 6), true);
            em.AddComponentData(armored, new EnemyArmor { Stages = 1 });
            Body(new float3(0, .95f, 8), false);
            var behindCover = Body(new float3(0, .95f, 10), true);
            RebuildCollision();
            var hostile = HostileBeam(shooter, new float3(0, .95f, 2), -math.forward());
            int bursts = 0;
            bridge.ProjectileBurstReceived += (kind, point, _, _, owner) =>
            {
                bursts++;
                Assert.That(kind, Is.EqualTo(ProjectileVisualKind.MushroomBeam));
                Assert.That(owner, Is.EqualTo(EnemyLaunchOwner.Player));
            };

            em.SetComponentData(player, new PunchRequest
            {
                Origin = new float3(0, .95f, 0), Direction = new float3(0, .4f, 1),
                Range = 3f, Radius = 1f, Strength = 12f, Damage = 5f, Sequence = 7u
            });
            em.SetComponentEnabled<PunchRequest>(player, true);
            world.GetOrCreateSystem<PunchDetectionSystem>().Update(world.Unmanaged);

            var punch = em.GetComponentData<PunchRequest>(player);
            Assert.That(punch.HitEnemy, Is.True);
            Assert.That(punch.RedirectedProjectile, Is.False);
            Assert.That(em.GetComponentData<EnemyLaunchState>(shooter).Phase, Is.EqualTo(EnemyLaunchPhase.Launched));
            Assert.That(em.GetComponentData<EnemyLaunchState>(first).Phase, Is.EqualTo(EnemyLaunchPhase.Active));

            Move(shooter, new float3(0, .95f, 3)); // A physics step can move the Shooter past the impact point.
            MovePlayer(new float3(0, 1, 3));
            Attack(.01f);
            Beam(.01f);
            Assert.That(em.Exists(hostile), Is.False);
            using var beams = em.CreateEntityQuery(typeof(MushroomBeam));
            var returned = beams.GetSingleton<MushroomBeam>();

            Assert.That(returned.Owner, Is.EqualTo(EnemyLaunchOwner.Player));
            Assert.That(returned.Origin.z, Is.EqualTo(2f));
            Assert.That(returned.Direction, Is.EqualTo(math.forward()));
            Assert.That(returned.Length, Is.LessThan(6f));
            Assert.That(em.GetComponentData<Health>(first).Current, Is.Zero);
            Assert.That(em.GetComponentData<EnemyDamageState>(first).DefeatOwner, Is.EqualTo(EnemyLaunchOwner.Player));
            Assert.That(em.GetComponentData<EnemyDamageState>(first).DefeatPunchSequence, Is.EqualTo(7u));
            Assert.That(em.GetComponentData<Health>(second).Current, Is.EqualTo(5f));
            Assert.That(em.GetComponentData<EnemyDamageState>(second).PlayDefeatAnimation, Is.Zero);
            Assert.That(em.GetComponentData<Health>(armored).Current, Is.EqualTo(5f));
            Assert.That(em.GetComponentData<Health>(behindCover).Current, Is.EqualTo(5f));

            Beam(.05f);
            Assert.That(bursts, Is.EqualTo(3), "MUSHROOM-003: two damaged enemies and one cover contact.");
            Assert.That(em.GetComponentData<Health>(second).Current, Is.EqualTo(5f));
            Assert.That(em.GetComponentData<EnemyLaunchState>(second).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
            Assert.That(playerHits, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LethalBeamWaitsForDefeatAnimationBeforeLevelClear(bool launched)
        {
            var target = Body(new float3(0f, .95f, 4f), true);
            em.AddComponentData(target, new EnemyLandingAnimation { Duration = .8f });
            em.AddComponent<PhysicsVelocity>(target);
            if (launched)
                em.SetComponentData(target, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched, LaunchSequence = 1 });
            var sequence = em.CreateEntity(typeof(EnemyWaveSequence), typeof(EnemyWaveEncounterComplete));
            em.SetComponentData(sequence, new EnemyWaveSequence
            {
                RunGeneration = 1, UndefeatedCount = 1, Phase = EnemyWaveRuntimePhase.AwaitingActivation
            });
            em.SetComponentEnabled<EnemyWaveEncounterComplete>(sequence, false);
            em.AddBuffer<EnemyWaveDefinition>(sequence).Add(default);
            em.AddComponentData(target, new EnemyWaveOwnership { Sequence = sequence, RunGeneration = 1 });
            RebuildCollision();

            var beam = em.CreateEntity(typeof(MushroomBeam));
            em.SetComponentData(beam, new MushroomBeam
            {
                Arena = arena, Origin = new float3(0f, .95f, 0f), Direction = math.forward(), Owner = EnemyLaunchOwner.Player,
                Radius = .55f, Range = 20f, ExpiresAt = now + .25d, Damage = 5f, PunchSequence = 7
            });
            em.AddBuffer<BodyProjectileHit>(beam);
            Beam(.02f);
            Assert.That(em.GetComponentData<EnemyDamageState>(target).PlayDefeatAnimation, Is.EqualTo(1));

            var count = world.GetOrCreateSystem<EnemyWaveDefeatCountSystem>();
            if (launched)
            {
                Assert.That(em.GetComponentData<EnemyLaunchState>(target).Phase, Is.EqualTo(EnemyLaunchPhase.Launched));
                count.Update(world.Unmanaged);
                Assert.That(em.GetComponentData<EnemyWaveSequence>(sequence).UndefeatedCount, Is.EqualTo(1));
                Beam(.3f);
                Assert.That(em.Exists(beam), Is.False);
                em.CreateSingleton(new EnemyLaunchSettings { UsefulMomentumSpeed = 1f });
                world.GetOrCreateSystem<EnemyRecoverySystem>().Update(world.Unmanaged);
            }
            Assert.That(em.GetComponentData<EnemyLaunchState>(target).Phase, Is.EqualTo(EnemyLaunchPhase.Defeated));
            count.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyWaveSequence>(sequence).UndefeatedCount, Is.Zero);
            world.GetOrCreateSystem<DefeatedEnemyLifecycleSystem>().Update(world.Unmanaged);
            var respawn = em.GetComponentData<RespawnRequest>(target);
            Assert.That(respawn.PoolNotBefore, Is.EqualTo(now + .8d).Within(.0001d));

            em.SetComponentEnabled<EnemyWaveEncounterComplete>(sequence, true);
            var completion = world.GetOrCreateSystemManaged<GauntletCompletionSystem>();
            uint before = GauntletCompletionRegistry.Sequence;
            world.SetTime(new TimeData(now + .4d, .02f));
            completion.Update();
            Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before));
            world.SetTime(new TimeData(now + .82d, .02f));
            completion.Update();
            Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(before + 1));
        }

        [Test]
        public void HostileBeamIgnoresEnemiesAndHitsPlayerAtMostOnceDuringItsHold()
        {
            var shooter = Shooter();
            var enemy = Body(new float3(0, 1, 5), true);
            var contacts = new List<Vector3>();
            bridge.ProjectileBurstReceived += (kind, point, _, _, owner) =>
            {
                contacts.Add(point);
                Assert.That(kind, Is.EqualTo(ProjectileVisualKind.MushroomBeam));
                Assert.That(owner, Is.EqualTo(EnemyLaunchOwner.Enemy));
            };
            RebuildCollision();
            MovePlayer(new float3(2, 1, 12));
            var beam = HostileBeam(shooter, new float3(0, 1, 0), math.forward());
            Beam(.02f);
            Assert.That(playerHits, Is.Zero);
            Assert.That(contacts, Is.Empty);
            MovePlayer(new float3(0, 1, 12));
            Beam(.02f);
            Beam(.02f);
            Assert.That(em.Exists(beam), Is.True);
            Assert.That(em.GetComponentData<Health>(enemy).Current, Is.EqualTo(5f));
            Assert.That(playerHits, Is.EqualTo(1));
            Assert.That(playerDamage, Is.EqualTo(10f));
            Assert.That(playerProtection, Is.EqualTo(.5f));
            Assert.That(contacts.Count, Is.EqualTo(1), "MUSHROOM-002: holding the beam does not repeat player VFX.");
            Assert.That(contacts[0].z, Is.EqualTo(11.5f).Within(.03f));
            Body(new float3(0, 1, 9), false);
            RebuildCollision();
            Beam(.02f);
            Beam(.02f);
            Assert.That(contacts.Count, Is.EqualTo(2), "MUSHROOM-002: cover contact emits once while blocked.");
            Assert.That(contacts[1].z, Is.EqualTo(8.5f).Within(.03f));
            Beam(.20f);
            Assert.That(em.Exists(beam), Is.False);
        }

        [TestCase(EnemyLaunchOwner.Player, 1)]
        [TestCase(EnemyLaunchOwner.Enemy, 0)]
        public void PhysicsImpactEmitsBeamForPlayerOwnedLaunches(EnemyLaunchOwner owner, int expectedBeams)
        {
            em.DestroyEntity(physicsEntity); // The live physics group owns this singleton in this test.
            var shooter = Shooter(new float3(0, .95f, 1.04f));
            var source = Body(new float3(0, .95f, 0), true);
            var material = Unity.Physics.Material.Default;
            material.CollisionResponse = CollisionResponsePolicy.CollideRaiseCollisionEvents;
            using var sphere = Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .5f },
                CollisionFilter.Default, material);
            foreach (var body in new[] { source, shooter })
            {
                em.AddComponent<LocalToWorld>(body);
                em.AddComponent<Simulate>(body);
                em.AddSharedComponent(body, new PhysicsWorldIndex());
                em.AddComponentData(body, new PhysicsCollider { Value = sphere });
                em.AddComponentData(body, PhysicsMass.CreateDynamic(sphere.Value.MassProperties, 1f));
                em.AddComponent<PhysicsVelocity>(body);
            }
            em.SetComponentData(source, new EnemyLaunchState
            {
                Phase = EnemyLaunchPhase.Launched, Owner = owner, OriginatingPunchSequence = 13u, FeedbackChainDepth = 1
            });
            em.SetComponentData(source, new PhysicsVelocity { Linear = math.forward() * 10f });
            em.CreateSingleton(new EnemyLaunchSettings { MinimumPropagationImpulse = .1f });
            var step = PhysicsStep.Default;
            step.Gravity = float3.zero;
            em.CreateSingleton(step);

            world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            world.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
            var systems = new List<System.Type>();
            foreach (var type in DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default))
                if (type.Namespace == "Unity.Physics.Systems") systems.Add(type);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            var simulation = world.GetOrCreateSystemManaged<PhysicsSystemGroup>();
            for (int i = 0; i < 8 && em.GetComponentData<EnemyLaunchState>(shooter).Phase == EnemyLaunchPhase.Active; i++)
            {
                world.SetTime(new TimeData(now += .02, .02f));
                simulation.Update();
                world.GetOrCreateSystem<EnemyLaunchCollisionSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();
            }
            Assert.That(em.GetComponentData<EnemyLaunchState>(shooter).Phase, Is.EqualTo(EnemyLaunchPhase.Launched));
            world.GetOrCreateSystem<MushroomShooterAttackSystem>().Update(world.Unmanaged);
            using var beams = em.CreateEntityQuery(typeof(MushroomBeam));
            Assert.That(beams.CalculateEntityCount(), Is.EqualTo(expectedBeams));
            if (expectedBeams == 1)
            {
                var beam = beams.GetSingleton<MushroomBeam>();
                Assert.That(beam.Owner, Is.EqualTo(EnemyLaunchOwner.Player));
                Assert.That(beam.Direction.z, Is.GreaterThan(.99f));
                Assert.That(beam.PunchSequence, Is.EqualTo(13u));
            }
        }

        [Test]
        public void LowShotSourceAnchorsTheWarningAndReleaseWhileCoverStillStopsBeam()
        {
            var shooter = Shooter(new float3(0, 1, 0));
            em.AddComponentData(shooter, new MushroomShotSource { LocalPosition = new float3(0, -.468f, .637f) });
            MovePlayer(new float3(12, 1, 0));
            var floor = Body(new float3(0, -.5f, 0), false);
            using var floorShape = Unity.Physics.BoxCollider.Create(new BoxGeometry
            {
                Size = new float3(50, 1, 50), Orientation = quaternion.identity
            });
            RebuildCollision(floor, floorShape);
            Attack(.05f);
            var origin = Lane(shooter).Origin;
            Assert.That(math.distance(origin, new float3(.637f, .532f, 0)), Is.LessThan(.00001f));
            Attack(.30f);
            Assert.That(Lane(shooter).Origin, Is.EqualTo(origin));
            Body(new float3(4, .532f, 0), false);
            RebuildCollision(floor, floorShape);
            Attack(.45f);
            using var shots = em.CreateEntityQuery(typeof(MushroomBeam));
            var shot = shots.GetSingletonEntity();
            Assert.That(em.GetComponentData<MushroomBeam>(shot).Origin, Is.EqualTo(origin));
            Beam(.025f);
            Assert.That(em.Exists(shot), Is.True);
            Assert.That(em.GetComponentData<MushroomBeam>(shot).Length, Is.InRange(1f, 3.5f));
            Beam(.25f);
            Assert.That(em.Exists(shot), Is.False);
            Assert.That(playerHits, Is.Zero);
        }

        [TestCase("EnemyMushroomShooter", EnemyAnimationProfile.MushroomShooter)]
        [TestCase("EnemyAlienSpike", EnemyAnimationProfile.AlienSpike)]
        public void EnemyPrefabUsesItsOwnAnimationProfile(string name, EnemyAnimationProfile expected)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/CrowdPunch/Prefabs/{name}.prefab");
            Assert.That(prefab, Is.Not.Null);
            var animators = prefab.GetComponentsInChildren<CrowdPunch.Authoring.EnemyAnimationAuthoring>(true);
            Assert.That(animators, Is.Not.Empty);
            foreach (var animation in animators)
            {
                Assert.That(animation.Profile, Is.EqualTo(expected));
                Assert.That(animation.Samples, Is.Not.Null);
            }
        }

        [Test]
        public void Barricade008_ShooterCannotStartWarningAcrossRoomBoundary()
        {
            var shooter = Shooter();
            var room = em.CreateEntity(typeof(BarricadeRoom));
            em.SetComponentData(room, new BarricadeRoom { Minimum = new float2(-8), Maximum = new float2(8), Activated = 1 });
            em.AddComponentData(shooter, new EnemyWaveOwnership { Sequence = room });
            RebuildCollision();
            Attack(.05f);
            Assert.That(AttackOf(shooter).Phase, Is.EqualTo(RangedAttackPhase.Ready));
            Assert.That(Lane(shooter).Length, Is.Zero);

            MovePlayer(new float3(0, 1, 6));
            Attack(.05f);
            Assert.That(AttackOf(shooter).Phase, Is.EqualTo(RangedAttackPhase.WindUp));
        }

        private Entity Shooter(float3 position = default)
        {
            var entity = Body(position, true);
            em.AddComponentData(entity, MushroomShooterSettings.Default);
            em.AddComponent<MushroomWarningLane>(entity);
            em.AddBuffer<MushroomBeamImpact>(entity);
            em.AddComponent<ExternalImpulse>(entity);
            em.SetComponentEnabled<ExternalImpulse>(entity, false);
            em.AddComponentData(entity, new RangedAttackState { Phase = RangedAttackPhase.Ready });
            em.AddComponentData(entity, new RangedEnemySettings
            {
                ProjectileRadius = .55f, EngagementRange = 32, Cooldown = 2, WindUpDuration = .75f,
                ProjectileDamage = 10, PlayerInvincibilitySeconds = .5f, ProjectilePlayerLayers = 1
            });
            return entity;
        }

        private Entity Body(float3 position, bool enemy)
        {
            var entity = em.CreateEntity(typeof(LocalTransform));
            Move(entity, position);
            if (enemy)
            {
                em.AddComponent<Enemy>(entity);
                em.AddComponent<EnemyTier>(entity);
                em.AddComponent<EnemyLaunchState>(entity);
                em.AddComponent<EnemyDamageState>(entity);
                em.AddComponentData(entity, new Health { Current = 5, Max = 5 });
                em.AddComponent<RespawnRequest>(entity);
                em.SetComponentEnabled<RespawnRequest>(entity, false);
                em.AddComponent<DeathRequest>(entity);
                em.SetComponentEnabled<DeathRequest>(entity, false);
                em.AddComponent<DamageRequest>(entity);
                em.SetComponentEnabled<DamageRequest>(entity, false);
                em.AddComponent<EnemyHealthBarVisibility>(entity);
                em.SetComponentEnabled<EnemyHealthBarVisibility>(entity, false);
            }
            bodies.Add(entity);
            return entity;
        }

        private Entity HostileBeam(Entity shooter, float3 origin, float3 direction)
        {
            var entity = em.CreateEntity(typeof(MushroomBeam));
            em.SetComponentData(entity, new MushroomBeam
            {
                Arena = arena, Emitter = shooter, Owner = EnemyLaunchOwner.Enemy, Origin = origin,
                Direction = direction, Radius = .55f, Range = 45, ExpiresAt = now + .25,
                Damage = 10, PlayerInvincibility = .5f, PlayerLayers = 1
            });
            return entity;
        }

        private void RebuildCollision(Entity floor = default, BlobAssetReference<Unity.Physics.Collider> floorShape = default)
        {
            if (physics.Bodies.IsCreated) physics.Dispose();
            physics = new PhysicsWorld(bodies.Count, 0, 0);
            var rigid = physics.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                var pose = em.GetComponentData<LocalTransform>(bodies[i]);
                rigid[i] = new RigidBody { Entity = bodies[i], Collider = bodies[i] == floor ? floorShape : ball,
                    WorldFromBody = new RigidTransform(pose.Rotation, pose.Position), Scale = 1f };
            }
            physics.CollisionWorld.BuildBroadphase(ref physics, .05f, float3.zero, true);
            em.SetComponentData(physicsEntity, new PhysicsWorldSingleton { PhysicsWorld = physics });
        }

        private void Move(Entity entity, float3 position) => em.SetComponentData(entity, LocalTransform.FromPosition(position));
        private void MovePlayer(float3 position) => em.SetComponentData(player,
            new PlayerSnapshot { Position = position, IsAvailable = true, Radius = .5f, CollisionLayer = 1 });
        private void Phase(Entity entity, EnemyLaunchPhase phase) => em.SetComponentData(entity, new EnemyLaunchState { Phase = phase });
        private RangedAttackState AttackOf(Entity entity) => em.GetComponentData<RangedAttackState>(entity);
        private MushroomWarningLane Lane(Entity entity) => em.GetComponentData<MushroomWarningLane>(entity);
        private void Attack(float dt)
        {
            world.SetTime(new TimeData(now += dt, dt));
            world.GetOrCreateSystem<MushroomShooterAttackSystem>().Update(world.Unmanaged);
        }
        private void Beam(float dt)
        {
            world.SetTime(new TimeData(now += dt, dt));
            world.GetOrCreateSystem<MushroomBeamSystem>().Update(world.Unmanaged);
        }
    }
}
