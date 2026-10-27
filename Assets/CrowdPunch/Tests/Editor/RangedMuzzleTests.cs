using CrowdPunch.Components;
using CrowdPunch.Mono.Player;
using CrowdPunch.Systems.Combat;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class RangedMuzzleTests
    {
        private World world;
        private Entity owner, visual;
        private BlobAssetReference<EnemyAnimationSamples> samples;

        [SetUp]
        public void SetUp()
        {
            world = new World("Ranged muzzle test");
            var em = world.EntityManager;
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var data = ref builder.ConstructRoot<EnemyAnimationSamples>();
            data.BoneCount = 1; data.FrameCount = 2;
            var durations = builder.Allocate(ref data.Durations, EnemyAnimationSamples.MotionCount);
            var poses = builder.Allocate(ref data.Matrices, EnemyAnimationSamples.MotionCount * 2);
            for (int motion = 0; motion < EnemyAnimationSamples.MotionCount; motion++)
            {
                durations[motion] = 1;
                for (int frame = 0; frame < 2; frame++)
                    poses[motion * 2 + frame] = new float3x4(new float3(1, 0, 0), new float3(0, 1, 0),
                        new float3(0, 0, 1), new float3(frame * 2, 1, motion));
            }
            samples = builder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
            owner = em.CreateEntity(typeof(Enemy), typeof(LocalTransform), typeof(DesiredMovement), typeof(EnemyMovementSettings),
                typeof(EnemyLaunchState), typeof(RespawnRequest), typeof(RangedAttackState), typeof(RangedEnemySettings));
            em.SetComponentEnabled<RespawnRequest>(owner, false);
            var transform = LocalTransform.FromPositionRotationScale(new float3(4, 2, -3), quaternion.RotateY(1.1f), 1.4f);
            em.SetComponentData(owner, transform);
            em.SetComponentData(owner, new EnemyMovementSettings { MoveSpeed = 4 });
            em.SetComponentData(owner, new RangedAttackState { Phase = RangedAttackPhase.WindUp, SecondsRemaining = .5f });
            var prefab = em.CreateEntity(typeof(Prefab), typeof(LocalTransform), typeof(RangedProjectile));
            em.SetComponentData(owner, new RangedEnemySettings { ProjectilePrefab = prefab, WindUpDuration = 1,
                ProjectileLaunchProgress = .4f, EngagementRange = 100, ProjectileSpeed = 10, Cooldown = 1 });
            visual = em.CreateEntity(typeof(EnemyAnimation), typeof(EnemyAnimationPlayback), typeof(LocalToWorld));
            em.SetComponentData(visual, new EnemyAnimation { Owner = owner, Profile = (byte)EnemyAnimationProfile.Goblin, Samples = samples });
            em.AddBuffer<SkinMatrix>(visual).Add(default);
            var modelToOwner = float4x4.TRS(new float3(.2f, -1, 0), quaternion.RotateZ(.2f), new float3(1.3f));
            em.SetComponentData(visual, new LocalToWorld { Value = math.mul(transform.ToMatrix(), modelToOwner) });
            em.AddComponentData(owner, new RangedProjectileOrigin { Animation = visual, Root = visual,
                BindPosition = new float3(.1f, -.02f, .615f), ModelToOwner = modelToOwner, ChargeRadius = .13f });
            em.SetComponentData(em.CreateEntity(typeof(PlayerSnapshot)), new PlayerSnapshot { IsAvailable = true, Position = new float3(0, 0, 10) });
            world.SetTime(new TimeData(.02, .02f));
        }

        [TearDown]
        public void TearDown() { world.Dispose(); samples.Dispose(); }

        private float3 VisibleOrigin()
        {
            var em = world.EntityManager;
            var origin = em.GetComponentData<RangedProjectileOrigin>(owner);
            return math.transform(em.GetComponentData<LocalToWorld>(visual).Value,
                math.mul(em.GetBuffer<SkinMatrix>(visual)[0].Value, new float4(origin.BindPosition, 1)));
        }

        [TestCase(0f), TestCase(.4f), TestCase(1f)]
        public void Enemy002ShotStartsAtRenderedWandReleasePose(float release)
        {
            var em = world.EntityManager;
            var settings = em.GetComponentData<RangedEnemySettings>(owner); settings.ProjectileLaunchProgress = release;
            em.SetComponentData(owner, settings);
            em.SetComponentData(owner, new RangedAttackState { Phase = RangedAttackPhase.WindUp, SecondsRemaining = .01f });
            world.GetOrCreateSystem<RangedEnemyAttackSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<EnemyAnimationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            using var shots = em.CreateEntityQuery(typeof(RangedProjectile)).ToComponentDataArray<RangedProjectile>(Allocator.Temp);
            Assert.That(shots.Length, Is.EqualTo(1));
            Assert.That(math.distance(shots[0].Start, VisibleOrigin()), Is.LessThan(.0001f),
                "Release must match the rendered pose, including owner yaw/scale and model offset.");
        }

        [TestCase(EnemyLaunchPhase.Launched), TestCase(EnemyLaunchPhase.Recovering), TestCase(EnemyLaunchPhase.Defeated)]
        public void Enemy002InterruptedWindUpDoesNotEmit(EnemyLaunchPhase phase)
        {
            var em = world.EntityManager;
            em.SetComponentData(owner, new EnemyLaunchState { Phase = phase });
            world.GetOrCreateSystem<RangedEnemyAttackSystem>().Update(world.Unmanaged);
            Assert.That(em.GetComponentData<RangedAttackState>(owner).CancelledWindUps, Is.EqualTo(1));
            Assert.That(em.CreateEntityQuery(typeof(RangedProjectile)).CalculateEntityCount(), Is.Zero);
        }

        [Test]
        public void Enemy002ChargeFollowsSkinAndClearsOnPoolingOrRelease()
        {
            var go = new GameObject("Charge bridge test");
            var bridge = go.AddComponent<PlayerEcsBridge>();
            PlayerBridgeRegistry.TryGetBridge(out var previous); PlayerBridgeRegistry.Register(bridge);
            int count = 0; ProjectileVisualSample sample = default;
            bridge.ProjectileVisualReceived += value => { if (value.Kind == ProjectileVisualKind.RangedCharge) { sample = value; count++; } };
            try
            {
                var em = world.EntityManager;
                world.GetOrCreateSystem<EnemyAnimationSystem>().Update(world.Unmanaged);
                var system = world.GetOrCreateSystem<ProjectileVisualBridgeSystem>();
                system.Update(world.Unmanaged);
                Assert.That(count, Is.EqualTo(1));
                Assert.That(math.distance((float3)sample.Position, VisibleOrigin()), Is.LessThan(.0001f));
                Assert.That(sample.ChargeProgress, Is.EqualTo(.5f).Within(.0001f));
                em.SetComponentEnabled<RespawnRequest>(owner, true); count = 0;
                system.Update(world.Unmanaged); Assert.That(count, Is.Zero);
                em.SetComponentEnabled<RespawnRequest>(owner, false);
                em.SetComponentData(owner, new RangedAttackState { Phase = RangedAttackPhase.Cooldown });
                system.Update(world.Unmanaged); Assert.That(count, Is.Zero);
            }
            finally
            {
                PlayerBridgeRegistry.Unregister(bridge);
                if (previous != null) PlayerBridgeRegistry.Register(previous);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Enemy002BakingTransfersOriginFromAccessoryToCombatOwner()
        {
            var em = world.EntityManager;
            var origin = em.GetComponentData<RangedProjectileOrigin>(owner);
            em.RemoveComponent<RangedProjectileOrigin>(owner);
            em.AddComponentData(visual, origin);
            em.AddBuffer<Unity.Entities.Hybrid.Baking.AdditionalEntitiesBakingData>(visual);
            world.GetOrCreateSystem<EnemyAnimationVisualBakingSystem>().Update(world.Unmanaged);
            Assert.That(em.HasComponent<RangedProjectileOrigin>(owner), Is.True);
            Assert.That(em.HasComponent<RangedProjectileOrigin>(visual), Is.False);
            Assert.That(em.GetComponentData<RangedProjectileOrigin>(owner).Animation, Is.EqualTo(visual));
        }

        [Test]
        public void Vision006ChargeUsesAuthoredOrbWithoutRuntimeCollider()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CrowdPunch/Prefabs/EnemyRanged.prefab");
            var wand = System.Array.Find(prefab.GetComponentsInChildren<CrowdPunch.Authoring.EnemyAnimationAuthoring>(true), a => a.name == "Wand 01");
            Assert.That(wand.ProjectileOrigin, Is.Not.Null);
            Assert.That(wand.ProjectileOrigin.localPosition.z, Is.EqualTo(.615f).Within(.0001f));
            Assert.That(wand.ProjectileOrigin.GetComponent<SphereCollider>().enabled, Is.False);
            var charge = Resources.Load<CrowdPunch.Configuration.CombatVfxLibrary>("CombatVfxLibrary").RangedCharge;
            Assert.That(charge, Is.Not.Null);
            Assert.That(charge.GetComponentsInChildren<Collider>(true), Is.Empty);
            foreach (var ps in charge.GetComponentsInChildren<ParticleSystem>(true))
                Assert.That(ps.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.Local));
        }
    }
}
