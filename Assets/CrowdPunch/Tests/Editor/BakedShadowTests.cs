using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Mono.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Tests
{
    public sealed class BakedShadowTests
    {
        private SceneSetup[] setup;
        [SetUp] public void SaveSetup() => setup=EditorSceneManager.GetSceneManagerSetup();
        [TearDown] public void RestoreSetup()
        {
            if(setup.Any(s=>s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.OpenScene("Assets/CrowdPunch/Scenes/Bootstrap.unity");
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
        public void Vision006_SavedChapterHasUsableBakedShadows(int stage)
        {
            var scene=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}.unity");
            var roots=scene.GetRootGameObjects();
            Assert.That(roots.SelectMany(r=>r.GetComponentsInChildren<BakedShadowLighting>(true)).Count(),Is.EqualTo(1));
            var policy=roots.SelectMany(r=>r.GetComponentsInChildren<BakedShadowLighting>(true)).Single();
            Assert.That(policy.bakedProbes,Is.Not.Null);
            Assert.That(policy.bakedProbes.count,Is.GreaterThan(0));
            var sun=roots.SelectMany(r=>r.GetComponentsInChildren<CelLevelLighting>(true)).Single().sun;
            Assert.That(sun.bakingOutput.isBaked,Is.True);
            Assert.That(sun.bakingOutput.mixedLightingMode,Is.EqualTo(MixedLightingMode.Shadowmask));
            Assert.That(sun.bakingOutput.occlusionMaskChannel,Is.InRange(0,3));
            var scenery=roots.Single(r=>r.name=="Chapter One - Sunlit Border Village");
            foreach(var r in scenery.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled))
            {
                Assert.That(GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.ContributeGI),Is.True,r.name);
                Assert.That(r.lightmapIndex,Is.InRange(0,LightmapSettings.lightmaps.Length-1),r.name);
                Assert.That(LightmapSettings.lightmaps[r.lightmapIndex].shadowMask,Is.Not.Null,r.name);
                Assert.That(r.GetComponent<MeshFilter>().sharedMesh.uv2,Is.Not.Empty,r.name);
            }
            foreach(var map in LightmapSettings.lightmaps)
            {
                var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(map.shadowMask)) as TextureImporter;
                Assert.That(importer,Is.Not.Null);
                Assert.That(importer.ignoreMipmapLimit,Is.True,"ECS lightmap-array copies require matching mip limits.");
            }
            var sub=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}/Campaign_{stage:00} Sub Scene.unity");
            var floor=sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true))
                .Single(r=>r.name=="Continuous Convex Floor" || r.name=="Continuous Round Floor");
            foreach(var material in floor.sharedMaterials)
            {
                Assert.That(material.GetFloat("_UseBakedShadowMask"),Is.EqualTo(1));
                Assert.That(material.GetTexture("_BakedShadowMask"),Is.Not.Null);
                Assert.That(material.GetVector("_BakedShadowST"),Is.EqualTo(floor.lightmapScaleOffset));
            }
        }

        [TestCase(4)] [TestCase(6)] [TestCase(7)] [TestCase(10)]
        public void Vision004_ChangingGameplayGeometryIsExcludedFromStaticBake(int stage)
        {
            var scene=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}/Campaign_{stage:00} Sub Scene.unity");
            int changing=0;
            foreach(var r in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)))
            {
                if(r.GetComponent<BarricadeVisualAuthoring>()==null && r.GetComponent<CoverPanelAuthoring>()==null &&
                    r.GetComponentInParent<BossPartAuthoring>()==null) continue;
                changing++;
                Assert.That(GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.ContributeGI),Is.False,r.name);
            }
            Assert.That(changing,Is.GreaterThan(0));
        }
    }
}
