using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Rendering;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class ArmoredGoblinPresentationTests
    {
        [Test]
        public void Enemy014EquipmentDisappearsInOrderAndReturnsOnPooledReuse()
        {
            using var world = new World("Armored equipment");
            var em = world.EntityManager;
            var owner = em.CreateEntity(typeof(EnemyArmor));
            var helmet = em.CreateEntity(typeof(EnemyArmorVisual), typeof(MaterialMeshInfo));
            var shield = em.CreateEntity(typeof(EnemyArmorVisual), typeof(MaterialMeshInfo));
            em.SetComponentData(helmet, new EnemyArmorVisual { Owner = owner, StagesRequired = 2 });
            em.SetComponentData(shield, new EnemyArmorVisual { Owner = owner, StagesRequired = 1 });
            var system = world.GetOrCreateSystem<EnemyArmorVisualSystem>();
            foreach (byte stages in new byte[] { 2, 1, 0, 2 })
            {
                em.SetComponentData(owner, new EnemyArmor { Stages = stages });
                system.Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();
                Assert.AreEqual(stages == 2, em.IsComponentEnabled<MaterialMeshInfo>(helmet));
                Assert.AreEqual(stages > 0, em.IsComponentEnabled<MaterialMeshInfo>(shield));
            }
        }

        [Test]
        public void Info004ArmoredKeepsAuthoredScalesTexturedCelAndGroundSpawn()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CrowdPunch/Prefabs/Enemies/Goblin Trooper Armored Variant.prefab");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CrowdPunch/Prefabs/ArmoredEnemy.prefab");
            var visual = prefab.GetComponentInChildren<Animator>(true).transform;
            foreach (var transform in source.GetComponentsInChildren<Transform>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(transform, source.transform);
                var target = string.IsNullOrEmpty(path) ? visual : visual.Find(path);
                Assert.NotNull(target, path);
                Assert.AreEqual(transform.localScale, target.localScale, path);
            }
            int helmet = 0, shield = 0, jogBodies = 0;
            foreach (var part in prefab.GetComponentsInChildren<EnemyAnimationAuthoring>(true))
            {
                Assert.AreEqual(EnemyAnimationProfile.GoblinArmored, part.Profile);
                Assert.AreEqual(7, part.SpawnMotion);
                Assert.NotNull(part.Samples);
                if (part.ArmorStagesRequired == 2) { helmet++; Assert.AreEqual("Helmet 05", part.name); }
                if (part.ArmorStagesRequired == 1) { shield++; Assert.AreEqual("Shield 01", part.name); }
                if (part.JogSamples != null) jogBodies++;
                foreach (var material in part.GetComponent<SkinnedMeshRenderer>().sharedMaterials)
                {
                    Assert.AreEqual("CrowdPunch/Cel/Enemy", material.shader.name);
                    var texture = material.GetTexture("_ColorMap");
                    Assert.NotNull(texture);
                    CrowdPunch.Editor.EnemyAppearanceMaterials.Configure(material);
                    Assert.AreSame(texture, material.GetTexture("_ColorMap"));
                }
            }
            Assert.AreEqual(1, helmet);
            Assert.AreEqual(1, shield);
            Assert.AreEqual(1, jogBodies);
            var settings = AssetDatabase.LoadAssetAtPath<CrowdPunch.Configuration.EnemySpawnSettings>(
                "Assets/CrowdPunch/Data/Settings/Enemies/ArmoredEnemySpawnSettings.asset");
            Assert.IsTrue(new SerializedObject(settings).FindProperty("spawnFromGround").boolValue);
        }
    }
}
