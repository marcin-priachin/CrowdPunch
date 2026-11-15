using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
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
        public void ClearStartThenTrackingLockAndShotShareTheSameCoveredLine()
        {
            var shooter = Shooter();
            var cover = Body(new float3(0, .95f, 5), false);
            RebuildCollision();
            Attack(.05f);
            Assert.That(AttackOf(shooter).Phase, Is.EqualTo(RangedAttackPhase.Ready));
            Move(cover, new float3(8, .95f, 5));
            RebuildCollision();
            Attack(.05f);
            Assert.That(AttackOf(shooter).Phase, Is.EqualTo(RangedAttackPhase.WindUp));
            for (int i = 0; i < 5; i++) Attack(.05f);
            Assert.That(Lane(shooter).Locked, Is.False);
            MovePlayer(new float3(3, 1, 12));
            Move(cover, new float3(1.25f, .95f, 5));
            RebuildCollision();
            Attack(.05f);
            var locked = Lane(shooter);
            Assert.That(locked.Locked, Is.True);
            Assert.That(locked.Direction.x, Is.GreaterThan(.2f));
            Assert.That(locked.Length, Is.LessThan(6f));
            MovePlayer(new float3(-3, 1, 12));
            Move(shooter, new float3(.2f, 1, 0));
            for (int i = 0; i < 8; i++) Attack(.05f);
            Assert.That(AttackOf(shooter).ProjectilesSpawned, Is.Zero);
            Assert.That(Lane(shooter).Direction, Is.EqualTo(locked.Direction));
            Assert.That(Lane(shooter).Origin, Is.EqualTo(locked.Origin));
            Attack(.05f);
            using var q = em.CreateEntityQuery(typeof(MushroomProjectile));
            var shot = q.GetSingleton<MushroomProjectile>();
            Assert.That(shot.Direction, Is.EqualTo(locked.Direction));
            Assert.That(shot.Speed, Is.EqualTo(32f));
            Flight(.25f);
            Assert.That(q.CalculateEntityCount(), Is.Zero);
            Assert.That(playerHits, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LaunchCancelsEitherPhaseAndCooldownWaitsForRecovery(bool afterLock)
        {
            var shooter = Shooter();
            Attack(.05f);
            Attack(afterLock ? .35f : .1f);
            Assert.That(Lane(shooter).Locked, Is.EqualTo(afterLock));
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
        public void PlayerPunchPrioritizesShotAndReturnPiercesUntilFirstSolid()
        {
            MovePlayer(new float3(0, 1, 0));
            var first = Body(new float3(0, .95f, 2), true);
            var shooter = Shooter(new float3(0, .95f, 4));
            var armored = Body(new float3(0, .95f, 6), true);
            em.AddComponentData(armored, new EnemyArmor { Stages = 1 });
            Body(new float3(0, .95f, 8), false);
            var behindCover = Body(new float3(0, .95f, 10), true);
            RebuildCollision();
            var shot = Shot(new float3(0, .95f, .6f), new float3(0, 0, -1), 1f);
            em.SetComponentData(player, new PunchRequest
            {
                Origin = new float3(0, .95f, 0), Direction = new float3(0, .4f, 1),
                Range = 3f, Radius = 1f, Strength = 12f, Damage = 5f, Sequence = 7u
            });
            em.SetComponentEnabled<PunchRequest>(player, true);
            world.GetOrCreateSystem<PunchDetectionSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<EnemyLaunchState>(first).Phase, Is.EqualTo(EnemyLaunchPhase.Active));
            var redirected = em.GetComponentData<MushroomProjectile>(shot);
            Assert.That(redirected.Owner, Is.EqualTo(EnemyLaunchOwner.Player));
            Assert.That(redirected.Direction, Is.EqualTo(new float3(0, 0, 1)));
            Assert.That(redirected.RangeRemaining, Is.EqualTo(45f));
            Flight(.5f);
            Assert.That(playerHits, Is.Zero);
            Assert.That(em.Exists(shot), Is.False);
            foreach (var target in new[] { first, shooter })
            {
                Assert.That(em.GetComponentData<Health>(target).Current, Is.Zero);
                Assert.That(em.GetComponentData<EnemyLaunchState>(target).Phase, Is.EqualTo(EnemyLaunchPhase.Defeated));
                Assert.That(em.GetComponentData<EnemyDamageState>(target).DefeatOwner, Is.EqualTo(EnemyLaunchOwner.Player));
                Assert.That(em.GetComponentData<EnemyDamageState>(target).DefeatPunchSequence, Is.EqualTo(7u));
            }
            Assert.That(em.GetComponentData<Health>(armored).Current, Is.EqualTo(5f));
            Assert.That(em.GetComponentData<Health>(behindCover).Current, Is.EqualTo(5f));
        }

        [Test]
        public void ASecondValidPunchCannotResetAReturnedShot()
        {
            MovePlayer(new float3(0, 1, 0));
            var shot = Shot(new float3(0, 1, 2), math.forward(), 45f);
            var returned = em.GetComponentData<MushroomProjectile>(shot);
            MushroomProjectilePunch.Redirect(ref returned, math.forward(), 1);
            returned.RangeRemaining = 22f;
            em.SetComponentData(shot, returned);
            em.SetComponentData(player, new PunchRequest
            {
                Origin = new float3(0, 1, 3), Direction = -math.forward(),
                Range = 3f, Radius = 1f, Strength = 12f, Damage = 5f, Sequence = 2u
            });
            em.SetComponentEnabled<PunchRequest>(player, true);
            world.GetOrCreateSystem<PunchDetectionSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<PunchRequest>(player).HitEnemy, Is.False);
            Assert.That(em.GetComponentData<MushroomProjectile>(shot).PunchSequence, Is.EqualTo(1u));
            Assert.That(em.GetComponentData<MushroomProjectile>(shot).Direction, Is.EqualTo(math.forward()));
            Assert.That(em.GetComponentData<MushroomProjectile>(shot).RangeRemaining, Is.EqualTo(22f));
        }

        [Test]
        public void IncomingShotIgnoresEnemiesAndConsumesOnPlayer()
        {
            var enemy = Body(new float3(0, 1, 5), true);
            RebuildCollision();
            var shot = Shot(new float3(0, 1, 0), new float3(0, 0, 1), 45f);
            Flight(.5f);
            Assert.That(em.Exists(shot), Is.False);
            Assert.That(em.GetComponentData<Health>(enemy).Current, Is.EqualTo(5f));
            Assert.That(playerHits, Is.EqualTo(1));
            Assert.That(playerDamage, Is.EqualTo(10f));
            Assert.That(playerProtection, Is.EqualTo(.5f));
        }

        [Test]
        public void ReturnedShotCanHitPlayerAfterSeparationAndCannotDamageAnEnemyTwice()
        {
            MovePlayer(new float3(0, 1, 0));
            var enemy = Body(new float3(0, 1, 2), true);
            em.SetComponentData(enemy, new Health { Current = 15, Max = 15 });
            RebuildCollision();
            var shot = Shot(new float3(0, 1, .3f), new float3(0, 0, 1), 45f);
            var state = em.GetComponentData<MushroomProjectile>(shot);
            MushroomProjectilePunch.Redirect(ref state, state.Direction, 1);
            em.SetComponentData(shot, state);
            Flight(.055f);
            Flight(.03f);
            Assert.That(em.GetComponentData<Health>(enemy).Current, Is.EqualTo(10f));
            Assert.That(em.GetBuffer<BodyProjectileHit>(shot).Length, Is.EqualTo(1));
            MovePlayer(new float3(0, 1, 4));
            Flight(.1f);
            Assert.That(playerHits, Is.EqualTo(1));
            Assert.That(em.Exists(shot), Is.False);
        }

        [Test]
        public void LowShotSourceAnchorsTheWarningAndReleaseWhileCoverStillStopsFlight()
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
            Assert.That(Lane(shooter).Locked, Is.True);
            Body(new float3(4, .532f, 0), false);
            RebuildCollision(floor, floorShape);
            Attack(.45f);
            using var shots = em.CreateEntityQuery(typeof(MushroomProjectile));
            var shot = shots.GetSingletonEntity();
            Assert.That(em.GetComponentData<LocalTransform>(shot).Position, Is.EqualTo(origin));
            Flight(.025f);
            Assert.That(em.Exists(shot), Is.True);
            Assert.That(em.GetComponentData<LocalTransform>(shot).Position.y, Is.EqualTo(origin.y));
            Flight(.2f);
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

        private Entity Shooter(float3 position = default)
        {
            var entity = Body(position, true);
            em.AddComponentData(entity, MushroomShooterSettings.Default);
            em.AddComponent<MushroomWarningLane>(entity);
            em.AddComponentData(entity, new RangedAttackState { Phase = RangedAttackPhase.Ready });
            em.AddComponentData(entity, new RangedEnemySettings
            {
                ProjectileRadius = .55f, EngagementRange = 32, Cooldown = 2, ProjectileSpeed = 32,
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

        private Entity Shot(float3 position, float3 direction, float remaining)
        {
            var entity = em.CreateEntity(typeof(LocalTransform), typeof(MushroomProjectile));
            Move(entity, position);
            em.SetComponentData(entity, new MushroomProjectile
            {
                Arena = arena, Owner = EnemyLaunchOwner.Enemy, Direction = direction, Speed = 32, Radius = .55f,
                Range = 45, RangeRemaining = remaining, PlayerDamage = 10, PlayerInvincibility = .5f,
                EnemyDamage = 5, PlayerLayers = 1
            });
            em.AddBuffer<BodyProjectileHit>(entity);
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
        private void Flight(float dt)
        {
            world.SetTime(new TimeData(now += dt, dt));
            world.GetOrCreateSystem<MushroomProjectileSystem>().Update(world.Unmanaged);
        }
    }
}
