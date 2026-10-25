using System.IO;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class WizardBudPresentationTests
    {
        [Test]
        public void Wizard001And006KeepBudScaleTextureClipsAndGroundSpawn()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(CrowdPunch.Editor.EnemyWizardPrefabBuilder.ModelPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CrowdPunch/Prefabs/EnemyWizard.prefab");
            var animator = prefab.GetComponentInChildren<Animator>();
            Assert.IsFalse(animator.applyRootMotion);
            Assert.That(animator.transform.localScale, Is.EqualTo(Vector3.one * 3f), "Keep the current Inspector scale.");
            foreach (var original in source.GetComponentsInChildren<Transform>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(original, source.transform);
                if (string.IsNullOrEmpty(path)) continue; // Root scale is the user's explicit 3x override.
                var target = string.IsNullOrEmpty(path) ? animator.transform : animator.transform.Find(path);
                Assert.NotNull(target, path);
                Assert.That(target.localScale, Is.EqualTo(original.localScale), path);
            }
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            foreach (var state in controller.layers[0].stateMachine.states)
            {
                Assert.That(AssetDatabase.GetAssetPath(state.state.motion), Does.StartWith("Assets/Bud Cute Series/FBX/"));
                if (state.state.name == "Motion2")
                {
                    Assert.That(state.state.motion.name, Is.EqualTo("Fly Projectile Upward"));
                    Assert.IsTrue(((AnimationClip)state.state.motion).isLooping);
                }
                if (state.state.name == "Motion7") Assert.That(state.state.motion.name, Is.EqualTo("Spawn to Ground Idle"));
            }
            var settings = AssetDatabase.LoadAssetAtPath<CrowdPunch.Configuration.EnemySpawnSettings>(
                "Assets/CrowdPunch/Data/Settings/Enemies/WizardEnemySpawnSettings.asset");
            Assert.IsTrue(new SerializedObject(settings).FindProperty("spawnFromGround").boolValue);
            Assert.AreSame(prefab, settings.EnemyPrefab);
            Assert.That(settings.WizardSettings.AnimationGroundClearance, Is.EqualTo(.1f));
            foreach (var part in prefab.GetComponentsInChildren<EnemyAnimationAuthoring>())
            {
                Assert.That(part.Profile, Is.EqualTo(EnemyAnimationProfile.Wizard));
                Assert.That(part.SpawnMotion, Is.EqualTo(7));
                Assert.NotNull(part.Samples);
                var material = part.GetComponent<SkinnedMeshRenderer>().sharedMaterial;
                Assert.That(material.shader.name, Is.EqualTo("CrowdPunch/Cel/Enemy"));
                var texture = material.GetTexture("_ColorMap");
                Assert.That(AssetDatabase.GetAssetPath(texture), Is.EqualTo("Assets/Bud Cute Series/Textures/Bud.psd"));
                CrowdPunch.Editor.EnemyAppearanceMaterials.Configure(material);
                Assert.AreSame(texture, material.GetTexture("_ColorMap"));
            }
        }

        [Test]
        public void Wizard006FlySamplesKeepOriginalMotionWithOneConstantHeightOffset()
        {
            var prefab = PrefabUtility.LoadPrefabContents("Assets/CrowdPunch/Prefabs/EnemyWizard.prefab");
            try
            {
                var animator = prefab.GetComponentInChildren<Animator>();
                var renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
                AnimationClip clip = null;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Bud Cute Series/FBX/Bud@Fly Forward In Place.FBX"))
                    if (asset is AnimationClip candidate && candidate.name == "Fly Forward In Place") clip = candidate;
                Assert.NotNull(clip);
                using var data = ReadSamples(renderer);
                ref var samples = ref data.Value;
                var vertices = renderer.sharedMesh.vertices;
                var weights = renderer.sharedMesh.boneWeights;
                var bindposes = renderer.sharedMesh.bindposes;
                var raw = new Matrix4x4[samples.BoneCount];
                float offset = 0, lowest = float.PositiveInfinity, highestMinimum = float.NegativeInfinity;
                animator.Rebind();
                for (int frame = 0; frame < samples.FrameCount; frame++)
                {
                    clip.SampleAnimation(animator.gameObject, frame / (float)samples.FrameCount * clip.length);
                    Matrix4x4 inverse = animator.transform.worldToLocalMatrix;
                    for (int bone = 0; bone < samples.BoneCount; bone++)
                    {
                        raw[bone] = inverse * renderer.bones[bone].localToWorldMatrix * bindposes[bone];
                        var exported = samples.Matrices[(samples.FrameCount + frame) * samples.BoneCount + bone];
                        if (frame == 0 && bone == 0) offset = exported.c3.y - raw[bone].m13;
                        Assert.That(math.distance(exported.c0, (float3)(Vector3)raw[bone].GetColumn(0)), Is.LessThan(.0001f));
                        Assert.That(math.distance(exported.c1, (float3)(Vector3)raw[bone].GetColumn(1)), Is.LessThan(.0001f));
                        Assert.That(math.distance(exported.c2, (float3)(Vector3)raw[bone].GetColumn(2)), Is.LessThan(.0001f));
                        Assert.That(exported.c3.x, Is.EqualTo(raw[bone].m03).Within(.0001f));
                        Assert.That(exported.c3.z, Is.EqualTo(raw[bone].m23).Within(.0001f));
                        Assert.That(exported.c3.y - raw[bone].m13, Is.EqualTo(offset).Within(.0001f), "Only one constant vertical translation may change the source clip.");
                    }
                    float minimum = float.PositiveInfinity;
                    for (int vertex = 0; vertex < vertices.Length; vertex++)
                    {
                        var w = weights[vertex]; var v = vertices[vertex];
                        Vector3 point = raw[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0
                            + raw[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                            + raw[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2
                            + raw[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                        minimum = Mathf.Min(minimum, point.y + offset);
                    }
                    lowest = Mathf.Min(lowest, minimum);
                    highestMinimum = Mathf.Max(highestMinimum, minimum);
                }
                Assert.That(lowest, Is.EqualTo(0).Within(.0001f));
                Assert.That(highestMinimum - lowest, Is.GreaterThan(.02f), "The original vertical fly motion must remain intact.");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        [TestCase(0f, 3f)]
        [TestCase(.1f, 3f)]
        [TestCase(.4f, 3f)]
        [TestCase(.1f, 1f)]
        public void Wizard006OriginalCyclesUseAdjustableWorldMetreClearance(float clearance, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CrowdPunch/Prefabs/EnemyWizard.prefab");
            var renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            using var samples = ReadSamples(renderer);
            using var world = new World("Wizard animation clearance");
            var em = world.EntityManager;
            var owner = em.CreateEntity(typeof(LocalTransform), typeof(EnemyLaunchState), typeof(DesiredMovement),
                typeof(EnemyMovementSettings), typeof(WizardCastState), typeof(WizardSettings));
            em.SetComponentData(owner, LocalTransform.Identity);
            em.SetComponentData(owner, new WizardSettings { AnimationGroundClearance = clearance });
            var visual = em.CreateEntity(typeof(EnemyAnimation), typeof(EnemyAnimationPlayback));
            em.SetComponentData(visual, new EnemyAnimation { Owner = owner, Samples = samples,
                Profile = (byte)EnemyAnimationProfile.Wizard, SpawnMotion = 7, WorldToAnimationHeight = 1f / scale });
            em.AddBuffer<SkinMatrix>(visual).ResizeUninitialized(samples.Value.BoneCount);
            var originalBounds = new AABB { Center = renderer.localBounds.center, Extents = renderer.localBounds.extents };
            em.AddComponentData(visual, new EnemyAnimationBounds { Animation = visual, Original = originalBounds });
            em.AddComponentData(visual, new Unity.Rendering.RenderBounds { Value = originalBounds });
            var vertices = renderer.sharedMesh.vertices;
            var weights = renderer.sharedMesh.boneWeights;
            var system = world.GetOrCreateSystem<EnemyAnimationSystem>();
            world.SetTime(new TimeData(0, 0));
            foreach (int motion in new[] { 0, 1, 2, EnemyAnimationSamples.FlyingMotion })
            {
                float cycleMinimum = float.PositiveInfinity;
                for (int sample = 0; sample < samples.Value.FrameCount; sample++)
                {
                    float phase = sample / (float)samples.Value.FrameCount;
                    em.SetComponentData(owner, new DesiredMovement { Speed = motion == 1 ? 3 : 0 });
                    em.SetComponentData(owner, new WizardCastState { Phase = motion == 2 ? WizardCastPhase.Active : WizardCastPhase.Cooldown });
                    em.SetComponentData(owner, new EnemyLaunchState { Phase = motion == 9 ? EnemyLaunchPhase.Launched : EnemyLaunchPhase.Active, LaunchSequence = 1 });
                    em.SetComponentData(visual, new EnemyAnimationPlayback { Initialized = 1, Phase = phase,
                        WasLaunched = 1, LaunchSequence = 1, FlightPhase = phase, WasAttacking = 1, AttackPhase = phase });
                    system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                    var skin = em.GetBuffer<SkinMatrix>(visual);
                    for (int vertex = 0; vertex < vertices.Length; vertex++)
                    {
                        var w = weights[vertex]; var p = new float4(vertices[vertex], 1);
                        float3 point = math.mul(skin[w.boneIndex0].Value, p) * w.weight0
                            + math.mul(skin[w.boneIndex1].Value, p) * w.weight1
                            + math.mul(skin[w.boneIndex2].Value, p) * w.weight2
                            + math.mul(skin[w.boneIndex3].Value, p) * w.weight3;
                        cycleMinimum = math.min(cycleMinimum, point.y * scale);
                    }
                }
                Assert.That(cycleMinimum, Is.EqualTo(clearance).Within(.001f), $"Motion {motion} must reach the configured clearance at its lowest point.");
            }
            var bounds = em.GetComponentData<Unity.Rendering.RenderBounds>(visual).Value;
            Assert.That(bounds.Max.y, Is.GreaterThanOrEqualTo(originalBounds.Max.y + clearance / scale - .0001f));
            Assert.That(bounds.Min.y, Is.EqualTo(originalBounds.Min.y).Within(.0001f), "Spawn bounds stay covered.");
            Assert.That(em.GetComponentData<LocalTransform>(owner), Is.EqualTo(LocalTransform.Identity));
        }

        [TestCase(.1f, 3f)]
        [TestCase(.4f, 3f)]
        [TestCase(.1f, 1f)]
        public void Wizard006SpawnStaysBelowFlightHeightAndHandsOffWithoutSnapping(float clearance, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CrowdPunch/Prefabs/EnemyWizard.prefab");
            var renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            using var samples = ReadSamples(renderer);
            using var world = new World("Wizard spawn handoff");
            var em = world.EntityManager;
            var owner = em.CreateEntity(typeof(LocalTransform), typeof(EnemyLaunchState), typeof(DesiredMovement),
                typeof(EnemyMovementSettings), typeof(WizardCastState), typeof(WizardSettings));
            em.SetComponentData(owner, LocalTransform.Identity);
            em.SetComponentData(owner, new WizardSettings { AnimationGroundClearance = clearance });
            var visual = em.CreateEntity(typeof(EnemyAnimation), typeof(EnemyAnimationPlayback));
            em.SetComponentData(visual, new EnemyAnimation { Owner = owner, Samples = samples,
                Profile = (byte)EnemyAnimationProfile.Wizard, SpawnMotion = 7, WorldToAnimationHeight = 1f / scale });
            em.SetComponentData(visual, new EnemyAnimationPlayback { Initialized = 1 });
            em.AddBuffer<SkinMatrix>(visual).ResizeUninitialized(samples.Value.BoneCount);
            var vertices = renderer.sharedMesh.vertices;
            var weights = renderer.sharedMesh.boneWeights;
            var system = world.GetOrCreateSystem<EnemyAnimationSystem>();
            world.SetTime(new TimeData(0, 0));
            system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            using var idle = em.GetBuffer<SkinMatrix>(visual).ToNativeArray(Allocator.Temp);
            float maximumHeight = MeshBounds(em.GetBuffer<SkinMatrix>(visual), vertices, weights).max.y * scale;
            float duration = samples.Value.Durations[7];
            em.AddComponentData(owner, new EnemySpawnAnimation { Duration = duration });
            for (int frame = 0; frame < samples.Value.FrameCount; frame++)
            {
                float phase = frame / (float)(samples.Value.FrameCount - 1);
                em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Recovering,
                    RecoverySecondsRemaining = duration * (1f - phase) });
                system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                var bounds = MeshBounds(em.GetBuffer<SkinMatrix>(visual), vertices, weights);
                Assert.That(bounds.max.y * scale, Is.LessThanOrEqualTo(maximumHeight + .001f), "Spawn must not rise above the destination fly pose.");
                if (frame == 0) Assert.That(bounds.min.y * scale, Is.LessThan(-.1f), "Preserve underground emergence.");
            }
            var endpoint = em.GetBuffer<SkinMatrix>(visual);
            for (int bone = 0; bone < endpoint.Length; bone++)
                Assert.That(math.csum(math.abs(endpoint[bone].Value.c3 - idle[bone].Value.c3)), Is.LessThan(.0001f), "Spawn ends at the first fly-idle pose with the configured clearance.");
            em.SetComponentEnabled<EnemySpawnAnimation>(owner, false);
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Active });
            em.SetComponentData(owner, new DesiredMovement { Speed = 3 });
            system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            var firstActive = em.GetBuffer<SkinMatrix>(visual);
            for (int bone = 0; bone < firstActive.Length; bone++)
            {
                Assert.That(math.distance(firstActive[bone].Value.c0, idle[bone].Value.c0), Is.LessThan(.0001f));
                Assert.That(math.distance(firstActive[bone].Value.c1, idle[bone].Value.c1), Is.LessThan(.0001f));
                Assert.That(math.distance(firstActive[bone].Value.c2, idle[bone].Value.c2), Is.LessThan(.0001f));
                Assert.That(math.distance(firstActive[bone].Value.c3, idle[bone].Value.c3), Is.LessThan(.0001f), "No height snap at the spawn/active boundary.");
            }
            for (int frame = 0; frame < 10; frame++)
            {
                world.SetTime(new TimeData((frame + 1) * .02, .02f));
                system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            }
            Assert.That(em.GetComponentData<EnemyAnimationPlayback>(visual).WasRecovering, Is.Zero, "The handoff completes without an extra takeoff clip.");
            Assert.That(em.GetComponentData<LocalTransform>(owner), Is.EqualTo(LocalTransform.Identity));
        }

        [TestCase(.1f, 3f)]
        [TestCase(.4f, 3f)]
        [TestCase(.1f, 1f)]
        public void Wizard006DeathStaysAtMovementClearanceThroughout(float clearance, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CrowdPunch/Prefabs/EnemyWizard.prefab");
            var renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            using var samples = ReadSamples(renderer);
            using var world = new World("Wizard death altitude");
            var em = world.EntityManager;
            var owner = em.CreateEntity(typeof(LocalTransform), typeof(EnemyLaunchState), typeof(DesiredMovement),
                typeof(EnemyMovementSettings), typeof(WizardSettings));
            em.SetComponentData(owner, LocalTransform.Identity);
            em.SetComponentData(owner, new WizardSettings { AnimationGroundClearance = clearance });
            em.SetComponentData(owner, new EnemyLaunchState { Phase = EnemyLaunchPhase.Defeated });
            var visual = em.CreateEntity(typeof(EnemyAnimation), typeof(EnemyAnimationPlayback));
            em.SetComponentData(visual, new EnemyAnimation { Owner = owner, Samples = samples,
                Profile = (byte)EnemyAnimationProfile.Wizard, SpawnMotion = 7, WorldToAnimationHeight = 1f / scale });
            em.AddBuffer<SkinMatrix>(visual).ResizeUninitialized(samples.Value.BoneCount);
            var vertices = renderer.sharedMesh.vertices;
            var weights = renderer.sharedMesh.boneWeights;
            var system = world.GetOrCreateSystem<EnemyAnimationSystem>();
            world.SetTime(new TimeData(0, 0));
            for (int frame = 0; frame < samples.Value.FrameCount; frame++)
            {
                em.SetComponentData(visual, new EnemyAnimationPlayback { Initialized = 1, Landing = 1,
                    ImpactPhase = frame / (float)(samples.Value.FrameCount - 1) });
                system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                var bounds = MeshBounds(em.GetBuffer<SkinMatrix>(visual), vertices, weights);
                Assert.That(bounds.min.y * scale, Is.EqualTo(clearance).Within(.001f),
                    $"Death frame {frame} must use the same clearance as movement, not float above it.");
                var unshifted = bounds;
                unshifted.center -= Vector3.up * (clearance / scale);
                Assert.That(renderer.localBounds.Contains(unshifted.min), Is.True);
                Assert.That(renderer.localBounds.Contains(unshifted.max), Is.True);
            }
            var final = MeshBounds(em.GetBuffer<SkinMatrix>(visual), vertices, weights);
            world.SetTime(new TimeData(2, 2));
            system.Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            Assert.That(MeshBounds(em.GetBuffer<SkinMatrix>(visual), vertices, weights).min.y,
                Is.EqualTo(final.min.y).Within(.0001f), "The terminal death pose holds its altitude.");
            Assert.That(em.GetComponentData<LocalTransform>(owner), Is.EqualTo(LocalTransform.Identity));
        }

        private static Bounds MeshBounds(DynamicBuffer<SkinMatrix> skin, Vector3[] vertices, BoneWeight[] weights)
        {
            Bounds bounds = default;
            for (int vertex = 0; vertex < vertices.Length; vertex++)
            {
                var w = weights[vertex]; var p = new float4(vertices[vertex], 1);
                float3 point = math.mul(skin[w.boneIndex0].Value, p) * w.weight0
                    + math.mul(skin[w.boneIndex1].Value, p) * w.weight1
                    + math.mul(skin[w.boneIndex2].Value, p) * w.weight2
                    + math.mul(skin[w.boneIndex3].Value, p) * w.weight3;
                if (vertex == 0) bounds = new Bounds(point, Vector3.zero);
                else bounds.Encapsulate(point);
            }
            return bounds;
        }

        private static BlobAssetReference<EnemyAnimationSamples> ReadSamples(SkinnedMeshRenderer renderer)
        {
            using var reader = new BinaryReader(new MemoryStream(renderer.GetComponent<EnemyAnimationAuthoring>().Samples.bytes));
            Assert.That(reader.ReadInt32(), Is.EqualTo(0x43504133)); reader.ReadString();
            int bones = reader.ReadInt32(), frames = reader.ReadInt32();
            for (int i = 0; i < bones; i++) reader.ReadString();
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var data = ref builder.ConstructRoot<EnemyAnimationSamples>();
            data.BoneCount = bones; data.FrameCount = frames;
            var durations = builder.Allocate(ref data.Durations, EnemyAnimationSamples.MotionCount);
            for (int i = 0; i < durations.Length; i++) durations[i] = reader.ReadSingle();
            var matrices = builder.Allocate(ref data.Matrices, EnemyAnimationSamples.MotionCount * frames * bones);
            for (int i = 0; i < matrices.Length; i++)
                matrices[i] = new float3x4(ReadColumn(reader), ReadColumn(reader), ReadColumn(reader), ReadColumn(reader));
            return builder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
        }

        private static float3 ReadColumn(BinaryReader reader) => new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }
}
