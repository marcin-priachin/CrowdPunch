using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Core;
using Unity.Deformations;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
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
        public void Enemy014BrokenEquipmentFallsBlinksStaysInWorldAndResets()
        {
            using var world = new World("Falling armor");
            var em = world.EntityManager;
            var owner = em.CreateEntity(typeof(EnemyArmor), typeof(EnemyLifetime), typeof(RespawnRequest));
            em.SetComponentEnabled<RespawnRequest>(owner, false);
            var root = em.CreateEntity(typeof(LocalToWorld));
            em.SetComponentData(root, new LocalToWorld { Value = float4x4.identity });
            var helmet = em.CreateEntity(typeof(EnemyArmorVisual), typeof(EnemyArmorDebris),
                typeof(MaterialMeshInfo), typeof(LocalToWorld), typeof(RenderBounds));
            em.SetComponentData(helmet, new LocalToWorld { Value = float4x4.identity });
            em.SetComponentData(helmet, new EnemyArmorVisual { Owner = owner, Animation = helmet, StagesRequired = 2 });
            em.SetComponentData(helmet, new EnemyArmorDebris { Root = root,
                MeshBounds = new AABB { Center = float3.zero, Extents = new float3(.1f) } });
            var skin = em.AddBuffer<SkinMatrix>(helmet);
            skin.Add(new SkinMatrix { Value = new float3x4(new float3(1, 0, 0), new float3(0, 1, 0),
                new float3(0, 0, 1), new float3(0, 2, 0)) });
            var system = world.GetOrCreateSystem<EnemyArmorVisualSystem>();
            em.SetComponentData(owner, new EnemyArmor { Stages = 2 });
            Step(0);
            em.SetComponentData(owner, new EnemyArmor { Stages = 1 });
            Step(0);
            Assert.IsTrue(em.IsComponentEnabled<MaterialMeshInfo>(helmet), "Break frame remains visible");
            Step(.2f);
            var falling = em.GetComponentData<EnemyArmorDebris>(helmet);
            Assert.Less(falling.WorldPose.c3.y, 2f, "Gravity lowers the detached helmet");
            em.SetComponentData(root, new LocalToWorld { Value = float4x4.Translate(new float3(10, 0, 0)) });
            Step(0);
            Assert.AreEqual(falling.WorldPose.c3.x, em.GetBuffer<SkinMatrix>(helmet)[0].Value.c3.x + 10f, .001f,
                "Equipment does not follow the moving owner");
            Step(.2f);
            Assert.IsFalse(em.IsComponentEnabled<MaterialMeshInfo>(helmet), "Blink off");
            Step(.13f);
            Assert.IsTrue(em.IsComponentEnabled<MaterialMeshInfo>(helmet), "Blink on after a disabled-render frame");
            Step(2f);
            Assert.IsFalse(em.IsComponentEnabled<MaterialMeshInfo>(helmet), "Debris lifetime expires");
            em.SetComponentData(owner, new EnemyLifetime { Generation = 1 });
            em.SetComponentData(owner, new EnemyArmor { Stages = 2 });
            Step(0);
            Assert.IsTrue(em.IsComponentEnabled<MaterialMeshInfo>(helmet), "Fresh armor is restored");
            Assert.AreEqual(0, em.GetComponentData<EnemyArmorDebris>(helmet).Detached);
            em.SetComponentData(owner, new EnemyArmor { Stages = 1 });
            Step(0);
            Assert.AreEqual(1, em.GetComponentData<EnemyArmorDebris>(helmet).Detached, "Reused helmet can break again");
            em.SetComponentData(owner, new RespawnRequest { IsPooled = 1 });
            em.SetComponentEnabled<RespawnRequest>(owner, true);
            Step(0);
            Assert.IsFalse(em.IsComponentEnabled<MaterialMeshInfo>(helmet), "Pooling clears detached debris");

            void Step(float dt)
            {
                world.SetTime(new TimeData(world.Time.ElapsedTime + dt, dt));
                system.Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();
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
