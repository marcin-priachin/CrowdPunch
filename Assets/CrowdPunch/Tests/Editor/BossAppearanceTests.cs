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
                    Assert.That(renderer.sharedMaterial.GetTexture("_ColorMap"), Is.EqualTo(AssetDatabase.LoadAssetAtPath<Texture2D>(EnemyAppearanceMaterials.AtlasPath)));
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
                    Assert.That(radius, Is.LessThanOrEqualTo(height * .5f - .095f), "Every rolling angle must clear the floor.");
                    var rolling = model.GetComponentInParent<RollingBossAuthoring>();
                    Assert.That(rolling.animationFloorOffsets.Length, Is.EqualTo(64));
                    var lowest = Enumerable.Repeat(float.PositiveInfinity, 128).ToArray();
                    var rotations = Enumerable.Range(0, 128).Select(i => Quaternion.Euler(i * 360f / 128, 0, 0)).ToArray();
                    BossAppearanceBuilder.VisitSamples(model, point => {
                        for (int i=0; i<128; i++)
                        {
                            float coordinate=i*.5f; int a=(int)coordinate;
                            float offset=Mathf.Lerp(rolling.animationFloorOffsets[a],rolling.animationFloorOffsets[(a+1)%64],coordinate-a);
                            var rotated=rolling.animationPivot+rotations[i]*(point-rolling.animationPivot)+Vector3.up*offset;
                            lowest[i]=Mathf.Min(lowest[i],toOwner.MultiplyPoint3x4(rotated).y);
                        }
                    });
                    Assert.That(lowest.Min(), Is.GreaterThanOrEqualTo(-height*.5f+.04f));
                    Assert.That(lowest.Max(), Is.LessThan(-height*.5f+.12f), "The silhouette should rest on the floor, not hover.");
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
                if (kind == 2) em.AddComponentData(visual, new RollingAnimationPivot { Center = new float3(0, 1, 0) });
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

        [TestCase(-2.06213883e-7f)]
        [TestCase(-2.1329668e-20f)]
        [TestCase(0f)]
        [TestCase(.15f)]
        public void Roll007UprightRecoveryKeepsBodyAndEyeSynchronized(float angle)
        {
            using var world = new World("Rolling floor seam regression");
            var em = world.EntityManager;
            var owner = em.CreateEntity(typeof(RollingBoss), typeof(RollingTuning));
            em.SetComponentData(owner, new RollingBoss { Phase = RollingPhase.Pause });
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var data = ref builder.ConstructRoot<EnemyAnimationSamples>();
            data.FrameCount = 2; data.BoneCount = 1;
            var durations = builder.Allocate(ref data.Durations, EnemyAnimationSamples.MotionCount);
            var matrices = builder.Allocate(ref data.Matrices, EnemyAnimationSamples.MotionCount * 2);
            for (int i = 0; i < durations.Length; i++) durations[i] = 1;
            for (int i = 0; i < matrices.Length; i++)
                matrices[i] = new float3x4(new float3(1, 0, 0), new float3(0, 1, 0), new float3(0, 0, 1), new float3(0, 1, 0));
            var samples = builder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
            using var floorBuilder = new BlobBuilder(Allocator.Temp);
            ref var floor = ref floorBuilder.ConstructRoot<RollingFloorSamples>();
            var offsets = floorBuilder.Allocate(ref floor.Offsets, 64);
            for (int i = 0; i < offsets.Length; i++) offsets[i] = .5f + .2f * math.sin(i * 2 * math.PI / offsets.Length);
            var floorSamples = floorBuilder.CreateBlobAssetReference<RollingFloorSamples>(Allocator.Persistent);
            try
            {
                var renderers = new Entity[2];
                for (int i = 0; i < renderers.Length; i++)
                {
                    var visual = em.CreateEntity(typeof(EnemyAnimationPlayback));
                    em.AddComponentData(visual, new EnemyAnimation { Owner = owner, Samples = samples });
                    em.AddComponentData(visual, new RollingAnimationPivot {
                        Center = new float3(0, 1, 0), Angle = angle, Floor = floorSamples
                    });
                    em.AddBuffer<SkinMatrix>(visual).Add(default);
                    renderers[i] = visual;
                }
                var system = world.GetOrCreateSystem<RollingAnimationSystem>();
                for (int tick = 0; tick < 180; tick++)
                {
                    world.SetTime(new TimeData(tick / 60.0, 1f / 60));
                    system.Update(world.Unmanaged);
                    em.CompleteAllTrackedJobs();
                    var body = em.GetBuffer<SkinMatrix>(renderers[0])[0].Value;
                    var eye = em.GetBuffer<SkinMatrix>(renderers[1])[0].Value;
                    Assert.That(math.all(math.isfinite(body.c3)), Is.True);
                    Assert.That(eye, Is.EqualTo(body), "Every renderer must finish its skin and floor update.");
                    Assert.That(em.GetComponentData<EnemyAnimationPlayback>(renderers[0]).Phase,
                        Is.EqualTo(em.GetComponentData<EnemyAnimationPlayback>(renderers[1]).Phase));
                }
                Assert.That(em.GetBuffer<SkinMatrix>(renderers[0])[0].Value.c3.y, Is.EqualTo(1.5f).Within(.0001f),
                    "Recovery must wrap to the first floor sample at zero, not clamp to the last sample.");
            }
            finally { samples.Dispose(); floorSamples.Dispose(); }
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
