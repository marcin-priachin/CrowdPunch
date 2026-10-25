using System.IO;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using NUnit.Framework;
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
            foreach (var original in source.GetComponentsInChildren<Transform>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(original, source.transform);
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
        public void Wizard006SampledBodyStaysAboveFloorExceptAuthoredEmergence()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CrowdPunch/Prefabs/EnemyWizard.prefab");
            var renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            var authoring = renderer.GetComponent<EnemyAnimationAuthoring>();
            using var reader = new BinaryReader(new MemoryStream(authoring.Samples.bytes));
            Assert.That(reader.ReadInt32(), Is.EqualTo(0x43504133));
            var animator = prefab.GetComponentInChildren<Animator>();
            string hash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)).ToString()
                + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(renderer.sharedMesh))
                + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(animator.avatar));
            Assert.That(reader.ReadString(), Is.EqualTo(hash));
            int bones = reader.ReadInt32(), frames = reader.ReadInt32();
            for (int bone = 0; bone < bones; bone++) reader.ReadString();
            for (int motion = 0; motion < EnemyAnimationSamples.MotionCount; motion++) Assert.That(reader.ReadSingle(), Is.GreaterThan(0));
            var matrices = new Matrix4x4[bones];
            var vertices = renderer.sharedMesh.vertices;
            var weights = renderer.sharedMesh.boneWeights;
            float spawnMinimum = 0;
            for (int motion = 0; motion < EnemyAnimationSamples.MotionCount; motion++)
            for (int frame = 0; frame < frames; frame++)
            {
                for (int bone = 0; bone < bones; bone++)
                {
                    matrices[bone] = Matrix4x4.identity;
                    for (int column = 0; column < 4; column++)
                    for (int row = 0; row < 3; row++) matrices[bone][row, column] = reader.ReadSingle();
                }
                for (int vertex = 0; vertex < vertices.Length; vertex++)
                {
                    var w = weights[vertex]; var v = vertices[vertex];
                    var p = matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0
                        + matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                        + matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2
                        + matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                    Assert.IsTrue(renderer.localBounds.Contains(p), "Culling bounds must include all sampled poses.");
                    if (motion == 7) spawnMinimum = Mathf.Min(spawnMinimum, p.y);
                    else Assert.That(p.y, Is.GreaterThanOrEqualTo(-.0001f), $"Motion {motion}, frame {frame}");
                }
            }
            Assert.That(spawnMinimum, Is.LessThan(-.1f), "Ground spawn must retain the supplied emergence.");
        }
    }
}
