using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Editor;
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
    public sealed class GatekeeperMagmaTests
    {
        [Test]
        public void Boss008AcceptedHitPlaysCompleteDamageClipWithoutMaterialFlashTimer()
        {
            using var world = new World("Magma hit reaction");
            var em = world.EntityManager;
            var boss = em.CreateEntity(typeof(BossEncounter), typeof(BossTuning));
            em.SetComponentData(boss, new BossEncounter { AcceptedHits = 1 });
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var data = ref builder.ConstructRoot<EnemyAnimationSamples>();
            data.FrameCount = 2; data.BoneCount = 1;
            var durations = builder.Allocate(ref data.Durations, EnemyAnimationSamples.MotionCount);
            var matrices = builder.Allocate(ref data.Matrices, EnemyAnimationSamples.MotionCount * 2);
            for (int i = 0; i < durations.Length; i++) durations[i] = 1;
            for (int i = 0; i < matrices.Length; i++) matrices[i] = new float3x4(new float3(1, 0, 0), new float3(0, 1, 0), new float3(0, 0, 1), float3.zero);
            var samples = builder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
            try
            {
                var visual = em.CreateEntity(typeof(EnemyAnimationPlayback), typeof(BossAnimationAnchor));
                em.AddComponentData(visual, new EnemyAnimation { Owner = boss, Samples = samples });
                em.AddBuffer<SkinMatrix>(visual).Add(default);
                var system = world.GetOrCreateSystem<GatekeeperAnimationSystem>();
                for (int i = 0; i < 8; i++)
                {
                    world.SetTime(new TimeData(i * .1, .1f));
                    system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                    Assert.That(em.GetComponentData<EnemyAnimationPlayback>(visual).Initialized, Is.EqualTo(4));
                }
                em.SetComponentData(boss, new BossEncounter { AcceptedHits = 2 });
                system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                Assert.That(em.GetComponentData<EnemyAnimationPlayback>(visual).Phase, Is.EqualTo(.1f).Within(.001f));
                for (int i = 0; i < 12; i++) { system.Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
                Assert.That(em.GetComponentData<EnemyAnimationPlayback>(visual).Initialized, Is.EqualTo(1));
            }
            finally { samples.Dispose(); }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void Boss008EveryEncounterUsesMagmaRigWithoutPrimitiveHands(int index)
        {
            var scene = EditorSceneManager.OpenPreviewScene(GatekeeperMagmaBuilder.Scenes[index]);
            try
            {
                var boss = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossEncounterAuthoring>()).Single();
                var animation = boss.GetComponentInChildren<EnemyAnimationAuthoring>();
                var renderer = animation.GetComponent<SkinnedMeshRenderer>();
                Assert.That(animation.GetComponent<GatekeeperRigAuthoring>(), Is.Not.Null);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromOriginalSource(animation.GetComponentInParent<Animator>().gameObject),
                    Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(GatekeeperMagmaBuilder.Source)));
                Assert.That(renderer.bones.Any(b => b.name == "RigLArm1"), Is.True);
                Assert.That(renderer.bones.Any(b => b.name == "RigRArm1"), Is.True);
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("CrowdPunch/Cel/Enemy"));
                var source = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Polygonal Magma/Materials/Polygonal Magma Orange.mat");
                Assert.That(renderer.sharedMaterial.GetTexture("_ColorMap"), Is.EqualTo(source.GetTexture("_BaseMap")));
                Assert.That(boss.leftHand.GetComponentsInChildren<Renderer>(), Is.Empty);
                Assert.That(boss.rightHand.GetComponentsInChildren<Renderer>(), Is.Empty);
                Assert.That(boss.leftHand.radius, Is.EqualTo(2.6f));
                Assert.That(boss.rightHand.radius, Is.EqualTo(2.6f));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void Boss002BothRigArmsFollowPhysicalHandsWithoutAccumulationOrPhysicsWrites()
        {
            using var world = new World("Magma arm attachment");
            var em = world.EntityManager;
            var left = em.CreateEntity(typeof(BossHand), typeof(LocalToWorld), typeof(PhysicsVelocity));
            var right = em.CreateEntity(typeof(BossHand), typeof(LocalToWorld), typeof(PhysicsVelocity));
            em.AddComponentData(left, new BossPart { Radius = 4.2f });
            em.AddComponentData(right, new BossPart { Radius = 5.1f });
            var boss = em.CreateEntity(typeof(BossEncounter));
            em.SetComponentData(boss, new BossEncounter { LeftHand = left, RightHand = right });
            var model = em.CreateEntity(typeof(LocalToWorld));
            var modelWorld = float4x4.TRS(new float3(8, 2, -3), quaternion.RotateY(.7f), new float3(3));
            em.SetComponentData(model, new LocalToWorld { Value = modelWorld });
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var data = ref builder.ConstructRoot<EnemyAnimationSamples>();
            data.FrameCount = 2; data.BoneCount = 5;
            var durations = builder.Allocate(ref data.Durations, EnemyAnimationSamples.MotionCount);
            var matrices = builder.Allocate(ref data.Matrices, EnemyAnimationSamples.MotionCount * 2 * 5);
            for (int i = 0; i < durations.Length; i++) durations[i] = 1;
            for (int i = 0; i < matrices.Length; i++)
            {
                var rotation = new float3x3(quaternion.RotateZ(i / 10 * .07f));
                matrices[i] = new float3x4(rotation.c0, rotation.c1, rotation.c2, new float3(i % 5, .5f, 0));
            }
            var samples = builder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
            using var boundsBuilder = new BlobBuilder(Allocator.Temp);
            ref var handData = ref boundsBuilder.ConstructRoot<GatekeeperHandBounds>();
            handData.FrameCount = 2; handData.LeftRadius = handData.RightRadius = 1;
            var handFrames = boundsBuilder.Allocate(ref handData.Frames, EnemyAnimationSamples.MotionCount * 4);
            for (int i = 0; i < handFrames.Length; i++)
                handFrames[i] = new GatekeeperHandFrame { Min = new float3(-.5f), Max = new float3(.5f) };
            var bounds = boundsBuilder.CreateBlobAssetReference<GatekeeperHandBounds>(Allocator.Persistent);
            try
            {
                var visual = em.CreateEntity(typeof(EnemyAnimationPlayback));
                em.AddComponentData(visual, new EnemyAnimation { Owner = boss, Samples = samples });
                em.AddComponentData(visual, new GatekeeperRig { Model = model, LeftPalm = 1, RightPalm = 2,
                    LeftBindPalm = new float3(.1f, .2f, 0), RightBindPalm = new float3(-.1f, .2f, 0), Bounds = bounds, GroundClearance = -100 });
                var mask = em.AddBuffer<GatekeeperArmBone>(visual);
                foreach (byte side in new byte[] { 0, 1, 2, 1, 2 }) mask.Add(new GatekeeperArmBone { Side = side });
                var skin = em.AddBuffer<SkinMatrix>(visual);
                for (int i = 0; i < 5; i++) skin.Add(new SkinMatrix { Value = matrices[i] });
                var head = skin[0].Value;
                var system = world.GetOrCreateSystem<GatekeeperArmAnimationSystem>();
                for (int tick = 0; tick < 420; tick++)
                {
                    var lp = new float3(3 + tick * .01f, 1, 5);
                    var rp = new float3(-3, 4, -tick * .02f);
                    em.SetComponentData(left, new LocalToWorld { Value = float4x4.Translate(lp) });
                    em.SetComponentData(right, new LocalToWorld { Value = float4x4.Translate(rp) });
                    em.SetComponentData(left, new BossHand { Phase = (BossHandPhase)(tick / 60), Attack = (BossAttack)(tick % 3), Duration = 1, Remaining = 1 - tick % 60 / 60f });
                    em.SetComponentData(right, new BossHand { Phase = (BossHandPhase)(6 - tick / 60), Attack = (BossAttack)((tick + 1) % 3), Duration = 1, Remaining = 1 - tick % 60 / 60f });
                    system.Update(world.Unmanaged);
                    em.CompleteAllTrackedJobs();
                    skin = em.GetBuffer<SkinMatrix>(visual);
                    var l = skin[1].Value; var r = skin[2].Value;
                    var lPalm = math.transform(modelWorld, math.mul(new float3x3(l.c0, l.c1, l.c2), new float3(.1f, .2f, 0)) + l.c3);
                    var rPalm = math.transform(modelWorld, math.mul(new float3x3(r.c0, r.c1, r.c2), new float3(-.1f, .2f, 0)) + r.c3);
                    Assert.That(math.distance(lPalm, lp), Is.LessThan(.0001f));
                    Assert.That(math.distance(rPalm, rp), Is.LessThan(.0001f));
                    Assert.That(math.length(l.c0), Is.EqualTo(1.4f).Within(.0001f));
                    Assert.That(math.length(r.c0), Is.EqualTo(1.7f).Within(.0001f));
                    Assert.That(skin[0].Value, Is.EqualTo(head));
                    Assert.That(em.GetComponentData<LocalToWorld>(left).Position, Is.EqualTo(lp));
                    Assert.That(em.GetComponentData<PhysicsVelocity>(left).Linear, Is.EqualTo(float3.zero));
                }
            }
            finally { samples.Dispose(); bounds.Dispose(); }
        }
    }
}
