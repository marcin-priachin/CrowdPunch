using System.IO;
using CrowdPunch.Components;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace CrowdPunch.Tests
{
    public sealed class EnemyJogAnimationTests
    {
        private World world;
        private Entity owner, layered, original;
        private BlobAssetReference<EnemyAnimationSamples> body;
        private BlobAssetReference<EnemyJogSamples> jog;

        [SetUp]
        public void SetUp()
        {
            world = new World("Goblin leg layer");
            var em = world.EntityManager;
            owner = em.CreateEntity(typeof(DesiredMovement), typeof(EnemyMovementSettings), typeof(LocalTransform),
                typeof(EnemyLaunchState), typeof(RespawnRequest), typeof(PhysicsVelocity));
            em.SetComponentData(owner, LocalTransform.Identity);
            em.SetComponentData(owner, new EnemyMovementSettings { MoveSpeed = 4 });
            em.SetComponentEnabled<RespawnRequest>(owner, false);
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<EnemyAnimationSamples>();
                root.BoneCount = 3; root.FrameCount = 2;
                var durations = builder.Allocate(ref root.Durations, EnemyAnimationSamples.MotionCount);
                var matrices = builder.Allocate(ref root.Matrices, EnemyAnimationSamples.MotionCount * 6);
                for (int motion = 0; motion < EnemyAnimationSamples.MotionCount; motion++)
                {
                    durations[motion] = 1;
                    for (int frame = 0; frame < 2; frame++)
                        for (int bone = 0; bone < 3; bone++)
                            matrices[(motion * 2 + frame) * 3 + bone] = Pose(new float3(motion * 10 + bone, frame, 0));
                }
                body = builder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
            }
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<EnemyJogSamples>();
                root.BoneCount = 3; root.FrameCount = 2; root.HipBoneIndex = 0;
                root.HipBindInverse = float4x4.Translate(new float3(0, 0, 3));
                builder.Allocate(ref root.LegBones, 1)[0] = 2;
                var durations = builder.Allocate(ref root.Durations, EnemyJogSamples.DirectionCount);
                var distances = builder.Allocate(ref root.CycleDistances, EnemyJogSamples.DirectionCount);
                var matrices = builder.Allocate(ref root.Matrices, EnemyJogSamples.DirectionCount * 2);
                for (int direction = 0; direction < EnemyJogSamples.DirectionCount; direction++)
                {
                    durations[direction] = 1;
                    distances[direction] = direction == 2 ? 4 : 2;
                    for (int frame = 0; frame < 2; frame++)
                        matrices[direction * 2 + frame] = Pose(new float3(direction + 1, frame, 0));
                }
                jog = builder.CreateBlobAssetReference<EnemyJogSamples>(Allocator.Persistent);
            }
            layered = Visual(jog); original = Visual(default);
        }

        [Test]
        public void Enemy014ShieldOverridesUpperBodyWhileJogLegsFollowMotionAndArmorBreak()
        {
            var em = world.EntityManager;
            em.AddComponentData(owner, EnemyArmor.Fresh);
            em.SetComponentData(owner, new PhysicsVelocity { Linear = new float3(2, 0, 2) });
            foreach (var visual in new[] { layered, original })
            {
                var animation = em.GetComponentData<EnemyAnimation>(visual);
                animation.Profile = (byte)EnemyAnimationProfile.GoblinArmored;
                em.SetComponentData(visual, animation);
            }
            Tick();
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(layered).GoblinMotion, Is.EqualTo(8));
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(layered).JogPhase, Is.GreaterThan(0));
            Assert.That(em.GetBuffer<SkinMatrix>(layered)[1].Value, Is.EqualTo(em.GetBuffer<SkinMatrix>(original)[1].Value));
            Assert.That(em.GetBuffer<SkinMatrix>(layered)[2].Value, Is.Not.EqualTo(em.GetBuffer<SkinMatrix>(original)[2].Value));
            em.SetComponentData(owner, new EnemyArmor { Stages = 1, HitSequence = 1 });
            Tick();
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(layered).GoblinMotion, Is.EqualTo(8));
            em.SetComponentData(owner, new EnemyArmor { Stages = 0, HitSequence = 2 });
            Tick();
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(layered).GoblinMotion, Is.EqualTo(5));
            Tick(1.1f);
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(layered).GoblinMotion, Is.EqualTo(2));
            Assert.That(em.GetComponentData<LocalTransform>(owner), Is.EqualTo(LocalTransform.Identity));
            Assert.That(em.GetComponentData<PhysicsVelocity>(owner).Linear, Is.EqualTo(new float3(2, 0, 2)));
        }

        [Test]
        public void Enemy014MirroredEquipmentRetainsItsAuthoredReflectionDuringPoseBlending()
        {
            var em = world.EntityManager;
            em.AddComponentData(owner, EnemyArmor.Fresh);
            var animation = em.GetComponentData<EnemyAnimation>(layered);
            animation.Profile = (byte)EnemyAnimationProfile.GoblinArmored;
            em.SetComponentData(layered, animation);
            for (int frame = 0; frame < 2; frame++)
                body.Value.Matrices[(8 * 2 + frame) * 3 + 1] = new float3x4(
                    new float3(-1, 0, 0), new float3(0, 1, 0), new float3(0, 0, 1), new float3(1, 2, 3));
            Tick();
            var pose = em.GetBuffer<SkinMatrix>(layered)[1].Value;
            Assert.That(pose.c0, Is.EqualTo(new float3(-1, 0, 0)));
            Assert.That(pose.c1, Is.EqualTo(new float3(0, 1, 0)));
            Assert.That(pose.c2, Is.EqualTo(new float3(0, 0, 1)));
            Assert.That(pose.c3, Is.EqualTo(new float3(1, 2, 3)));
        }

        private static float3x4 Pose(float3 position) => new float3x4(new float3(1, 0, 0),
            new float3(0, 1, 0), new float3(0, 0, 1), position);

        private Entity Visual(BlobAssetReference<EnemyJogSamples> layer)
        {
            var em = world.EntityManager;
            var entity = em.CreateEntity(typeof(EnemyAnimation), typeof(EnemyAnimationPlayback));
            em.SetComponentData(entity, new EnemyAnimation { Owner = owner, Profile = (byte)EnemyAnimationProfile.Goblin,
                Samples = body, JogSamples = layer, SpawnMotion = 7 });
            em.SetComponentData(entity, new EnemyAnimationPlayback { Initialized = 1 });
            em.AddBuffer<SkinMatrix>(entity).ResizeUninitialized(3);
            return entity;
        }

        [TearDown]
        public void TearDown() { world.Dispose(); body.Dispose(); jog.Dispose(); }

        private void Tick(float delta = .125f)
        {
            world.SetTime(new TimeData(world.Time.ElapsedTime + delta, delta));
            world.GetOrCreateSystem<EnemyAnimationSystem>().Update(world.Unmanaged);
            world.EntityManager.CompleteAllTrackedJobs();
        }

        private void AssertOriginalUpperBody()
        {
            var em = world.EntityManager;
            Assert.That(em.GetBuffer<SkinMatrix>(layered)[0].Value, Is.EqualTo(em.GetBuffer<SkinMatrix>(original)[0].Value));
            Assert.That(em.GetBuffer<SkinMatrix>(layered)[1].Value, Is.EqualTo(em.GetBuffer<SkinMatrix>(original)[1].Value));
            Assert.That(em.GetComponentData<LocalTransform>(owner), Is.EqualTo(LocalTransform.Identity));
            Assert.That(em.GetComponentData<PhysicsVelocity>(owner), Is.EqualTo(new PhysicsVelocity()));
        }

        [TestCase(0f, 1f)]
        [TestCase(45f, 2f)]
        [TestCase(90f, 3f)]
        [TestCase(180f, 5f)]
        [TestCase(270f, 7f)]
        [TestCase(22.5f, 1.5f)]
        public void Info004JogLegsBlendInLocalMovementDirection(float degrees, float expected)
        {
            var em = world.EntityManager;
            float angle = math.radians(degrees);
            em.SetComponentData(owner, new DesiredMovement { Direction = new float3(math.sin(angle), 0, math.cos(angle)), Speed = 4 });
            Tick();
            AssertOriginalUpperBody();
            var hip = em.GetBuffer<SkinMatrix>(layered)[0].Value.c3;
            var leg = em.GetBuffer<SkinMatrix>(layered)[2].Value.c3;
            Assert.That(leg.x - hip.x, Is.EqualTo(expected).Within(.001f));
            Assert.That(leg.z - hip.z, Is.EqualTo(3).Within(.001f), "Restore the original hip transform before attaching the jog legs.");
        }

        [Test]
        public void Info004JogLegsUseFacingBlendOutAtIdleAndKeepUpperFiring()
        {
            var em = world.EntityManager;
            em.SetComponentData(owner, new DesiredMovement { Direction = new float3(1, 0, 0), Speed = 4 });
            em.SetComponentData(owner, LocalTransform.FromRotation(quaternion.RotateY(math.PI / 2)));
            em.AddComponentData(owner, new RangedEnemySettings { WindUpDuration = 1, ProjectileLaunchProgress = .4f });
            em.AddComponentData(owner, new RangedAttackState { Phase = RangedAttackPhase.WindUp, SecondsRemaining = .5f });
            Tick();
            Assert.That(em.GetBuffer<SkinMatrix>(layered)[2].Value.c3.x - em.GetBuffer<SkinMatrix>(layered)[0].Value.c3.x,
                Is.EqualTo(1).Within(.001f), "World right is local forward while facing right.");
            Assert.That(em.GetBuffer<SkinMatrix>(layered)[1].Value, Is.EqualTo(em.GetBuffer<SkinMatrix>(original)[1].Value));
            em.SetComponentData(owner, new RangedAttackState { Phase = RangedAttackPhase.Cooldown, ProjectilesSpawned = 1 });
            Tick();
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(layered).GoblinMotion, Is.EqualTo(4));
            Assert.That(em.GetBuffer<SkinMatrix>(layered)[1].Value, Is.EqualTo(em.GetBuffer<SkinMatrix>(original)[1].Value));
            em.SetComponentData(owner, new DesiredMovement());
            Tick();
            Assert.That(em.GetBuffer<SkinMatrix>(layered)[2].Value, Is.EqualTo(em.GetBuffer<SkinMatrix>(original)[2].Value));
        }

        [TestCase(EnemyLaunchPhase.Launched)]
        [TestCase(EnemyLaunchPhase.Recovering)]
        [TestCase(EnemyLaunchPhase.Defeated)]
        public void Combat011JogLegsDoNotOverrideLaunchRecoveryOrDefeat(EnemyLaunchPhase phase)
        {
            var em = world.EntityManager;
            em.SetComponentData(owner, new DesiredMovement { Direction = math.forward(), Speed = 4 });
            Tick();
            em.SetComponentData(owner, new EnemyLaunchState { Phase = phase, RecoverySecondsRemaining = 1, LaunchSequence = 1 });
            Tick();
            for (int bone = 0; bone < 3; bone++)
                Assert.That(em.GetBuffer<SkinMatrix>(layered)[bone].Value, Is.EqualTo(em.GetBuffer<SkinMatrix>(original)[bone].Value));
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(layered).Movement, Is.EqualTo(float2.zero));
        }

        [Test]
        public void Info004ChargeKeepsFullRunForwardPoseWithoutJogLegOverrides()
        {
            var em = world.EntityManager;
            em.SetComponentData(owner, new DesiredMovement { Direction = math.forward(), Speed = 4 });
            Tick();
            em.AddComponentData(owner, new EnemyContactAttemptState { IsAttempting = 1 });
            Tick();
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(layered).GoblinMotion, Is.EqualTo(2));
            for (int bone = 0; bone < 3; bone++)
                Assert.That(em.GetBuffer<SkinMatrix>(layered)[bone].Value, Is.EqualTo(em.GetBuffer<SkinMatrix>(original)[bone].Value));
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(layered).Movement, Is.EqualTo(float2.zero));
        }

        [Test]
        public void Info006JogLegsDoNotOverrideGroundEmergence()
        {
            var em = world.EntityManager;
            em.SetComponentData(owner, new DesiredMovement { Direction = math.forward(), Speed = 4 });
            em.AddComponentData(owner, new EnemySpawnAnimation { Duration = 1 });
            em.SetComponentEnabled<EnemySpawnAnimation>(owner, true);
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering, RecoverySecondsRemaining = .5f });
            Tick();
            for (int bone = 0; bone < 3; bone++)
                Assert.That(em.GetBuffer<SkinMatrix>(layered)[bone].Value, Is.EqualTo(em.GetBuffer<SkinMatrix>(original)[bone].Value));
        }

        [TestCase("Baseline")]
        [TestCase("Ranged")]
        [TestCase("Elite")]
        public void Info004GoblinPrefabLegMasksExcludeUpperBodyAndEquipment(string kind)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
                kind == "Elite" ? "Assets/CrowdPunch/Prefabs/EliteEnemy.prefab" : "Assets/CrowdPunch/Prefabs/Enemy" + kind + ".prefab");
            int layeredBodies = 0;
            foreach (var authoring in prefab.GetComponentsInChildren<CrowdPunch.Authoring.EnemyAnimationAuthoring>(true))
            {
                var renderer = authoring.GetComponent<UnityEngine.SkinnedMeshRenderer>();
                if (renderer.bones.Length == 1) { Assert.That(authoring.JogSamples, Is.Null); continue; }
                layeredBodies++;
                Assert.That(authoring.JogSamples, Is.Not.Null);
                if (kind == "Elite")
                {
                    Assert.That(authoring.JogCycleDistances, Has.Length.EqualTo(8));
                    foreach (float distance in authoring.JogCycleDistances)
                        Assert.That(distance, Is.GreaterThan(.01f).And.LessThan(10));
                }
                using var reader = new BinaryReader(new MemoryStream(authoring.JogSamples.bytes));
                Assert.That(reader.ReadInt32(), Is.EqualTo(0x43504A31));
                reader.ReadString();
                Assert.That(reader.ReadInt32(), Is.EqualTo(renderer.bones.Length));
                Assert.That(reader.ReadInt32(), Is.EqualTo(32));
                Assert.That(renderer.bones[reader.ReadInt32()].name, Is.EqualTo("RigPelvis"));
                int count = reader.ReadInt32();
                for (int i = 0; i < 16; i++) reader.ReadSingle();
                Assert.That(count, Is.EqualTo(8));
                for (int i = 0; i < count; i++) Assert.That(renderer.bones[reader.ReadInt32()].name, Does.Contain("Leg"));
            }
            Assert.That(layeredBodies, Is.EqualTo(1));
        }

        [TestCase(0f, 2f)]
        [TestCase(.5f, .5f)]
        public void Enemy009EliteJogFollowsActualSetupMotionAtFullLegWeight(float requestedSpeed, float actualSpeed)
        {
            var em = world.EntityManager;
            em.AddComponentData(owner, new ElitePunchState { Phase = ElitePunchPhase.Repositioning });
            em.SetComponentData(owner, new EnemyMovementSettings { MoveSpeed = 30 });
            em.SetComponentData(owner, new DesiredMovement { Direction = math.forward(), Speed = requestedSpeed });
            em.SetComponentData(owner, new PhysicsVelocity { Linear = new float3(0, 0, actualSpeed) });
            Tick(.1f);
            var hip = em.GetBuffer<SkinMatrix>(layered)[0].Value.c3;
            var leg = em.GetBuffer<SkinMatrix>(layered)[2].Value.c3;
            Assert.That(leg.x - hip.x, Is.EqualTo(1).Within(.001f), "Slow setup and residual braking motion need a full directional step.");
            Assert.That(em.GetBuffer<SkinMatrix>(layered)[1].Value, Is.EqualTo(em.GetBuffer<SkinMatrix>(original)[1].Value));
            Assert.That(em.GetComponentData<PhysicsVelocity>(owner).Linear.z, Is.EqualTo(actualSpeed));
        }

        [Test]
        public void Enemy009EliteJogUsesResolvedVelocityRelativeToCurrentFacingWithoutDirectionLag()
        {
            var em = world.EntityManager;
            em.AddComponentData(owner, new ElitePunchState { Phase = ElitePunchPhase.Repositioning });
            em.SetComponentData(owner, LocalTransform.FromRotation(quaternion.RotateY(math.PI / 2)));
            em.SetComponentData(owner, new DesiredMovement { Direction = new float3(-1, 0, 0), Speed = 4 });
            em.SetComponentData(owner, new PhysicsVelocity { Linear = new float3(1, 0, 0) });
            var animation = em.GetComponentData<EnemyAnimation>(layered); animation.BlendResponse = 12;
            em.SetComponentData(layered, animation);
            em.SetComponentData(layered, new EnemyAnimationPlayback { Initialized = 1, Movement = new float2(0, -1) });
            Tick(.05f);
            var movement = em.GetComponentData<EnemyAnimationPlayback>(layered).Movement;
            Assert.That(movement.x, Is.EqualTo(0).Within(.001f));
            Assert.That(movement.y, Is.EqualTo(1).Within(.001f), "Actual world-right motion is local forward after facing right.");
        }

        [Test]
        public void Enemy009BlockedEliteStopsSteppingDespiteSetupIntent()
        {
            var em = world.EntityManager;
            em.AddComponentData(owner, new ElitePunchState { Phase = ElitePunchPhase.Repositioning });
            em.SetComponentData(owner, new DesiredMovement { Direction = math.forward(), Speed = 30 });
            Tick(.1f);
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(layered).JogPhase, Is.Zero);
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(layered).Movement, Is.EqualTo(float2.zero));
            Assert.That(em.GetBuffer<SkinMatrix>(layered)[2].Value, Is.EqualTo(em.GetBuffer<SkinMatrix>(original)[2].Value));
        }

        [Test]
        public void Enemy009EliteGaitAdvancesWithTravelledDistanceRatherThanWallClock()
        {
            var em = world.EntityManager;
            em.AddComponentData(owner, new ElitePunchState { Phase = ElitePunchPhase.Repositioning });
            em.SetComponentData(owner, new DesiredMovement { Direction = math.forward(), Speed = 4 });
            em.SetComponentData(owner, new PhysicsVelocity { Linear = new float3(0, 0, 2) });
            Tick(.1f);
            float slow = em.GetComponentData<EnemyAnimationPlayback>(layered).JogPhase;
            em.SetComponentData(layered, new EnemyAnimationPlayback { Initialized = 1 });
            em.SetComponentData(owner, new PhysicsVelocity { Linear = new float3(0, 0, 4) });
            Tick(.1f);
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(layered).JogPhase, Is.EqualTo(slow * 2).Within(.001f));
        }

        [TestCase(0f, 1f, .5f)]
        [TestCase(.5f, 1f, .05f)]
        [TestCase(2f, 1f, .2f)]
        [TestCase(2f, 2f, .1f)]
        public void Enemy009LegPlaybackCapUsesOriginalJogSpeed(float cap, float duration, float expectedPhase)
        {
            var em = world.EntityManager;
            em.AddComponentData(owner, new ElitePunchState { Phase = ElitePunchPhase.Repositioning });
            em.SetComponentData(owner, new PhysicsVelocity { Linear = new float3(0, 0, 10) });
            var animation = em.GetComponentData<EnemyAnimation>(layered);
            animation.MaxLegAnimationSpeed = cap;
            em.SetComponentData(layered, animation);
            for (int i = 0; i < 8; i++) jog.Value.Durations[i] = duration;
            Tick(.1f);
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(layered).JogPhase, Is.EqualTo(expectedPhase).Within(.001f));
            Assert.That(em.GetBuffer<SkinMatrix>(layered)[1].Value, Is.EqualTo(em.GetBuffer<SkinMatrix>(original)[1].Value));
            Assert.That(em.GetComponentData<PhysicsVelocity>(owner).Linear.z, Is.EqualTo(10));
        }

        [TestCase(90f, 4f)]
        [TestCase(67.5f, 3f)]
        public void Enemy009EliteGaitUsesTheBlendedDirectionalStride(float degrees, float stride)
        {
            var em = world.EntityManager;
            em.AddComponentData(owner, new ElitePunchState { Phase = ElitePunchPhase.Repositioning });
            float angle = math.radians(degrees);
            em.SetComponentData(owner, new PhysicsVelocity { Linear = new float3(math.sin(angle), 0, math.cos(angle)) * 2 });
            Tick(.1f);
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(layered).JogPhase, Is.EqualTo(.2f / stride).Within(.001f));
        }

        [TestCase(ElitePunchPhase.Repositioning)]
        [TestCase(ElitePunchPhase.CommittedSweep)]
        public void Enemy009EliteContactAttemptCannotSuppressJogLegs(ElitePunchPhase phase)
        {
            var em = world.EntityManager;
            em.AddComponentData(owner, new ElitePunchState { Phase = phase, SweepProgress = .4f });
            em.AddComponentData(owner, new EnemyContactAttemptState { IsAttempting = 1 });
            em.SetComponentData(owner, new PhysicsVelocity { Linear = new float3(0, 0, 1) });
            Tick(.1f);
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(layered).JogPhase, Is.GreaterThan(0));
            Assert.That(em.GetBuffer<SkinMatrix>(layered)[2].Value.c3.x
                - em.GetBuffer<SkinMatrix>(layered)[0].Value.c3.x, Is.EqualTo(1).Within(.001f));
            Assert.That(em.GetBuffer<SkinMatrix>(layered)[1].Value, Is.EqualTo(em.GetBuffer<SkinMatrix>(original)[1].Value));
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(layered).GoblinMotion,
                Is.EqualTo(phase == ElitePunchPhase.CommittedSweep ? 4 : 1));
        }

        [Test]
        public void Enemy009EliteSweepKeepsAuthoredUpperBodyThroughDamageAndJogBlending()
        {
            var em = world.EntityManager;
            em.AddComponentData(owner, new ElitePunchState { Phase = ElitePunchPhase.CommittedSweep, SweepProgress = .4f });
            em.AddComponentData(owner, new Health { Current = 10, Max = 10 });
            em.SetComponentData(owner, new DesiredMovement { Direction = math.normalize(new float3(1, 0, 2)), Speed = 4 });
            em.SetComponentData(owner, new PhysicsVelocity { Linear = math.normalize(new float3(1, 0, 2)) * 4 });
            for (int direction = 0; direction < 8; direction++)
            {
                var rotation = new float3x3(quaternion.RotateY(direction % 2 == 0 ? 1.4f : -1.4f));
                for (int frame = 0; frame < 2; frame++)
                    jog.Value.Matrices[direction * 2 + frame] = new float3x4(rotation.c0, rotation.c1, rotation.c2, float3.zero);
            }
            Tick();
            em.SetComponentData(owner, new Health { Current = 9, Max = 10 });
            Tick();
            Assert.That(em.GetBuffer<SkinMatrix>(layered)[0].Value, Is.EqualTo(em.GetBuffer<SkinMatrix>(original)[0].Value));
            Assert.That(em.GetBuffer<SkinMatrix>(layered)[1].Value, Is.EqualTo(em.GetBuffer<SkinMatrix>(original)[1].Value));
            Assert.That(em.GetComponentData<LocalTransform>(owner), Is.EqualTo(LocalTransform.Identity));
            Assert.That(math.length(em.GetComponentData<PhysicsVelocity>(owner).Linear), Is.EqualTo(4).Within(.001f));
            Assert.AreEqual(4, em.GetComponentData<EnemyAnimationPlayback>(layered).GoblinMotion);
            var leg = em.GetBuffer<SkinMatrix>(layered)[2].Value;
            Assert.That(math.length(leg.c0), Is.EqualTo(1).Within(.0001f));
            Assert.That(math.length(leg.c1), Is.EqualTo(1).Within(.0001f));
            Assert.That(math.length(leg.c2), Is.EqualTo(1).Within(.0001f));
        }

        [Test]
        public void Info004ElitePrefabKeepsEveryAuthoredTransformScaleAndTexturedCelMaterials()
        {
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
                "Assets/CrowdPunch/Prefabs/Enemies/Goblin Giant Elite Variant.prefab");
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/CrowdPunch/Prefabs/EliteEnemy.prefab");
            var visual = prefab.GetComponentInChildren<UnityEngine.Animator>(true).transform;
            Assert.AreEqual(source.transform.localScale, visual.localScale);
            foreach (var transform in source.GetComponentsInChildren<UnityEngine.Transform>(true))
            {
                string path = UnityEditor.AnimationUtility.CalculateTransformPath(transform, source.transform);
                var target = string.IsNullOrEmpty(path) ? visual : visual.Find(path);
                Assert.NotNull(target, path);
                Assert.AreEqual(transform.localScale, target.localScale, path);
            }
            foreach (var part in prefab.GetComponentsInChildren<CrowdPunch.Authoring.EnemyAnimationAuthoring>(true))
            {
                Assert.AreEqual(EnemyAnimationProfile.Goblin, part.Profile);
                Assert.AreEqual(7, part.SpawnMotion);
                foreach (var material in part.GetComponent<UnityEngine.SkinnedMeshRenderer>().sharedMaterials)
                {
                    Assert.AreEqual("CrowdPunch/Cel/Enemy", material.shader.name);
                    Assert.NotNull(material.GetTexture("_ColorMap"));
                    var texture = material.GetTexture("_ColorMap");
                    CrowdPunch.Editor.EnemyAppearanceMaterials.Configure(material);
                    Assert.AreSame(texture, material.GetTexture("_ColorMap"));
                }
            }
        }

        [TestCase("Baseline")]
        [TestCase("Ranged")]
        [TestCase("Elite")]
        public void Info004JogSamplingPreservesAuthoredScaleControllerAndOriginalSamples(string kind)
        {
            var root = UnityEditor.PrefabUtility.LoadPrefabContents(kind == "Elite" ? "Assets/CrowdPunch/Prefabs/EliteEnemy.prefab"
                : "Assets/CrowdPunch/Prefabs/Enemy" + kind + ".prefab");
            try
            {
                var animator = root.GetComponentInChildren<UnityEngine.Animator>();
                var scale = animator.transform.localScale;
                var controller = animator.runtimeAnimatorController;
                var parts = root.GetComponentsInChildren<CrowdPunch.Authoring.EnemyAnimationAuthoring>(true);
                var originals = new byte[parts.Length][];
                for (int i = 0; i < parts.Length; i++) originals[i] = parts[i].Samples.bytes;
                CrowdPunch.Editor.GoblinJogSampling.Configure(root, "Enemy" + kind);
                Assert.That(animator.transform.localScale, Is.EqualTo(scale));
                Assert.That(animator.runtimeAnimatorController, Is.EqualTo(controller));
                for (int i = 0; i < parts.Length; i++) Assert.That(parts[i].Samples.bytes, Is.EqualTo(originals[i]));
            }
            finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
