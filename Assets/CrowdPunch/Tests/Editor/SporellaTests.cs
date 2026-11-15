using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Lifetime;
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
    public sealed class SporellaTests
    {
        private World world;
        private EntityManager em;
        private Entity player, arena, physicsEntity;
        private PhysicsWorld physics;
        private BlobAssetReference<Unity.Physics.Collider> collider;
        private readonly List<Entity> bodies = new();
        private PlayerEcsBridge bridge, previousBridge;
        private GameObject playerObject;
        private int playerHits;
        private double now;

        [SetUp]
        public void SetUp()
        {
            world = new World("Sporella");
            em = world.EntityManager;
            player = em.CreateEntity(typeof(PlayerSnapshot), typeof(PunchRequest));
            em.SetComponentEnabled<PunchRequest>(player, false);
            arena = em.CreateEntity(typeof(ArenaGroundPlane));
            physicsEntity = em.CreateEntity(typeof(PhysicsWorldSingleton));
            collider = Unity.Physics.SphereCollider.Create(new SphereGeometry { Radius = .45f });
            playerObject = new GameObject("Sporella test player");
            bridge = playerObject.AddComponent<PlayerEcsBridge>();
            bridge.PublishPlayerHealth(100f, 100f);
            PlayerBridgeRegistry.TryGetBridge(out previousBridge);
            PlayerBridgeRegistry.Register(bridge);
            bridge.EnemyContactHitReceived += (_, _, _) => playerHits++;
            playerHits = 0;
            now = 0;
            MovePlayer(new float3(0, .94f, 11.24f));
            RebuildPhysics();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerBridgeRegistry.Unregister(bridge);
            if (previousBridge != null) PlayerBridgeRegistry.Register(previousBridge);
            Object.DestroyImmediate(playerObject);
            world.Dispose();
            physics.Dispose();
            collider.Dispose();
            bodies.Clear();
        }

        [Test]
        public void OpenSideLaneAllowsWindupAndNewCoverCanStillStopTheShot()
        {
            var shooter = Shooter();
            var cover = Body(new float3(0, .94f, 3.74f));
            RebuildPhysics();
            Tick<SporellaAttackSystem>(.02f);
            Assert.That(em.GetComponentData<RangedAttackState>(shooter).Phase, Is.EqualTo(RangedAttackPhase.Ready));

            Move(cover, new float3(0, .94f, 10f)); // The center lane is blocked while both outside lanes are open.
            RebuildPhysics();
            Tick<SporellaAttackSystem>(.02f);
            Assert.That(em.GetComponentData<RangedAttackState>(shooter).Phase, Is.EqualTo(RangedAttackPhase.WindUp));

            Move(cover, new float3(0, .94f, 3.74f));
            RebuildPhysics();
            Tick<SporellaAttackSystem>(.64f);
            Assert.That(CountShots(), Is.Zero);
            Tick<SporellaAttackSystem>(.01f);
            Assert.That(CountShots(), Is.EqualTo(1));
            Tick<SporellaProjectileSystem>(1f); // A large step must not skip the cover.
            Assert.That(CountShots(), Is.Zero);
            Assert.That(playerHits, Is.Zero);
        }

        [Test]
        public void FirstShotLocksTwoLeftToRightPassesOnTheSampledTarget()
        {
            var shooter = Shooter();
            Tick<SporellaAttackSystem>(.05f);
            var sampled = new float3(2, .94f, 14f);
            var shooterAtRelease = new float3(-2, 0, 1);
            MovePlayer(sampled);
            Move(shooter, shooterAtRelease);
            Tick<SporellaAttackSystem>(.65f);

            float3 forward = math.normalize(new float3(sampled.x - shooterAtRelease.x, 0, sampled.z - shooterAtRelease.z));
            float3 right = math.cross(math.up(), forward);
            float3 origin = new float3(shooterAtRelease.x, .94f, shooterAtRelease.z) + forward * 1.24f;
            float[] expectedOffsets = { -1.5f, 0, 1.5f, -1.5f, 0, 1.5f };
            var seen = new HashSet<Entity>();
            using var query = em.CreateEntityQuery(typeof(SporellaProjectile));

            MovePlayer(new float3(-8f, .94f, 5f));
            Move(shooter, new float3(8f, 0, 0));
            for (int i = 0; i < expectedOffsets.Length; i++)
            {
                if (i > 0)
                {
                    Tick<SporellaAttackSystem>(.17f);
                    Assert.That(CountShots(), Is.EqualTo(i));
                    Tick<SporellaAttackSystem>(.01f);
                }

                Assert.That(CountShots(), Is.EqualTo(i + 1));
                using var entities = query.ToEntityArray(Allocator.Temp);
                foreach (var entity in entities)
                {
                    if (!seen.Add(entity)) continue;
                    var shot = em.GetComponentData<SporellaProjectile>(entity);
                    Assert.That(shot.Speed, Is.EqualTo(14f));
                    Assert.That(math.distance(PositionOf(entity), origin), Is.LessThan(.001f));
                    float travel = math.dot(sampled - origin, forward) / math.dot(shot.Direction, forward);
                    float3 aimPoint = origin + shot.Direction * travel;
                    Assert.That(math.distance(aimPoint, sampled + right * expectedOffsets[i]), Is.LessThan(.001f));
                }
            }

            Tick<SporellaProjectileSystem>(.2f);
            Tick<SporellaProjectileSystem>(.2f);
            foreach (var entity in seen)
            {
                var shot = em.GetComponentData<SporellaProjectile>(entity);
                Assert.That(math.distance(PositionOf(entity), origin + shot.Direction * 5.6f), Is.LessThan(.001f));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LaunchCancelsOnlyUnfiredNormalShots(bool released)
        {
            var shooter = Shooter();
            Tick<SporellaAttackSystem>(.02f);
            if (released) Tick<SporellaAttackSystem>(.65f);
            em.SetComponentData(shooter, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
            Tick<SporellaAttackSystem>(1f);
            Assert.That(CountShots(), Is.EqualTo(released ? 1 : 0));
            Assert.That(em.GetComponentData<SporellaNormalBurst>(shooter).ShotsRemaining, Is.Zero);
        }

        [TestCase(SporellaShotKind.EnemyShot)]
        [TestCase(SporellaShotKind.ImpactShot)]
        public void RedirectWinsPunchOverlapAndPiercesOncePerEnemyWithoutLaunching(SporellaShotKind kind)
        {
            MovePlayer(new float3(0, .94f, 0));
            var first = Body(new float3(0, .94f, 2f), true);
            var second = Body(new float3(0, .94f, 4f), true);
            var armored = Body(new float3(0, .94f, 6f), true);
            em.AddComponentData(armored, new EnemyArmor { Stages = 1 });
            var boss = Body(new float3(0, .94f, 8f), true);
            em.SetComponentData(boss, new EnemyTier { Value = EnemyCombatTier.Boss });
            var objective = Body(new float3(0, .94f, 10f), true);
            em.AddComponent<Barricade>(objective);
            Body(new float3(0, .94f, 12f));
            var behindCover = Body(new float3(0, .94f, 14f), true);
            RebuildPhysics();

            var shot = Shot(new float3(0, .94f, .4f), -math.forward(), kind);
            Punch();
            Assert.That(em.GetComponentData<PunchRequest>(player).RedirectedProjectile, Is.True);
            var returned = em.GetComponentData<SporellaProjectile>(shot);
            Assert.That(returned.Kind, Is.EqualTo(SporellaShotKind.PunchReturn));
            Assert.That(returned.Direction, Is.EqualTo(math.forward()));
            Assert.That(PositionOf(shot), Is.EqualTo(new float3(0, .94f, .4f)));
            Assert.That(HealthOf(first), Is.EqualTo(30f));
            Assert.That(em.GetComponentData<EnemyLaunchState>(first).Phase, Is.EqualTo(EnemyLaunchPhase.Active));

            Tick<SporellaProjectileSystem>(.1f);
            Assert.That(playerHits, Is.Zero);
            Tick<SporellaProjectileSystem>(1f);
            Assert.That(HealthOf(first), Is.EqualTo(20f));
            Assert.That(HealthOf(second), Is.EqualTo(20f));
            Assert.That(em.GetComponentData<EnemyDamageState>(first).LastDamageOwner, Is.EqualTo(EnemyLaunchOwner.Player));
            Assert.That(em.GetComponentData<EnemyLaunchState>(second).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
            Assert.That(HealthOf(armored), Is.EqualTo(30f));
            Assert.That(HealthOf(boss), Is.EqualTo(30f));
            Assert.That(HealthOf(objective), Is.EqualTo(30f));
            Assert.That(HealthOf(behindCover), Is.EqualTo(30f));
            Assert.That(em.Exists(shot), Is.False);
        }

        [TestCase(SporellaShotKind.EnemyShot)]
        [TestCase(SporellaShotKind.ImpactShot)]
        public void EnemyAndImpactShotsRespectEligibilityAndCanHitPlayer(SporellaShotKind kind)
        {
            var origin = new float3(0, .94f, 0);
            float3 direction = math.forward();
            var first = Body(origin + direction * 2f, true);
            var second = Body(origin + direction * 5f, true);
            MovePlayer(origin + direction * 8f);
            RebuildPhysics();
            Shot(origin, math.forward(), kind);
            Tick<SporellaProjectileSystem>(1f);

            bool impact = kind == SporellaShotKind.ImpactShot;
            Assert.That(HealthOf(first), Is.EqualTo(impact ? 20f : 30f));
            Assert.That(HealthOf(second), Is.EqualTo(30f));
            Assert.That(playerHits, Is.EqualTo(impact ? 0 : 1));
            Assert.That(CountShots(), Is.Zero);

            if (!impact) return;
            Shot(origin, math.forward(), kind);
            MovePlayer(origin + direction * 1f);
            Tick<SporellaProjectileSystem>(1f);
            Assert.That(playerHits, Is.EqualTo(1));
        }

        [Test]
        public void ReturningShotCanHitPlayerAfterLeavingRedirectContact()
        {
            MovePlayer(new float3(0, .94f, 0));
            var shot = Shot(new float3(0, .94f, .4f), -math.forward(), SporellaShotKind.EnemyShot);
            Punch();
            Tick<SporellaProjectileSystem>(.1f);
            Assert.That(playerHits, Is.Zero);
            Assert.That(em.GetComponentData<SporellaProjectile>(shot).ExitingRedirectContact, Is.False);
            MovePlayer(new float3(0, .94f, 3));
            Tick<SporellaProjectileSystem>(.25f);
            Assert.That(playerHits, Is.EqualTo(1));
            Assert.That(em.Exists(shot), Is.False);
        }

        [TestCase(SporellaOwnershipInteractions.PunchRedirectOnly, SporellaImpactDirection.IncomingOrOutgoing, true, false, false)]
        [TestCase(SporellaOwnershipInteractions.LaunchedEnemyTriggerOnly, SporellaImpactDirection.IncomingOrOutgoing, false, true, true)]
        [TestCase(SporellaOwnershipInteractions.Both, SporellaImpactDirection.IncomingOnly, true, true, false)]
        public void OwnershipAndDirectionSettingsAreIndependent(SporellaOwnershipInteractions interactions,
            SporellaImpactDirection direction, bool canRedirect, bool canTriggerIncoming, bool canTriggerOutgoing)
        {
            var settings = SporellaSettings.Default;
            settings.OwnershipInteractions = interactions;
            settings.ImpactDirection = direction;
            var shot = Shot(new float3(0, .94f, .4f), -math.forward(), SporellaShotKind.EnemyShot, settings);
            Punch();
            Assert.That(em.GetComponentData<PunchRequest>(player).RedirectedProjectile, Is.EqualTo(canRedirect));
            Assert.That(em.GetComponentData<SporellaProjectile>(shot).Kind == SporellaShotKind.PunchReturn, Is.EqualTo(canRedirect));

            var shooter = Shooter(settings);
            var history = em.GetBuffer<SporellaImpactHistory>(shooter);
            var contacts = em.GetBuffer<SporellaImpactContact>(shooter);
            Assert.That(SporellaImpactRecording.TryRecord(history, contacts, settings, true,
                new SporellaImpactHistory { PunchSequence = 1 }, float3.zero, math.forward(), 2), Is.EqualTo(canTriggerOutgoing));
            Assert.That(SporellaImpactRecording.TryRecord(history, contacts, settings, false,
                new SporellaImpactHistory { PunchSequence = 2 }, float3.zero, math.forward(), 2), Is.EqualTo(canTriggerIncoming));
        }

        [Test]
        public void PerEmitterHistoryDeduplicatesBothDirectionsAndLethalBurstOutlastsEmitter()
        {
            var shooter = Shooter();
            var other = Shooter();
            var settings = SporellaSettings.Default;
            var chain = new SporellaImpactHistory { PunchSequence = 12 };
            var center = new float3(3, 0, 1);
            var direction = new float3(1, 0, 0);

            bool Record(Entity entity, bool outgoing, SporellaImpactHistory key) => SporellaImpactRecording.TryRecord(
                em.GetBuffer<SporellaImpactHistory>(entity), em.GetBuffer<SporellaImpactContact>(entity),
                settings, outgoing, key, center, direction, 3);

            Assert.That(Record(shooter, false, chain), Is.True);
            Assert.That(Record(shooter, true, chain), Is.False);
            Assert.That(Record(other, true, chain), Is.True);
            Assert.That(Record(other, false, new SporellaImpactHistory { PunchSequence = 13 }), Is.True);
            // A sequence-less chain's key survives propagation and includes the source lifetime.
            var fallback = new SporellaImpactHistory { Source = player, SourceLifetime = 1, LaunchSequence = 1 };
            Assert.That(Record(other, true, fallback), Is.True);
            Assert.That(Record(other, false, fallback), Is.False);
            fallback.SourceLifetime++;
            Assert.That(Record(other, false, fallback), Is.True);
            em.DestroyEntity(other);

            em.SetComponentData(shooter, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            em.SetComponentEnabled<RespawnRequest>(shooter, true);
            Tick<SporellaImpactBurstSystem>(.02f);
            Assert.That(CountShots(), Is.EqualTo(1));
            em.DestroyEntity(shooter);

            Tick<SporellaImpactBurstSystem>(.179f);
            Assert.That(CountShots(), Is.EqualTo(1));
            Tick<SporellaImpactBurstSystem>(.001f);
            Assert.That(CountShots(), Is.EqualTo(2));
            Tick<SporellaImpactBurstSystem>(.72f);
            using var query = em.CreateEntityQuery(typeof(SporellaProjectile));
            using var shots = query.ToEntityArray(Allocator.Temp);
            Assert.That(shots.Length, Is.EqualTo(6));
            int[] laneCounts = new int[3];
            float3 right = math.cross(math.up(), direction);
            foreach (var entity in shots)
            {
                var shot = em.GetComponentData<SporellaProjectile>(entity);
                Assert.That(shot.Kind, Is.EqualTo(SporellaShotKind.ImpactShot));
                Assert.That(PositionOf(entity), Is.EqualTo(new float3(center.x, .94f, center.z)));
                float offset = 10f * math.dot(shot.Direction, right) / math.dot(shot.Direction, direction);
                int lane = (int)math.round(offset / 1.5f) + 1;
                Assert.That(offset, Is.EqualTo((lane - 1) * 1.5f).Within(.001f));
                laneCounts[lane]++;
                Assert.That(shot.PunchSequence, Is.EqualTo(12u));
            }
            Assert.That(laneCounts, Is.EqualTo(new[] { 2, 2, 2 }));
        }

        [TestCase(SporellaShotKind.ImpactShot)]
        [TestCase(SporellaShotKind.PunchReturn)]
        public void FinalProjectileDefeatAllowsDieBeforeReportingCompletion(SporellaShotKind kind)
        {
            var sporella = Shooter();
            Move(sporella, new float3(0, .94f, 4f));
            em.SetComponentData(sporella, new Health { Current = 5f, Max = 5f });
            const float dieDuration = 5f / 3f;
            em.AddComponentData(sporella, new EnemyLandingAnimation { Duration = dieDuration, OrdinaryDefeatDuration = dieDuration });
            RebuildPhysics();

            Shot(new float3(0, .94f, 1f), math.forward(), kind);
            Tick<SporellaProjectileSystem>(.3f);
            var launch = em.GetComponentData<EnemyLaunchState>(sporella);
            Assert.That(launch.Phase, Is.EqualTo(EnemyLaunchPhase.Defeated));
            Assert.That(launch.LaunchSequence, Is.Zero);
            Tick<DefeatedEnemyLifecycleSystem>(.02f);
            Assert.That(em.GetComponentData<RespawnRequest>(sporella).PoolNotBefore, Is.EqualTo(now + dieDuration).Within(.0001d));

            var sequence = em.CreateEntity(typeof(EnemyWaveSequence), typeof(EnemyWaveEncounterComplete));
            em.SetComponentEnabled<EnemyWaveEncounterComplete>(sequence, true);
            var completion = world.GetOrCreateSystemManaged<GauntletCompletionSystem>();
            uint reported = GauntletCompletionRegistry.Sequence;
            completion.Update();
            Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(reported));

            world.SetTime(new TimeData(now + dieDuration + .02d, .02f));
            completion.Update();
            Assert.That(GauntletCompletionRegistry.Sequence, Is.EqualTo(reported + 1));
        }

        private Entity Shooter(SporellaSettings? settings = null)
        {
            var entity = Body(float3.zero, true);
            em.AddComponentData(entity, settings ?? SporellaSettings.Default);
            em.AddComponentData(entity, RangedSettings());
            em.AddComponentData(entity, new RangedAttackState { Phase = RangedAttackPhase.Ready });
            em.AddComponent<SporellaNormalBurst>(entity);
            em.AddBuffer<SporellaImpactHistory>(entity);
            em.AddBuffer<SporellaImpactContact>(entity);
            return entity;
        }

        private Entity Body(float3 position, bool enemy = false)
        {
            var entity = em.CreateEntity(typeof(LocalTransform));
            Move(entity, position);
            if (enemy)
            {
                em.AddComponent<Enemy>(entity);
                em.AddComponent<EnemyTier>(entity);
                em.AddComponent<EnemyLaunchState>(entity);
                em.AddComponent<EnemyDamageState>(entity);
                em.AddComponent<EnemyHealthBarVisibility>(entity);
                em.SetComponentEnabled<EnemyHealthBarVisibility>(entity, false);
                em.AddComponentData(entity, new Health { Current = 30, Max = 30 });
                em.AddComponent<RespawnRequest>(entity);
                em.SetComponentEnabled<RespawnRequest>(entity, false);
                em.AddComponent<DeathRequest>(entity);
                em.SetComponentEnabled<DeathRequest>(entity, false);
                em.AddComponent<DamageRequest>(entity);
                em.SetComponentEnabled<DamageRequest>(entity, false);
            }
            bodies.Add(entity);
            return entity;
        }

        private Entity Shot(float3 origin, float3 direction, SporellaShotKind kind, SporellaSettings? settings = null)
        {
            var tuning = settings ?? SporellaSettings.Default;
            var shot = SporellaShotEmission.Create(arena, Entity.Null, 0, kind, tuning, RangedSettings());
            shot.Direction = direction;
            var entity = em.CreateEntity(typeof(LocalTransform));
            Move(entity, origin);
            em.AddComponentData(entity, shot);
            em.AddBuffer<BodyProjectileHit>(entity);
            return entity;
        }

        private static RangedEnemySettings RangedSettings() => new RangedEnemySettings
        {
            ProjectileRadius = .38f, EngagementRange = 25, Cooldown = 2, ProjectileDamage = 10,
            PlayerInvincibilitySeconds = .5f, ProjectilePlayerLayers = 1
        };

        private void RebuildPhysics()
        {
            if (physics.Bodies.IsCreated) physics.Dispose();
            physics = new PhysicsWorld(bodies.Count, 0, 0);
            var rigid = physics.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                var pose = em.GetComponentData<LocalTransform>(bodies[i]);
                rigid[i] = new RigidBody { Entity = bodies[i], Collider = collider,
                    WorldFromBody = new RigidTransform(pose.Rotation, pose.Position), Scale = 1f };
            }
            physics.CollisionWorld.BuildBroadphase(ref physics, .02f, float3.zero, true);
            em.SetComponentData(physicsEntity, new PhysicsWorldSingleton { PhysicsWorld = physics });
        }

        private void Move(Entity entity, float3 position) => em.SetComponentData(entity, LocalTransform.FromPosition(position));
        private void MovePlayer(float3 position) => em.SetComponentData(player,
            new PlayerSnapshot { Position = position, IsAvailable = true, Radius = .5f, CollisionLayer = 1 });
        private float3 PositionOf(Entity entity) => em.GetComponentData<LocalTransform>(entity).Position;
        private float HealthOf(Entity entity) => em.GetComponentData<Health>(entity).Current;
        private int CountShots()
        {
            using var query = em.CreateEntityQuery(typeof(SporellaProjectile));
            return query.CalculateEntityCount();
        }

        private void Punch()
        {
            em.SetComponentData(player, new PunchRequest
            {
                Origin = new float3(0, .94f, 0), Direction = math.forward(), Range = 3f, Radius = 1f,
                Strength = 12f, Damage = 5f, Sequence = 7
            });
            em.SetComponentEnabled<PunchRequest>(player, true);
            Tick<PunchDetectionSystem>(.02f);
        }

        private void Tick<T>(float dt) where T : unmanaged, ISystem
        {
            world.SetTime(new TimeData(now += dt, dt));
            world.GetOrCreateSystem<T>().Update(world.Unmanaged);
        }
    }
}
