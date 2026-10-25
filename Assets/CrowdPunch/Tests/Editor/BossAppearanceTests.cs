using System;
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
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class BossAppearanceTests
    {
        [TestCase(10)] [TestCase(20)] [TestCase(30)] [TestCase(40)]
        public void Combat008BossSamplesMatchSourcesAndClearTheFloor(int level)
        {
            var scene = EditorSceneManager.OpenPreviewScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{level:00}/Campaign_{level:00} Sub Scene.unity");
            try
            {
                var animations = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EnemyAnimationAuthoring>()).ToArray();
                Assert.That(animations.Length, Is.GreaterThan(0));
                foreach (var animation in animations)
                {
                    var renderer = animation.GetComponent<SkinnedMeshRenderer>();
                    var animator = animation.GetComponentInParent<Animator>();
                    using var reader = new BinaryReader(new MemoryStream(animation.Samples.bytes));
                    Assert.That(reader.ReadInt32(), Is.EqualTo(0x43504133));
                    string hash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)).ToString()
                        + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(renderer.sharedMesh))
                        + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(animator.avatar));
                    Assert.That(reader.ReadString(), Is.EqualTo(hash), renderer.name);
                    if (level == 20 || level == 30 || level == 40)
                    {
                        Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("CrowdPunch/Cel/Enemy"));
                        Assert.That(renderer.sharedMaterial.GetTexture("_ColorMap"), Is.Not.Null);
                        Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial), Does.StartWith((level == 20 ? ChickenModelBuilder.MaterialFolder : level == 30 ? RollingSpikeBuilder.MaterialFolder : DinoModelBuilder.MaterialFolder) + "/"));
                    }
                    else Assert.That(renderer.sharedMaterial.GetTexture("_ColorMap"), Is.EqualTo(level == 10 ? AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(GatekeeperMagmaBuilder.Art + "Gatekeeper Magma Cel.mat").GetTexture("_ColorMap") : AssetDatabase.LoadAssetAtPath<Texture2D>(EnemyAppearanceMaterials.AtlasPath)));
                }
                var model = animations[0].GetComponentInParent<Animator>().gameObject;
                var toOwner = model.transform.parent.worldToLocalMatrix * model.transform.localToWorldMatrix;
                float minimum = float.PositiveInfinity, radius = 0;
                BossAppearanceBuilder.VisitSamples(model, p => {
                    Vector3 ownerPoint = toOwner.MultiplyPoint3x4(p);
                    minimum = Mathf.Min(minimum, ownerPoint.y);
                    radius = Mathf.Max(radius, ownerPoint.magnitude);
                });
                float height = level == 10 ? model.GetComponentInParent<BossPartAuthoring>().height
                    : level == 20 ? model.GetComponentInParent<ChickenBossAuthoring>().settings.bodyHeight
                    : level == 30 ? model.GetComponentInParent<RollingBossAuthoring>().settings.bodyHeight
                    : model.GetComponentInParent<DinoBossAuthoring>().settings.bodyHeight;
                Assert.That(minimum, Is.GreaterThanOrEqualTo(-height * .5f + .075f));
                if (level == 30)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RollingSpikeBuilder.ModelPath);
                    Vector3 ratio = new Vector3(model.transform.localScale.x / prefab.transform.localScale.x,
                        model.transform.localScale.y / prefab.transform.localScale.y, model.transform.localScale.z / prefab.transform.localScale.z);
                    Assert.That(ratio.x, Is.GreaterThan(1), "Spike is enlarged to the existing hitbox.");
                    Assert.That(ratio.y, Is.EqualTo(ratio.x).Within(.0001f));
                    Assert.That(ratio.z, Is.EqualTo(ratio.x).Within(.0001f), "Uniform fitting preserves proportions.");
                    float footprint = 0;
                    BossAppearanceBuilder.VisitSamples(model, point =>
                    {
                        var position = toOwner.MultiplyPoint3x4(point);
                        footprint = Mathf.Max(footprint, new Vector2(position.x, position.z).magnitude);
                    }, 2);
                    Assert.That(footprint, Is.EqualTo(model.GetComponentInParent<RollingBossAuthoring>().settings.bodyRadius).Within(.005f),
                        "The idle/spin silhouette must reach the existing collision footprint.");
                    var controller = (UnityEditor.Animations.AnimatorController)model.GetComponent<Animator>().runtimeAnimatorController;
                    Assert.That(controller.layers[0].stateMachine.states[1].state.motion.name, Is.EqualTo("Hide and Spin Attack In Place"));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void Combat008BossAnimationTransitionsStayFiniteAndPreservePhysics(int kind)
        {
            using var world = new World("Boss appearance regression");
            var em = world.EntityManager;
            var owner = em.CreateEntity(typeof(LocalTransform), typeof(PhysicsVelocity));
            var original = LocalTransform.FromPosition(new float3(3, 4, 5));
            em.SetComponentData(owner, original);
            em.SetComponentData(owner, new PhysicsVelocity { Linear = new float3(2, 0, 1) });
            if (kind == 0) { em.AddComponentData(owner, new BossEncounter()); em.AddComponentData(owner, new BossTuning { TransitionDuration = 1 }); em.AddComponentData(owner, new BossImpactFeedback()); }
            if (kind == 1) { em.AddComponentData(owner, new ChickenBoss { Stage = 1 }); em.AddComponentData(owner, new ChickenTuning { WindUp = 1, Stagger = 1 }); }
            if (kind == 2) { em.AddComponentData(owner, new RollingBoss()); em.AddComponentData(owner, new RollingTuning { WindUp = 1, VisualRollDegreesPerSecond = 480 }); }
            if (kind == 3) { em.AddComponentData(owner, new DinoBoss { Stage = 1 }); em.AddComponentData(owner, new DinoTuning { ChaseSpeeds = new float3(4), WarningDuration = 1, StaggerDuration = 1 }); }
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var data = ref builder.ConstructRoot<EnemyAnimationSamples>();
            data.FrameCount = 2; data.BoneCount = 1;
            var durations = builder.Allocate(ref data.Durations, EnemyAnimationSamples.MotionCount);
            var matrices = builder.Allocate(ref data.Matrices, EnemyAnimationSamples.MotionCount * 2);
            for (int i = 0; i < durations.Length; i++) durations[i] = 1;
            for (int i = 0; i < matrices.Length; i++) matrices[i] = new float3x4(new float3(1, 0, 0), new float3(0, 1, 0), new float3(0, 0, 1), new float3(0, 1, 0));
            var samples = builder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
            try
            {
                var visual = em.CreateEntity(typeof(EnemyAnimationPlayback));
                em.AddComponentData(visual, new EnemyAnimation { Owner = owner, Samples = samples });
                em.AddComponentData(visual, new BossAnimationAnchor());
                if (kind == 2) em.AddComponentData(visual, new RollingAnimationState());
                em.AddBuffer<SkinMatrix>(visual).Add(default);
                for (int tick = 0; tick < 300; tick++)
                {
                    int phase = tick / 60;
                    if (kind == 0) em.SetComponentData(owner, new BossEncounter { Cycle = (BossCycle)phase });
                    if (kind == 1) em.SetComponentData(owner, new ChickenBoss { Phase = (ChickenPhase)phase, Stage = 3, Remaining = .5f, SinceShot = .1f });
                    if (kind == 2) em.SetComponentData(owner, new RollingBoss { Phase = (RollingPhase)System.Math.Min(phase, 3), Remaining = .5f });
                    if (kind == 3) em.SetComponentData(owner, new DinoBoss { Phase = (DinoPhase)phase, Stage = 3, Remaining = .5f });
                    world.SetTime(new TimeData(tick / 60.0, 1f / 60));
                    if (kind == 0) world.GetOrCreateSystem<GatekeeperAnimationSystem>().Update(world.Unmanaged);
                    if (kind == 1) world.GetOrCreateSystem<ChickenAnimationSystem>().Update(world.Unmanaged);
                    if (kind == 2) world.GetOrCreateSystem<RollingAnimationSystem>().Update(world.Unmanaged);
                    if (kind == 3) world.GetOrCreateSystem<DinoAnimationSystem>().Update(world.Unmanaged);
                    em.CompleteAllTrackedJobs();
                    var pose = em.GetBuffer<SkinMatrix>(visual)[0].Value;
                    Assert.That(math.all(math.isfinite(pose.c3)), Is.True);
                    Assert.That(math.length(pose.c0), Is.InRange(.7f, 1.3f));
                    Assert.That(math.length(pose.c1), Is.InRange(.7f, 1.3f));
                }
                Assert.That(em.GetComponentData<LocalTransform>(owner), Is.EqualTo(original));
                Assert.That(em.GetComponentData<PhysicsVelocity>(owner).Linear, Is.EqualTo(new float3(2, 0, 1)));
            }
            finally { samples.Dispose(); }
        }

        [Test]
        public void Roll007SpinInterpolationKeepsAuthoredScaleAndHitsPreserveCommittedCycle()
        {
            using var world = new World("Spike animation regression");
            var em = world.EntityManager;
            var owner = em.CreateEntity(typeof(RollingBoss), typeof(RollingTuning));
            em.SetComponentData(owner, new RollingBoss { Phase = RollingPhase.Roll, Remaining = 2 });
            em.SetComponentData(owner, new RollingTuning { WindUp = 1, Invulnerability = .75f });
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var data = ref builder.ConstructRoot<EnemyAnimationSamples>();
            data.FrameCount = 2; data.BoneCount = 1;
            var durations = builder.Allocate(ref data.Durations, EnemyAnimationSamples.MotionCount);
            var matrices = builder.Allocate(ref data.Matrices, EnemyAnimationSamples.MotionCount * 2);
            for (int i = 0; i < durations.Length; i++) durations[i] = 1;
            for (int i = 0; i < matrices.Length; i++)
            {
                var r = new float3x3(quaternion.RotateY(i % 2 == 0 ? 0 : math.PI * .5f));
                matrices[i] = new float3x4(r.c0 * 2, r.c1 * 2, r.c2 * 2, new float3(i / 2, 1, 0));
            }
            var samples = builder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
            try
            {
                var visual = em.CreateEntity(typeof(EnemyAnimationPlayback), typeof(RollingAnimationState));
                em.AddComponentData(visual, new EnemyAnimation { Owner = owner, Samples = samples });
                em.AddBuffer<SkinMatrix>(visual).Add(default);
                var system = world.GetOrCreateSystem<RollingAnimationSystem>();
                world.SetTime(new TimeData(.25, .25f));
                system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                var pose = em.GetBuffer<SkinMatrix>(visual)[0].Value;
                Assert.That(math.length(pose.c0), Is.EqualTo(2).Within(.0001f));
                Assert.That(math.length(pose.c1), Is.EqualTo(2).Within(.0001f));
                Assert.That(pose.c3.x, Is.EqualTo(1), "Roll plays the authored spin slot.");
                var boss = new RollingBoss { Phase = RollingPhase.WindUp, Remaining = .5f, AcceptedHits = 1 };
                em.SetComponentData(owner, boss);
                system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                Assert.That(em.GetBuffer<SkinMatrix>(visual)[0].Value.c3.x, Is.EqualTo(3), "Accepted hits play Take Damage.");
                Assert.That(em.GetComponentData<RollingBoss>(owner), Is.EqualTo(boss), "Presentation preserves action and timing.");
                boss.Phase = RollingPhase.Defeated; em.SetComponentData(owner, boss);
                for (int tick = 0; tick < 8; tick++) { system.Update(world.Unmanaged); em.CompleteAllTrackedJobs(); }
                Assert.That(em.GetComponentData<EnemyAnimationPlayback>(visual).Phase, Is.EqualTo(1), "Death holds its final pose.");
            }
            finally { samples.Dispose(); }
        }
        [Test]
        public void Boss002HandPoseDoesNotAccumulateOrMoveCollider()
        {
            using var world = new World("Hand pose regression");
            var em = world.EntityManager;
            var owner = em.CreateEntity(typeof(BossHand), typeof(LocalToWorld), typeof(LocalTransform));
            em.SetComponentData(owner, new LocalToWorld { Value = float4x4.Translate(new float3(3, 2, 4)) });
            em.SetComponentData(owner, LocalTransform.FromPosition(new float3(3, 2, 4)));
            em.SetComponentData(owner, new BossHand { Phase = BossHandPhase.Anticipation, Remaining = .2f, Duration = 1 });
            var visual = em.CreateEntity(typeof(BossHandVisualPose), typeof(LocalToWorld));
            em.AddComponentData(visual, new BossVisualOwner { Value = owner });
            em.SetComponentData(visual, new LocalToWorld { Value = float4x4.Translate(new float3(3, 2, 4)) });
            var system = world.GetOrCreateSystem<BossHandPoseSystem>();
            system.Update(world.Unmanaged);
            var first = em.GetComponentData<LocalToWorld>(visual);
            for (int i = 0; i < 300; i++) system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<LocalToWorld>(visual), Is.EqualTo(first));
            em.SetComponentData(owner, new BossHand { Phase = BossHandPhase.Ready });
            system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<LocalToWorld>(visual).Value, Is.EqualTo(float4x4.Translate(new float3(3, 2, 4))));
            Assert.That(em.GetComponentData<LocalTransform>(owner).Position, Is.EqualTo(new float3(3, 2, 4)));
        }
    }
}
