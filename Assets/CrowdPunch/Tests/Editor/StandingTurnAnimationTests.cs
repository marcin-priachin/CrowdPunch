using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
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
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class StandingTurnAnimationTests
    {
        private World world;
        private Entity owner, visual;
        private BlobAssetReference<EnemyAnimationSamples> original;
        private BlobAssetReference<StandingTurnSamples> turns;

        [SetUp]
        public void Setup()
        {
            world = new World("Elite and Dino standing turns");
            var em = world.EntityManager;
            owner = em.CreateEntity(typeof(LocalTransform), typeof(PhysicsVelocity), typeof(ElitePunchState), typeof(EnemyLaunchState), typeof(RespawnRequest));
            em.SetComponentData(owner, LocalTransform.Identity);
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            em.SetComponentEnabled<RespawnRequest>(owner, false);
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var source = ref builder.ConstructRoot<EnemyAnimationSamples>();
            source.BoneCount = 3; source.FrameCount = 2;
            var durations = builder.Allocate(ref source.Durations, 11);
            var poses = builder.Allocate(ref source.Matrices, 66);
            for (int motion = 0; motion < 11; motion++)
            {
                durations[motion] = 2;
                for (int frame = 0; frame < 2; frame++)
                {
                    poses[motion * 6 + frame * 3] = Pose(float3.zero);
                    poses[motion * 6 + frame * 3 + 1] = Pose(new float3(0, 2, 0));
                    poses[motion * 6 + frame * 3 + 2] = Pose(new float3(0, -1, 0));
                }
            }
            original = builder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
            using var turnBuilder = new BlobBuilder(Allocator.Temp);
            ref var data = ref turnBuilder.ConstructRoot<StandingTurnSamples>();
            data.BoneCount = 3; data.FrameCount = 2; data.HipBoneIndex = 0; data.HipBindInverse = float4x4.identity;
            turnBuilder.Allocate(ref data.LegBones, 1)[0] = 2;
            var lengths = turnBuilder.Allocate(ref data.Durations, 2); lengths[0] = lengths[1] = 2;
            var legs = turnBuilder.Allocate(ref data.Matrices, 4);
            legs[0] = legs[1] = Pose(new float3(-1, -1, 0));
            legs[2] = legs[3] = Pose(new float3(1, -1, 0));
            turns = turnBuilder.CreateBlobAssetReference<StandingTurnSamples>(Allocator.Persistent);
            visual = em.CreateEntity(typeof(EnemyAnimationPlayback), typeof(StandingTurnPlayback));
            em.AddComponentData(visual, new EnemyAnimation { Owner = owner, Samples = original });
            em.AddComponentData(visual, new StandingTurnAnimation { Samples = turns, MinimumAngle = math.radians(10), MinimumSpeed = math.radians(15),
                BlendInDuration = .2f, BlendOutDuration = .08f, StopDelay = .06f });
            var skin = em.AddBuffer<SkinMatrix>(visual);
            for (int i = 0; i < 3; i++) skin.Add(new SkinMatrix());
            Tick();
        }

        [TearDown]
        public void Cleanup() { world.Dispose(); original.Dispose(); turns.Dispose(); }

        [TestCase(false, -1)] [TestCase(false, 1)] [TestCase(true, -1)] [TestCase(true, 1)]
        public void Info004_StandingTurnsBlendOnlyLegsAndReturnPromptly(bool dino, int direction)
        {
            if (dino) UseDino();
            Turn(16 * direction); Tick();
            Assert.That(Playback.Direction, Is.EqualTo(direction));
            Assert.That(Playback.Weight, Is.EqualTo(.1f).Within(.001f));
            var skin = world.EntityManager.GetBuffer<SkinMatrix>(visual);
            Assert.That(skin[2].Value.c3.x, Is.EqualTo(direction * .1f).Within(.001f));
            Assert.That(skin[0].Value, Is.EqualTo(Pose(float3.zero)));
            Assert.That(skin[1].Value, Is.EqualTo(Pose(new float3(0, 2, 0))));
            var pose = world.EntityManager.GetComponentData<LocalTransform>(owner);
            for (int i = 0; i < 10; i++) Tick();
            Assert.That(Playback.Direction, Is.Zero);
            Assert.That(world.EntityManager.GetBuffer<SkinMatrix>(visual)[2].Value, Is.EqualTo(Pose(new float3(0, -1, 0))));
            Assert.That(world.EntityManager.GetComponentData<LocalTransform>(owner), Is.EqualTo(pose));
            Assert.That(world.EntityManager.GetComponentData<PhysicsVelocity>(owner).Linear, Is.EqualTo(float3.zero));
        }

        [TestCase(false)] [TestCase(true)]
        public void Info004_SmallAndSlowYawDoesNotTriggerFootwork(bool dino)
        {
            if (dino) UseDino();
            Turn(6); Tick(); Tick();
            Assert.That(Playback.Direction, Is.Zero);
            for (int i = 0; i < 100; i++) { Turn(.1f); Tick(); }
            Assert.That(Playback.Direction, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void Info004_MovingYawDoesNotCarryIntoStandingTurns(bool dino)
        {
            if (dino) UseDino();
            world.EntityManager.SetComponentData(owner, new PhysicsVelocity { Linear = new float3(1, 0, 0) });
            for (int i = 0; i < 15; i++) { Turn(2); Tick(); }
            world.EntityManager.SetComponentData(owner, new PhysicsVelocity());
            Tick();
            Assert.That(Playback.Direction, Is.Zero);
            Turn(16); Tick();
            Assert.That(Playback.Direction, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void Info004_SustainedTurnsRepeatAndReverse(bool dino)
        {
            if (dino) UseDino();
            for (int i = 0; i < 240; i++) { Turn(2); Tick(); }
            Assert.That(Playback.Direction, Is.EqualTo(1));
            Assert.That(Playback.Weight, Is.EqualTo(1).Within(.001f));
            Assert.That(Playback.Phase, Is.InRange(0f, 1f));
            Turn(-16); Tick(); Assert.That(Playback.Direction, Is.EqualTo(-1));
        }

        [TestCase(EnemyLaunchPhase.Launched)] [TestCase(EnemyLaunchPhase.Recovering)] [TestCase(EnemyLaunchPhase.Defeated)]
        public void Combat011_LifecyclePosesInterruptEliteTurns(EnemyLaunchPhase phase)
        {
            Turn(16); Tick();
            world.EntityManager.SetComponentData(owner, new EnemyLaunchState { Phase = phase });
            Turn(16); Tick(); Assert.That(Playback.Weight, Is.Zero);
        }

        [Test]
        public void Combat011_PoolingClearsTurnAndReuseStartsFromCurrentFacing()
        {
            Turn(16); Tick();
            world.EntityManager.SetComponentData(owner, new RespawnRequest { IsPooled = 1 });
            world.EntityManager.SetComponentEnabled<RespawnRequest>(owner, true);
            Turn(90); Tick(); Assert.That(Playback.Weight, Is.Zero);
            world.EntityManager.SetComponentEnabled<RespawnRequest>(owner, false);
            Tick(); Assert.That(Playback.Direction, Is.Zero);
        }

        [Test]
        public void Enemy009_EliteSweepAndHitReactionInterruptTurns()
        {
            Turn(16); Tick();
            world.EntityManager.SetComponentData(owner, new ElitePunchState { Phase = ElitePunchPhase.CommittedSweep });
            Turn(16); Tick(); Assert.That(Playback.Weight, Is.Zero);
            world.EntityManager.SetComponentData(owner, new ElitePunchState());
            Turn(16); Tick();
            world.EntityManager.SetComponentData(visual, new EnemyAnimationPlayback { HitActive = 1 });
            Turn(16); Tick(); Assert.That(Playback.Weight, Is.Zero);
        }

        [TestCase(DinoPhase.Stagger)] [TestCase(DinoPhase.Defeated)]
        public void Pillar006_DinoCombatReactionsInterruptTurns(DinoPhase phase)
        {
            UseDino(); Turn(16); Tick();
            world.EntityManager.SetComponentData(owner, new DinoBoss { Phase = phase });
            Turn(16); Tick(); Assert.That(Playback.Weight, Is.Zero);
        }

        [TestCase(40)] [TestCase(80)] [TestCase(0)]
        public void Info004_SavedBodiesUseSharedPlayerTurnSources(int level)
        {
            if (level == 0)
            {
                Verify(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CrowdPunch/Prefabs/EliteEnemy.prefab"));
                return;
            }
            var scene = EditorSceneManager.OpenPreviewScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{level:00}/Campaign_{level:00} Sub Scene.unity");
            try { Verify(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DinoBossAuthoring>()).Single().gameObject); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void Verify(GameObject root)
        {
            var animations = root.GetComponentsInChildren<EnemyAnimationAuthoring>(true);
            Assert.That(animations.Count(a => a.StandingTurnSamples != null), Is.EqualTo(1), "Only the body receives the lower-body layer");
            var authoring = animations.Single(a => a.StandingTurnSamples != null);
            Assert.That(authoring.JogSamples, Is.Not.Null);
            using var reader = new BinaryReader(new MemoryStream(authoring.StandingTurnSamples.bytes));
            Assert.That(reader.ReadInt32(), Is.EqualTo(0x43505431));
            string hash = "StandingTurn1" + AssetDatabase.GetAssetDependencyHash("Assets/CrowdPunch/Animation/PlayerLeftTurn.anim")
                + AssetDatabase.GetAssetDependencyHash("Assets/CrowdPunch/Animation/PlayerRightTurn.anim")
                + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(authoring.GetComponent<SkinnedMeshRenderer>().sharedMesh))
                + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(root.GetComponentInChildren<Animator>(true).avatar));
            Assert.That(reader.ReadString(), Is.EqualTo(hash));
            Assert.That(authoring.StandingTurnMinimumAngle, Is.EqualTo(10));
            Assert.That(authoring.StandingTurnBlendInDuration, Is.EqualTo(.2f));
            Assert.That(authoring.StandingTurnBlendOutDuration, Is.EqualTo(.08f));
        }

        private StandingTurnPlayback Playback => world.EntityManager.GetComponentData<StandingTurnPlayback>(visual);
        private void UseDino() { world.EntityManager.RemoveComponent<ElitePunchState>(owner); world.EntityManager.AddComponentData(owner, new DinoBoss()); }
        private void Turn(float degrees)
        {
            var pose = world.EntityManager.GetComponentData<LocalTransform>(owner);
            pose.Rotation = math.mul(quaternion.RotateY(math.radians(degrees)), pose.Rotation);
            world.EntityManager.SetComponentData(owner, pose);
        }
        private void Tick()
        {
            var skin = world.EntityManager.GetBuffer<SkinMatrix>(visual);
            skin[0] = new SkinMatrix { Value = Pose(float3.zero) };
            skin[1] = new SkinMatrix { Value = Pose(new float3(0, 2, 0)) };
            skin[2] = new SkinMatrix { Value = Pose(new float3(0, -1, 0)) };
            world.SetTime(new TimeData(world.Time.ElapsedTime + .02, .02f));
            world.GetOrCreateSystem<StandingTurnAnimationSystem>().Update(world.Unmanaged);
            world.EntityManager.CompleteAllTrackedJobs();
        }
        private static float3x4 Pose(float3 p) => new float3x4(new float3(1, 0, 0), new float3(0, 1, 0), new float3(0, 0, 1), p);
    }
}
