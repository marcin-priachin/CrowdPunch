using System.Linq;
using CrowdPunch.Editor;
using CrowdPunch.Mono.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Tests
{
    public sealed class CelPresentationTests
    {
        private SceneSetup[] setup;
        [OneTimeSetUp] public void SaveSetup() => setup=EditorSceneManager.GetSceneManagerSetup();
        [OneTimeTearDown] public void RestoreSetup() => EditorSceneManager.RestoreSceneManagerSetup(setup);
        public static int[] CampaignNumbers => AssetDatabase.LoadAssetAtPath<CrowdPunch.Configuration.CampaignCatalog>(CampaignBuildRegistration.CatalogPath)
            .levels.Where(level=>level.Available).Select(level=>int.Parse(System.IO.Path.GetFileNameWithoutExtension(level.scenePath).Split('_').Last())).ToArray();

        [TestCaseSource(nameof(CampaignNumbers))]
        public void Vision006_EveryCampaignLevelHasProductionPresentation(int number)
        {
            var scene=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{number:00}.unity");
            var roots=scene.GetRootGameObjects();
            var volumes=roots.SelectMany(r=>r.GetComponentsInChildren<Volume>(true));
            Assert.That(volumes.Any(v=>v.enabled && v.isGlobal && v.weight==1 && v.priority==150 &&
                AssetDatabase.GetAssetPath(v.sharedProfile)==CelPresentationAuthoring.ProfilePath),Is.True,scene.path);
            var lighting=roots.SelectMany(r=>r.GetComponentsInChildren<CelLevelLighting>(true)).Single();
            Assert.That(lighting.ambientMultiplier,Is.EqualTo(number>=11 && number<=20?1f:1.08f)); Assert.That(lighting.sun,Is.Not.Null);
            CheckRenderers(roots,scene.path);
            var subscenes=roots.SelectMany(r=>r.GetComponentsInChildren<Unity.Scenes.SubScene>(true))
                .Select(s=>AssetDatabase.GetAssetPath(s.SceneAsset)).ToArray();
            Assert.That(subscenes,Is.Not.Empty);
            foreach (string sub in subscenes) CheckRenderers(EditorSceneManager.OpenScene(sub).GetRootGameObjects(),sub);
        }

        [Test]
        public void Vision006_AllGamePrefabsUseProductionSurfaces()
        {
            foreach (string path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/CrowdPunch"}).Select(AssetDatabase.GUIDToAssetPath))
                CheckRenderers(new[]{AssetDatabase.LoadAssetAtPath<GameObject>(path)},path);
        }

        [Test]
        public void Vision006_BootstrapAndProfileEnableTheApprovedPostProcessing()
        {
            var scene=EditorSceneManager.OpenScene(CampaignBuildRegistration.BootstrapPath);
            CheckRenderers(scene.GetRootGameObjects(),scene.path);
            var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).Single(c=>c.CompareTag("MainCamera"));
            var data=camera.GetComponent<UniversalAdditionalCameraData>();
            Assert.That(camera.allowHDR && data.renderPostProcessing && (data.volumeLayerMask.value&1)!=0,Is.True);
            Assert.That(data.antialiasing,Is.EqualTo(AntialiasingMode.SubpixelMorphologicalAntiAliasing));
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(CelPresentationAuthoring.ProfilePath);
            Assert.That(profile.TryGet<Tonemapping>(out var tone),Is.True);
            Assert.That(tone.mode.overrideState && tone.mode.value==TonemappingMode.Neutral,Is.True);
            Assert.That(profile.TryGet<ColorAdjustments>(out var grade),Is.True);
            Assert.That(grade.contrast.value,Is.EqualTo(5)); Assert.That(grade.saturation.value,Is.EqualTo(4));
            Assert.That(profile.TryGet<Bloom>(out var bloom),Is.True); Assert.That(bloom.intensity.value,Is.EqualTo(.06f));
            Assert.That(AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline),Is.EqualTo(CelPresentationAuthoring.PipelinePath));
            Assert.That(((UniversalRenderPipelineAsset)GraphicsSettings.defaultRenderPipeline).supportsHDR,Is.True);
        }

        private static void CheckRenderers(GameObject[] roots,string context)
        {
            foreach (var renderer in roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)))
            {
                if (renderer is ParticleSystemRenderer) continue;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material==null) continue;
                    Assert.That(material.shader,Is.Not.Null,context+" / "+renderer.name);
                    Assert.That(CelPresentationAuthoring.ShaderKind(material.shader.name),Is.Null,
                        context+" / "+renderer.name+" still uses "+material.shader.name);
                    Assert.That(material.shader.isSupported,Is.True,context+" / "+material.name);
                }
            }
        }
    }
}
