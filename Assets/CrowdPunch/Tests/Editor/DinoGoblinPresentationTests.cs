using System.IO;
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
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class DinoGoblinPresentationTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void Pillar006_SavedModelPreservesScalesEquipmentPaletteAndSources(int encounter)
        {
            var scene = EditorSceneManager.OpenPreviewScene(DinoModelBuilder.ScenePaths[encounter]);
            try
            {
                var boss = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DinoBossAuthoring>()).Single();
                var visual = boss.transform.Find("Dino Model");
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(DinoModelBuilder.ModelPath);
                foreach (var original in source.GetComponentsInChildren<Transform>(true))
                {
                    string path = AnimationUtility.CalculateTransformPath(original, source.transform);
                    var actual = path.Length == 0 ? visual : visual.Find(path);
                    Assert.IsNotNull(actual, path);
                    Assert.Less(Vector3.Distance(original.localScale, actual.localScale), .0001f, path);
                }
                var animations = visual.GetComponentsInChildren<EnemyAnimationAuthoring>(true);
                Assert.AreEqual(source.GetComponentsInChildren<Renderer>(true).Length, animations.Length);
                Assert.AreEqual(1, animations.Count(a => a.JogSamples != null));
                Assert.IsEmpty(visual.GetComponentsInChildren<LODGroup>(true));
                foreach (var animation in animations)
                {
                    var renderer = animation.GetComponent<SkinnedMeshRenderer>();
                    var animator = animation.GetComponentInParent<Animator>();
                    using var reader = new BinaryReader(new MemoryStream(animation.Samples.bytes));
                    Assert.AreEqual(0x43504133, reader.ReadInt32());
                    string hash = AssetDatabase.GetAssetDependencyHash(DinoModelBuilder.ControllerPath).ToString()
                        + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(renderer.sharedMesh))
                        + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(animator.avatar));
                    Assert.AreEqual(hash, reader.ReadString());
                    Assert.AreSame(animator.transform, renderer.rootBone);
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.AreEqual("CrowdPunch/Cel/Enemy", material.shader.name);
                        Assert.IsNotNull(material.GetTexture("_ColorMap"));
                    }
                }
                var controller = (UnityEditor.Animations.AnimatorController)visual.GetComponent<Animator>().runtimeAnimatorController;
                CollectionAssert.AreEqual(new[] { "Block", "Block", "Cast Spell", "Take Damage", "Die" },
                    controller.layers[0].stateMachine.states.Select(s => s.state.motion.name).ToArray());
                float lowest = float.PositiveInfinity;
                BossAppearanceBuilder.VisitSamples(visual.gameObject, p => lowest = Mathf.Min(lowest, visual.TransformPoint(p).y), 11);
                Assert.GreaterOrEqual(lowest, boss.transform.position.y - boss.settings.bodyHeight * .5f + .075f);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(DinoPhase.Chase, 0f)] [TestCase(DinoPhase.Warning, 0f)] [TestCase(DinoPhase.Burst, 0f)]
        [TestCase(DinoPhase.Stagger, 0f)] [TestCase(DinoPhase.Defeated, 0f)]
        [TestCase(DinoPhase.Chase, -.000001f)] [TestCase(DinoPhase.Chase, .000001f)]
        public void Pillar006_PhasesMaskOnlyLegsPreserveScalePaletteAndPhysics(DinoPhase phase, float sideways)
        {
            using var world = new World("Dino masked jog");
            var em = world.EntityManager;
            var owner = em.CreateEntity(typeof(DinoBoss), typeof(DinoTuning), typeof(LocalTransform), typeof(PhysicsVelocity));
            em.SetComponentData(owner, new DinoBoss { Phase = phase, Stage = 1, Remaining = .5f });
            em.SetComponentData(owner, new DinoTuning { WarningDuration = 1, StaggerDuration = 1 });
            var transform = LocalTransform.FromPositionRotationScale(new float3(3, 4, 5), quaternion.identity, 1.5f);
            em.SetComponentData(owner, transform);
            var velocity = new float3(sideways, 0, 10);
            em.SetComponentData(owner, new PhysicsVelocity { Linear = velocity });
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var data = ref builder.ConstructRoot<EnemyAnimationSamples>();
            data.BoneCount = 3; data.FrameCount = 2;
            var durations = builder.Allocate(ref data.Durations, 11);
            var poses = builder.Allocate(ref data.Matrices, 66);
            var identity = new float3x4(new float3(1, 0, 0), new float3(0, 1, 0), new float3(0, 0, 1), float3.zero);
            for (int motion = 0; motion < 11; motion++)
            {
                durations[motion] = 1;
                for (int frame = 0; frame < 2; frame++)
                {
                    poses[motion * 6 + frame * 3] = identity;
                    poses[motion * 6 + frame * 3 + 1] = identity;
                    poses[motion * 6 + frame * 3 + 2] = new float3x4(identity.c0, identity.c1, identity.c2, new float3(motion, 8, 9));
                }
            }
            using var samples = builder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
            using var jogBuilder = new BlobBuilder(Allocator.Temp);
            ref var jog = ref jogBuilder.ConstructRoot<EnemyJogSamples>();
            jog.BoneCount = 3; jog.FrameCount = 2; jog.HipBoneIndex = 0; jog.HipBindInverse = float4x4.identity;
            jogBuilder.Allocate(ref jog.LegBones, 1)[0] = 1;
            var jogDurations = jogBuilder.Allocate(ref jog.Durations, 8);
            var distances = jogBuilder.Allocate(ref jog.CycleDistances, 8);
            var legs = jogBuilder.Allocate(ref jog.Matrices, 16);
            for (int i = 0; i < 8; i++) { jogDurations[i] = 1; distances[i] = 2; }
            for (int i = 0; i < 16; i++) legs[i] = new float3x4(identity.c0, identity.c1, identity.c2, new float3(1, 2, 3));
            using var jogSamples = jogBuilder.CreateBlobAssetReference<EnemyJogSamples>(Allocator.Persistent);
            var visual = em.CreateEntity(typeof(EnemyAnimationPlayback), typeof(BossAnimationAnchor));
            em.AddComponentData(visual, new EnemyAnimation { Owner = owner, Samples = samples, JogSamples = jogSamples, MaxLegAnimationSpeed = 2 });
            em.AddComponentData(visual, new DinoVisualOwner { Value = owner });
            var palette = new float4(.3f, .6f, .9f, 1);
            em.AddComponentData(visual, new URPMaterialPropertyBaseColor { Value = palette });
            var skin = em.AddBuffer<SkinMatrix>(visual);
            for (int i = 0; i < 3; i++) skin.Add(new SkinMatrix { Value = identity });
            world.SetTime(new TimeData(1, .02f));
            world.GetOrCreateSystem<DinoAnimationSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<DinoPresentationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            skin = em.GetBuffer<SkinMatrix>(visual);
            int expected = phase == DinoPhase.Burst ? 1 : phase == DinoPhase.Warning ? 2 : phase == DinoPhase.Stagger ? 3 : phase == DinoPhase.Defeated ? 4 : 0;
            Assert.AreEqual(new float3(expected, 8, 9), skin[2].Value.c3);
            Assert.AreEqual(expected < 3 ? new float3(1, 2, 3) : float3.zero, skin[1].Value.c3);
            Assert.AreEqual(identity.c0, skin[2].Value.c0);
            Assert.AreEqual(identity.c1, skin[2].Value.c1);
            Assert.AreEqual(identity.c2, skin[2].Value.c2);
            Assert.AreEqual(palette, em.GetComponentData<URPMaterialPropertyBaseColor>(visual).Value);
            Assert.AreEqual(transform, em.GetComponentData<LocalTransform>(owner));
            Assert.AreEqual(velocity, em.GetComponentData<PhysicsVelocity>(owner).Linear);
        }
    }
}
