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
    public sealed class ChickenTurnTransitionTests
    {
        private World world;
        private Entity owner, visual;
        private BlobAssetReference<EnemyAnimationSamples> samples;
        private BlobAssetReference<EnemyJogSamples> mask;

        [SetUp]
        public void Setup()
        {
            world = new World("Chicken turn transitions");
            var em = world.EntityManager;
            owner = em.CreateEntity(typeof(ChickenBoss), typeof(ChickenTuning), typeof(ChickenTurnState),
                typeof(ChickenTurnPlayback), typeof(LocalTransform), typeof(PhysicsVelocity));
            em.SetComponentData(owner, new ChickenBoss { Phase = ChickenPhase.Pause });
            em.SetComponentData(owner, LocalTransform.Identity);
            em.AddComponentData(owner, ChickenAnimationTuning.Default);
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var data = ref builder.ConstructRoot<EnemyAnimationSamples>();
            data.BoneCount = 3; data.FrameCount = 2;
            var durations = builder.Allocate(ref data.Durations, 11);
            var poses = builder.Allocate(ref data.Matrices, 66);
            for (int motion = 0; motion < 11; motion++)
            {
                durations[motion] = 2f;
                for (int frame = 0; frame < 2; frame++)
                {
                    int start = motion * 6 + frame * 3;
                    poses[start] = Pose(float3.zero);
                    poses[start + 1] = Pose(new float3(motion == 6 ? -1 : motion == 7 ? 1 : 0, -1, 0));
                    poses[start + 2] = Pose(new float3(0, motion == 6 || motion == 7 ? 999 : 2, 0));
                }
            }
            samples = builder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
            using var maskBuilder = new BlobBuilder(Allocator.Temp);
            ref var jog = ref maskBuilder.ConstructRoot<EnemyJogSamples>();
            jog.BoneCount = 3; jog.FrameCount = 2; jog.HipBoneIndex = 0; jog.HipBindInverse = float4x4.identity;
            maskBuilder.Allocate(ref jog.LegBones, 1)[0] = 1;
            mask = maskBuilder.CreateBlobAssetReference<EnemyJogSamples>(Allocator.Persistent);
            visual = em.CreateEntity(typeof(EnemyAnimationPlayback));
            em.AddComponentData(visual, new EnemyAnimation { Owner = owner, Samples = samples, JogSamples = mask, BlendResponse = 12 });
            var skin = em.AddBuffer<SkinMatrix>(visual);
            for (int i = 0; i < 3; i++) skin.Add(new SkinMatrix { Value = Pose(float3.zero) });
            Tick(.02f);
        }

        [TearDown]
        public void Cleanup()
        {
            world.Dispose();
            samples.Dispose(); mask.Dispose();
        }

        [TestCase(-1f)]
        [TestCase(1f)]
        public void Chicken007_SmallTurnsStayIdleAndEntryDoesNotSnap(float direction)
        {
            SetRate(direction);
            for (int i = 0; i < 3; i++) Tick(.02f);
            Assert.That(world.EntityManager.GetComponentData<ChickenTurnPlayback>(owner).Motion, Is.Zero);
            Assert.That(LegX, Is.Zero.Within(.0001f));
            for (int i = 0; i < 6; i++) Tick(.02f);
            var turn = world.EntityManager.GetComponentData<ChickenTurnPlayback>(owner);
            Assert.That(turn.Motion, Is.EqualTo(direction < 0 ? 6 : 7));
            Assert.That(turn.Weight, Is.EqualTo(.1f).Within(.001f));
            Assert.That(LegX, Is.EqualTo(direction * turn.Weight).Within(.001f));
            for (int i = 0; i < 9; i++) Tick(.02f);
            Assert.That(math.abs(LegX), Is.EqualTo(1f).Within(.001f));
            Assert.That(world.EntityManager.GetBuffer<SkinMatrix>(visual)[2].Value.c3.y, Is.EqualTo(2f).Within(.02f));
            Assert.That(world.EntityManager.GetComponentData<LocalTransform>(owner), Is.EqualTo(LocalTransform.Identity));
            Assert.That(world.EntityManager.GetComponentData<PhysicsVelocity>(owner).Linear, Is.EqualTo(float3.zero));
        }

        [Test]
        public void Chicken007_IdleYawJitterDoesNotStartTurnPlayback()
        {
            for (int i = 0; i < 100; i++)
            {
                SetRate(i % 2 == 0 ? .1f : -.1f);
                Tick(.02f);
            }
            Assert.That(world.EntityManager.GetComponentData<ChickenTurnPlayback>(owner).Motion, Is.Zero);
            Assert.That(LegX, Is.Zero.Within(.0001f));
        }

        [TestCase(ChickenPhase.WindUp, 2)]
        [TestCase(ChickenPhase.Cast, 5)]
        [TestCase(ChickenPhase.Rush, 1)]
        [TestCase(ChickenPhase.Stagger, 3)]
        [TestCase(ChickenPhase.Defeated, 4)]
        public void Chicken007_CombatPosesImmediatelyTakePriorityOverTurning(ChickenPhase phase, int motion)
        {
            SetRate(1f);
            for (int i = 0; i < 20; i++) Tick(.02f);
            Assert.That(LegX, Is.EqualTo(1f).Within(.001f));
            world.EntityManager.SetComponentData(owner, new ChickenBoss { Phase = phase });
            Tick(.02f);
            Assert.That(world.EntityManager.GetComponentData<ChickenTurnPlayback>(owner).Weight, Is.Zero);
            Assert.That(world.EntityManager.GetComponentData<EnemyAnimationPlayback>(visual).Initialized, Is.EqualTo(motion + 1));
        }

        [Test]
        public void Chicken007_ReturnsToExactIdleBeforeLongTurnClipFinishes()
        {
            SetRate(1f);
            for (int i = 0; i < 20; i++) Tick(.02f);
            Assert.That(LegX, Is.EqualTo(1f).Within(.001f));
            SetRate(0);
            for (int i = 0; i < 9; i++) Tick(.02f);
            Assert.That(world.EntityManager.GetComponentData<ChickenTurnPlayback>(owner).Motion, Is.Zero);
            Assert.That(LegX, Is.Zero.Within(.0001f), "No exponential leg-blend tail after the configured exit");
            Assert.That(world.EntityManager.GetComponentData<EnemyAnimationPlayback>(visual).Initialized, Is.EqualTo(1));
        }

        [TestCase(90f)]
        [TestCase(180f)]
        [TestCase(-90f)]
        public void Chicken007_MovementDirectionChangesKeepFullJogWeight(float degrees)
        {
            float radians = math.radians(degrees);
            var target = new float2(math.sin(radians), math.cos(radians));
            var movement = new float2(0, 1);
            for (int i = 0; i < 50; i++)
            {
                movement = ChickenSkinPose.BlendMovement(movement, target, .2f);
                Assert.That(math.length(movement), Is.EqualTo(1f).Within(.0001f));
            }
            Assert.That(math.distance(movement, target), Is.LessThan(.001f));
        }

        private float LegX => world.EntityManager.GetBuffer<SkinMatrix>(visual)[1].Value.c3.x;
        private void SetRate(float rate) => world.EntityManager.SetComponentData(owner, new ChickenTurnState { YawRate = rate });
        private void Tick(float delta)
        {
            world.SetTime(new TimeData(world.Time.ElapsedTime + delta, delta));
            world.GetOrCreateSystem<ChickenAnimationSystem>().Update(world.Unmanaged);
            world.EntityManager.CompleteAllTrackedJobs();
        }
        private static float3x4 Pose(float3 position) => new float3x4(new float3(1, 0, 0), new float3(0, 1, 0), new float3(0, 0, 1), position);
    }
}
