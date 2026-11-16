using CrowdPunch.Authoring;
using System;
using System.Linq;
using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.AI;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Groups;
using CrowdPunch.Systems.Movement;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace CrowdPunch.Tests
{
    public sealed class AlienSpikeTests
    {
        private World world;
        private EntityManager em;
        private PhysicsWorld physics;
        private bool physicsCreated;
        private BlobAssetReference<Unity.Physics.Collider> collider;
        private PlayerEcsBridge bridge, previousBridge;
        private Entity player;
        private float playerDamage;

        [SetUp]
        public void SetUp()
        {
            world = new World("Alien Spike");
            world.SetTime(new TimeData(1, 1));
            em = world.EntityManager;
            collider = Unity.Physics.BoxCollider.Create(new BoxGeometry
            {
                Size = new float3(1, 2, 1), Orientation = quaternion.identity
            }, CollisionFilter.Default);
            player = em.CreateEntity(typeof(PlayerSnapshot));
            bridge = new GameObject("Alien Spike test player").AddComponent<PlayerEcsBridge>();
            bridge.PublishPlayerHealth(100, 100);
            PlayerBridgeRegistry.TryGetBridge(out previousBridge);
            PlayerBridgeRegistry.Register(bridge);
            playerDamage = 0;
            bridge.EnemyContactHitReceived += (damagePercent, protection, push) => playerDamage += damagePercent * bridge.MaxHealth;
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
            if (physicsCreated) physics.Dispose();
            physicsCreated = false;
            collider.Dispose();
            PlayerBridgeRegistry.Unregister(bridge);
            if (previousBridge != null) PlayerBridgeRegistry.Register(previousBridge);
            Object.DestroyImmediate(bridge.gameObject);
        }

        [Test]
        public void VolleyAndPunchSystemsSortInTheGameplayGroups()
        {
            var group = world.GetOrCreateSystemManaged<GamePrePhysicsGroup>();
            world.GetOrCreateSystemManaged<GamePostPhysicsGroup>();
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default)
                .Where(type => Attribute.GetCustomAttributes(type, typeof(UpdateInGroupAttribute), true)
                    .Cast<UpdateInGroupAttribute>().Any(group => group.GroupType == typeof(GamePrePhysicsGroup)
                        || group.GroupType == typeof(GamePostPhysicsGroup)))
                .ToArray();
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            group.SortSystems();
            using var order = group.GetAllSystems();
            var volley = world.GetExistingSystem<AlienSpikeVolleySystem>();
            var navigation = world.GetExistingSystem<EnemyNavigationSystem>();
            var movement = world.GetExistingSystem<EnemyMovementSystem>();
            var punch = world.GetExistingSystem<PunchDetectionSystem>();
            Assert.That(order.IndexOf(volley), Is.GreaterThanOrEqualTo(0));
            Assert.That(order.IndexOf(volley), Is.LessThan(order.IndexOf(navigation)));
            Assert.That(order.IndexOf(navigation), Is.LessThan(order.IndexOf(movement)));
            Assert.That(order.IndexOf(movement), Is.LessThan(order.IndexOf(punch)));
        }

        [Test]
        public void CommittedVolleyFiresOutOfRangeAndLaunchRestartsCooldown()
        {
            var t = new AlienSpikeTuning { Cooldown = 3, TelegraphSeconds = 1 };
            var volley = new AlienSpikeVolley();
            Assert.That(AlienSpikeVolleySystem.Advance(ref volley, t, EnemyLaunchPhase.Active, false, 1), Is.False);
            Assert.That(AlienSpikeVolleySystem.Advance(ref volley, t, EnemyLaunchPhase.Active, true, 1), Is.True);
            Assert.That(volley.Phase, Is.EqualTo(AlienSpikeVolleyPhase.Telegraph));
            AlienSpikeVolleySystem.Advance(ref volley, t, EnemyLaunchPhase.Active, false, 1);
            Assert.That(volley.Phase, Is.EqualTo(AlienSpikeVolleyPhase.Release));
            AlienSpikeVolleySystem.Advance(ref volley, t, EnemyLaunchPhase.Launched, true, 1);
            AlienSpikeVolleySystem.Advance(ref volley, t, EnemyLaunchPhase.Recovering, true, 2);
            Assert.That(volley.Phase, Is.EqualTo(AlienSpikeVolleyPhase.Moving));
            Assert.That(volley.Remaining, Is.EqualTo(3));
            Assert.That(AlienSpikeVolleySystem.Advance(ref volley, t, EnemyLaunchPhase.Active, true, 2), Is.False);
        }

        [TestCase(AlienSpikeVolleyMode.TimerWhileMoving, 5f, true)]
        [TestCase(AlienSpikeVolleyMode.MidRangePositioning, 5f, false)]
        [TestCase(AlienSpikeVolleyMode.MidRangePositioning, 7f, true)]
        [TestCase(AlienSpikeVolleyMode.TimerWhileMoving, 12f, false)]
        public void VolleyModeControlsPositioningBeforeCommit(AlienSpikeVolleyMode mode, float distance, bool commits)
        {
            var spike = em.CreateEntity(typeof(AlienSpikeTuning), typeof(EnemyLaunchState),
                typeof(LocalTransform), typeof(NavigationAgent), typeof(EnemyMovementSettings), typeof(AlienSpikeVolley),
                typeof(DesiredMovement), typeof(NavigationIntent));
            em.SetComponentData(spike, new AlienSpikeTuning
            {
                VolleyMode = mode, Cooldown = 3, TelegraphSeconds = 1, EngagementRadius = 11, PreferredDistance = 7
            });
            em.SetComponentData(spike, new EnemyMovementSettings { MoveSpeed = 3f });
            em.SetComponentData(spike, LocalTransform.Identity);
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Position = new float3(distance, 0, 0) });
            world.GetOrCreateSystem<AlienSpikeVolleySystem>().Update(world.Unmanaged);
            var state = em.GetComponentData<AlienSpikeVolley>(spike);
            Assert.That(state.Phase == AlienSpikeVolleyPhase.Telegraph, Is.EqualTo(commits));
            Assert.That(em.GetComponentData<DesiredMovement>(spike).Speed, Is.EqualTo(commits ? 0 : 3));
            if (!commits && mode == AlienSpikeVolleyMode.MidRangePositioning)
                Assert.That(em.GetComponentData<NavigationIntent>(spike).Destination.x, Is.LessThan(0));
        }

        [Test]
        public void PunchOnTheReleaseStepCancelsNormalVolley()
        {
            var audioEntity = em.CreateSingletonBuffer<EnemyAttackAudioEvent>();
            var spike = em.CreateEntity(typeof(AlienSpikeTuning), typeof(AlienSpikeVolley),
                typeof(EnemyLaunchState), typeof(LocalTransform));
            em.SetComponentData(spike, LocalTransform.Identity);
            em.SetComponentData(spike, new AlienSpikeTuning { Cooldown = 3 });
            em.SetComponentData(spike, new AlienSpikeVolley { Phase = AlienSpikeVolleyPhase.Release });
            em.SetComponentData(spike, new EnemyLaunchState { Phase = EnemyLaunchPhase.Launched });
            em.AddBuffer<AlienSpikeSocket>(spike).Add(new AlienSpikeSocket
            {
                ReleaseTip = new float3(.4f, .5f, 0), ReleaseDirection = new float2(1, 0)
            });
            em.AddBuffer<AlienSpikeBurst>(spike);

            world.GetOrCreateSystem<AlienSpikeEmissionSystem>().Update(world.Unmanaged);

            using var shots = em.CreateEntityQuery(typeof(AlienSpikeProjectile));
            Assert.That(shots.IsEmpty, Is.True);
            Assert.That(em.GetBuffer<EnemyAttackAudioEvent>(audioEntity).IsEmpty, Is.True);
            var volley = em.GetComponentData<AlienSpikeVolley>(spike);
            Assert.That(volley.Phase, Is.EqualTo(AlienSpikeVolleyPhase.Moving));
            Assert.That(volley.Remaining, Is.EqualTo(3));
        }

        [Test]
        public void QueuedImpactEmitsFromItsRecordedPoseEvenAfterLethalImpact()
        {
            var audioEntity = em.CreateSingletonBuffer<EnemyAttackAudioEvent>();
            var spike = em.CreateEntity(typeof(AlienSpikeTuning), typeof(AlienSpikeVolley),
                typeof(EnemyLaunchState), typeof(LocalTransform));
            em.SetComponentData(spike, LocalTransform.FromPosition(new float3(10, 1, 10)));
            em.SetComponentData(spike, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            em.SetComponentData(spike, new AlienSpikeTuning
            {
                Shot = new AlienSpikeShotTuning { Radius = .13f, Speed = 9, Lifetime = 2 }
            });
            em.AddComponent<ArenaGroundPlane>(em.CreateEntity());
            em.AddBuffer<AlienSpikeSocket>(spike).Add(new AlienSpikeSocket
            {
                IdleTip = new float3(.4f, 3f, 0), IdleDirection = new float2(1, 0)
            });
            em.AddBuffer<AlienSpikeBurst>(spike).Add(new AlienSpikeBurst
            {
                Pose = LocalTransform.FromPosition(new float3(2, 1, 3)), PunchSequence = 56, LaunchSequence = 8
            });

            var emission = world.GetOrCreateSystem<AlienSpikeEmissionSystem>();
            emission.Update(world.Unmanaged);
            emission.Update(world.Unmanaged);
            using var projectiles = em.CreateEntityQuery(typeof(AlienSpikeProjectile));
            var shot = projectiles.GetSingleton<AlienSpikeProjectile>();
            Assert.That(shot.PunchSequence, Is.EqualTo(56));
            Assert.That(shot.LaunchSequence, Is.EqualTo(8));
            Assert.That(shot.PlayerOwned, Is.EqualTo(1));
            Assert.That(shot.Velocity, Is.EqualTo(new float3(9, 0, 0)));
            var position = em.GetComponentData<LocalTransform>(projectiles.GetSingletonEntity()).Position;
            Assert.That(position, Is.EqualTo(new float3(2.4f, 1.45f, 3)));
            var audio = em.GetBuffer<EnemyAttackAudioEvent>(audioEntity);
            Assert.That(audio.Length, Is.EqualTo(1));
            Assert.That(audio[0].Kind, Is.EqualTo(EnemyAttackAudioKind.AlienSpikeBurst));
            Assert.That(audio[0].Position, Is.EqualTo(new float3(2, 1, 3)));
        }

        [TestCase(AlienSpikeAuthoring.SpawnMotion, TestName = "SpawnEmergesOnceAndStaysAboveTheFloor")]
        [TestCase(EnemyAnimationSamples.ImpactMotion, TestName = "DefeatIsGroundedFromTheFirstFrame")]
        public void SampledPoseGrounding(int motion)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CrowdPunch/Prefabs/EnemyAlienSpike.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var animation = instance.GetComponentInChildren<EnemyAnimationAuthoring>();
                var skin = animation.GetComponent<SkinnedMeshRenderer>();
                var visual = skin.GetComponentInParent<Animator>().transform;
                var capsule = instance.GetComponent<UnityEngine.CapsuleCollider>();
                float floor = capsule.center.y - capsule.height * .5f;
                var vertices = skin.sharedMesh.vertices;
                var weights = skin.sharedMesh.boneWeights;
                using var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(animation.Samples.bytes));
                reader.ReadInt32();
                reader.ReadString();
                int bones = reader.ReadInt32();
                int frames = reader.ReadInt32();
                for (int i = 0; i < bones; i++) reader.ReadString();
                reader.BaseStream.Position += sizeof(float) * (EnemyAnimationSamples.MotionCount + motion * frames * bones * 12);

                var pose = new Matrix4x4[bones];
                bool emerged = false;
                for (int frame = 0; frame < frames; frame++)
                {
                    for (int bone = 0; bone < bones; bone++)
                    {
                        var matrix = Matrix4x4.identity;
                        for (int column = 0; column < 4; column++)
                        for (int row = 0; row < 3; row++)
                            matrix[row, column] = reader.ReadSingle();
                        pose[bone] = instance.transform.worldToLocalMatrix * visual.localToWorldMatrix * matrix;
                    }
                    float lowest = float.PositiveInfinity;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var w = weights[i];
                        var v = vertices[i];
                        float y = pose[w.boneIndex0].MultiplyPoint3x4(v).y * w.weight0
                            + pose[w.boneIndex1].MultiplyPoint3x4(v).y * w.weight1
                            + pose[w.boneIndex2].MultiplyPoint3x4(v).y * w.weight2
                            + pose[w.boneIndex3].MultiplyPoint3x4(v).y * w.weight3;
                        lowest = Mathf.Min(lowest, y);
                    }
                    if (motion == EnemyAnimationSamples.ImpactMotion)
                    {
                        Assert.That(lowest - floor, Is.InRange(0f, .025f), $"Defeat frame {frame}");
                    }
                    else
                    {
                        if (frame == 0) Assert.That(lowest, Is.LessThan(floor));
                        emerged |= lowest >= floor;
                        if (emerged) Assert.That(lowest, Is.GreaterThanOrEqualTo(floor), $"Spawn frame {frame}");
                    }
                }
                if (motion == AlienSpikeAuthoring.SpawnMotion) Assert.That(emerged, Is.True);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void PrefabSocketsMatchTheAuthoredReleasePose()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CrowdPunch/Prefabs/EnemyAlienSpike.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var authoring = instance.GetComponent<AlienSpikeAuthoring>();
                var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                var animator = instance.GetComponentInChildren<Animator>();
                authoring.projectileAttack.SampleAnimation(animator.gameObject,
                    authoring.projectileAttack.length * AlienSpikeAuthoring.ReleasePhase);
                var weights = skin.sharedMesh.boneWeights;
                var vertices = skin.sharedMesh.vertices;
                var bindposes = skin.sharedMesh.bindposes;
                Assert.That(authoring.sockets.Length, Is.EqualTo(12));
                for (int socket = 0; socket < authoring.sockets.Length; socket++)
                {
                    int apex = 331 + socket * 9;
                    var weight = weights[apex];
                    var vertex = vertices[apex];
                    Vector3 Bone(int bone) => skin.bones[bone].TransformPoint(bindposes[bone].MultiplyPoint3x4(vertex));
                    Vector3 worldTip = Bone(weight.boneIndex0) * weight.weight0 + Bone(weight.boneIndex1) * weight.weight1
                        + Bone(weight.boneIndex2) * weight.weight2 + Bone(weight.boneIndex3) * weight.weight3;
                    var localTip = instance.transform.InverseTransformPoint(worldTip);
                    Assert.That(Vector3.Distance(authoring.sockets[socket].releaseTip, localTip), Is.LessThan(.006f));
                }
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void BodyContactsDeduplicateAcrossPropagatedPunchAndAcceptANewPunch()
        {
            var origin = new AlienSpikeBurst { Source = em.CreateEntity(), LaunchSequence = 3, PunchSequence = 7 };
            var propagated = new AlienSpikeBurst { Source = em.CreateEntity(), LaunchSequence = 6, PunchSequence = 7 };
            var history = em.AddBuffer<AlienSpikeBurstHistory>(em.CreateEntity());
            Assert.That(AlienSpikeBurstRecording.TryRecord(history, origin), Is.True);
            Assert.That(AlienSpikeBurstRecording.TryRecord(history, origin), Is.False);
            Assert.That(AlienSpikeBurstRecording.TryRecord(history, propagated), Is.False);
            origin.PunchSequence++;
            origin.LaunchSequence++;
            Assert.That(AlienSpikeBurstRecording.TryRecord(history, origin), Is.True);
            Assert.That(AlienSpikeBurstRecording.TryRecord(history, propagated), Is.False);
        }

        [TestCase(false, false, 0, 8)]
        [TestCase(false, true, 0, 0)]
        [TestCase(true, false, 15, 0)]
        [TestCase(true, true, 0, 0)]
        public void SweptContactResolvesNearestEligibleTargetOrCover(bool owned, bool cover,
            float expectedEnemyDamage, float expectedPlayerDamage)
        {
            var enemy = Enemy();
            var wall = em.CreateEntity();
            if (cover) BuildPhysics(new[] { enemy, wall }, new[] { 4f, 2f });
            else BuildPhysics(new[] { enemy }, new[] { 4f });
            em.SetComponentData(player, new PlayerSnapshot { IsAvailable = true, Radius = .5f, Position = new float3(8, 1, 0) });
            int bursts = 0;
            bridge.ProjectileBurstReceived += (kind, point, normal, _, owner) =>
            {
                bursts++;
                Assert.That(kind, Is.EqualTo(ProjectileVisualKind.AlienSpike));
                Assert.That(owner, Is.EqualTo(owned ? EnemyLaunchOwner.Player : EnemyLaunchOwner.Enemy));
                Assert.That(point.x, Is.EqualTo(cover ? 1.5f : owned ? 3.5f : 7.5f).Within(.03f));
                Assert.That(normal.x, Is.LessThan(-.9f));
            };
            var projectile = Fire(owned);
            Tick();
            Assert.That(em.Exists(projectile), Is.False);
            Assert.That(bursts, Is.EqualTo(1), "SPIKE-003: one burst at the first solid or eligible contact.");
            Assert.That(em.GetComponentData<Health>(enemy).Current, Is.EqualTo(20 - expectedEnemyDamage));
            Assert.That(playerDamage, Is.EqualTo(expectedPlayerDamage));
            Assert.That(em.GetComponentData<PhysicsVelocity>(enemy).Linear, Is.EqualTo(new float3(1, 0, 0)));
        }

        [Test]
        public void DamageAfterLethalQueuedBodyImpactPreservesEarlierChainCredit()
        {
            var enemy = Enemy();
            em.SetComponentData(enemy, new DamageRequest { Amount = 20, DefeatPunchSequence = 99 });
            em.SetComponentEnabled<DamageRequest>(enemy, true);
            BuildPhysics(new[] { enemy }, new[] { 3f });
            Fire(true);
            Tick();
            Assert.That(em.GetComponentData<EnemyDamageState>(enemy).DefeatPunchSequence, Is.EqualTo(99));
        }

        [Test]
        public void IntactArmorConsumesShotWithoutLosingAStage()
        {
            var armored = Enemy();
            em.AddComponentData(armored, new EnemyArmor { Stages = 2 });
            BuildPhysics(new[] { armored }, new[] { 3f });
            var projectile = Fire(true);
            Tick();
            Assert.That(em.Exists(projectile), Is.False);
            Assert.That(em.GetComponentData<EnemyArmor>(armored).Stages, Is.EqualTo(2));
            Assert.That(em.GetComponentData<Health>(armored).Current, Is.EqualTo(20));
        }

        [TestCase(false, AlienSpikeAimMode.DirectContactOnly)]
        [TestCase(true, AlienSpikeAimMode.ChickenStyle)]
        [TestCase(false, AlienSpikeAimMode.ChickenStyle)]
        [TestCase(true, AlienSpikeAimMode.DirectContactOnly)]
        public void RedirectedShotCanHitItsEmitterAndReplacesChainCredit(bool owned, AlienSpikeAimMode mode)
        {
            var emitter = Enemy(10);
            BuildPhysics(new[] { emitter }, new[] { 3f });
            var entity = Fire(owned);
            var shot = em.GetComponentData<AlienSpikeProjectile>(entity);
            shot.Emitter = emitter;
            shot.Tuning.AimMode = mode;
            AlienSpikeProjectilePunch.Redirect(em, entity, ref shot, new float3(0, 1, 0),
                new PunchSpecification { Direction = new float3(1, 0, 0), PunchSequence = 41 });
            em.SetComponentData(entity, shot);
            Tick();
            Assert.That(em.GetComponentData<Health>(emitter).Current, Is.Zero);
            var damage = em.GetComponentData<EnemyDamageState>(emitter);
            Assert.That(damage.DefeatPunchSequence, Is.EqualTo(41));
            Assert.That(damage.DefeatOwner, Is.EqualTo(EnemyLaunchOwner.Player));
        }

        [TestCase(AlienSpikeExploderMode.DetonateOnHit, true, 20)]
        [TestCase(AlienSpikeExploderMode.OrdinaryDamageOnly, false, 5)]
        public void ExploderModesBothConsumeShot(AlienSpikeExploderMode mode, bool detonate, float expectedHealth)
        {
            var exploder = Enemy();
            em.AddComponent<ExplosiveEnemyState>(exploder);
            em.AddComponent<ExplosiveDetonationRequest>(exploder);
            em.SetComponentEnabled<ExplosiveDetonationRequest>(exploder, false);
            BuildPhysics(new[] { exploder }, new[] { 3f });
            var projectile = Fire(true);
            var shot = em.GetComponentData<AlienSpikeProjectile>(projectile);
            shot.Tuning.ExploderMode = mode;
            em.SetComponentData(projectile, shot);
            Tick();
            Assert.That(em.Exists(projectile), Is.False);
            Assert.That(em.GetComponentData<Health>(exploder).Current, Is.EqualTo(expectedHealth));
            Assert.That(em.IsComponentEnabled<ExplosiveDetonationRequest>(exploder), Is.EqualTo(detonate));
            if (detonate) Assert.That(em.GetComponentData<ExplosiveDetonationRequest>(exploder).PunchSequence, Is.EqualTo(22));
        }

        private Entity Enemy(float health = 20)
        {
            var entity = em.CreateEntity(typeof(Enemy), typeof(EnemyTier), typeof(Health), typeof(EnemyLaunchState),
                typeof(EnemyDamageState), typeof(DamageRequest), typeof(DeathRequest), typeof(EnemyHealthBarVisibility),
                typeof(PhysicsVelocity), typeof(RespawnRequest));
            em.SetComponentData(entity, new Health { Current = health, Max = health });
            em.SetComponentData(entity, new PhysicsVelocity { Linear = new float3(1, 0, 0) });
            em.SetComponentEnabled<RespawnRequest>(entity, false);
            em.SetComponentEnabled<DamageRequest>(entity, false);
            em.SetComponentEnabled<DeathRequest>(entity, false);
            return entity;
        }

        private void BuildPhysics(Entity[] entities, float[] positions)
        {
            physics = new PhysicsWorld(entities.Length, 0, 0);
            physicsCreated = true;
            var bodies = physics.Bodies;
            for (int i = 0; i < entities.Length; i++)
                bodies[i] = new RigidBody { Collider = collider, Entity = entities[i], Scale = 1,
                    WorldFromBody = new RigidTransform(quaternion.identity, new float3(positions[i], 1, 0)) };
            physics.CollisionWorld.BuildBroadphase(ref physics, 1, float3.zero, true);
            em.SetComponentData(em.CreateEntity(typeof(PhysicsWorldSingleton)), new PhysicsWorldSingleton { PhysicsWorld = physics });
        }

        private Entity Fire(bool owned)
        {
            var shot = em.CreateEntity(typeof(LocalTransform), typeof(AlienSpikeProjectile), typeof(PunchAimAssistTarget));
            em.SetComponentData(shot, LocalTransform.FromPosition(new float3(0, 1, 0)));
            em.SetComponentData(shot, new AlienSpikeProjectile
            {
                Tuning = new AlienSpikeShotTuning { Radius = .13f, Speed = 12, Lifetime = 2, NormalDamage = 8, BurstDamage = 15 },
                Velocity = new float3(12, 0, 0), Remaining = 2, PunchSequence = 22, ChainDepth = 2,
                PlayerOwned = (byte)(owned ? 1 : 0)
            });
            return shot;
        }

        private void Tick() => world.GetOrCreateSystem<AlienSpikeProjectileSystem>().Update(world.Unmanaged);
    }
}

