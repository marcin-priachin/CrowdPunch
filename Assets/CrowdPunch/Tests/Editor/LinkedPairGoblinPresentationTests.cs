using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class LinkedPairGoblinPresentationTests
    {
        [Test]
        public void Link006PrefabRetainsAuthoredScalePaletteAndGroundSpawn()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CrowdPunch/Prefabs/Enemies/Goblin Trooper Linked Variant.prefab");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CrowdPunch/Prefabs/EnemyLinkedPair.prefab");
            var animator = prefab.GetComponentInChildren<Animator>(true);
            foreach (var transform in source.GetComponentsInChildren<Transform>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(transform, source.transform);
                var target = string.IsNullOrEmpty(path) ? animator.transform : animator.transform.Find(path);
                Assert.NotNull(target, path);
                Assert.AreEqual(transform.localScale, target.localScale, path);
            }
            Assert.NotNull(prefab.GetComponent<LinkedPairAuthoring>().settings);
            Assert.NotNull(animator.GetBoneTransform(HumanBodyBones.LeftHand).Find("+ L Hand"));
            Assert.NotNull(animator.GetBoneTransform(HumanBodyBones.RightHand).Find("+ R Hand"));
            foreach (var part in prefab.GetComponentsInChildren<EnemyAnimationAuthoring>(true))
            {
                Assert.AreEqual(EnemyAnimationProfile.Goblin, part.Profile);
                Assert.AreEqual(7, part.SpawnMotion);
                Assert.NotNull(part.Samples);
                Assert.NotNull(part.JogSamples);
                foreach (var material in part.GetComponent<SkinnedMeshRenderer>().sharedMaterials)
                {
                    Assert.AreEqual("CrowdPunch/Cel/Enemy", material.shader.name);
                    var texture = material.GetTexture("_ColorMap");
                    Assert.NotNull(texture);
                    CrowdPunch.Editor.EnemyAppearanceMaterials.Configure(material);
                    Assert.AreSame(texture, material.GetTexture("_ColorMap"));
                }
            }
            var settings = AssetDatabase.LoadAssetAtPath<CrowdPunch.Configuration.EnemySpawnSettings>(
                "Assets/CrowdPunch/Data/Settings/Enemies/LinkedPairEnemySpawnSettings.asset");
            Assert.IsTrue(new SerializedObject(settings).FindProperty("spawnFromGround").boolValue);
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            var states = controller.layers[0].stateMachine.states;
            foreach (int slot in new[] { 1, 2 })
                StringAssert.Contains("Goblin Giant Modular Pack Cute Series/Animations/", AssetDatabase.GetAssetPath(states[slot].state.motion));
            StringAssert.EndsWith("Char@Spawn.FBX", AssetDatabase.GetAssetPath(states[7].state.motion));
        }

        [Test]
        public void Link006TetherSelectsFacingHandAndSwapsWhenPartnersCross()
        {
            var left = new float3(-1, 2, 0);
            var right = new float3(1, 2, 0);
            Assert.AreEqual(left, LinkedPairPresentationSystem.FacingHand(left, right, new float3(-5, 0, 0)));
            Assert.AreEqual(right, LinkedPairPresentationSystem.FacingHand(left, right, new float3(5, 0, 0)));
            // World-space hand positions already include enemy yaw and the final animated pose.
            Assert.AreEqual(left, LinkedPairPresentationSystem.FacingHand(right, left, new float3(-5, 0, 0)));
        }
    }
}
