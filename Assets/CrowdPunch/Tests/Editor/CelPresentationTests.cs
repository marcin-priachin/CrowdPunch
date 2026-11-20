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
        private static readonly string[] ChapterProfiles =
        {
            "PaintedVillagePost", "PaintedFoundryPost", "HighlandPaintedPost", "ObservatoryPaintedPost",
            "NecropolisPaintedPost", "CanyonPaintedPost", "VaultPaintedPost", "CitadelPaintedPost", "CrownPaintedPost"
        };

        private SceneSetup[] setup;
        [OneTimeSetUp] public void SaveSetup() => setup=EditorSceneManager.GetSceneManagerSetup();
        [OneTimeTearDown] public void RestoreSetup()
        {
            // The Test Runner can begin with only unloaded scene placeholders.
            if (setup.Any(s=>s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
        public static int[] CampaignNumbers => AssetDatabase.LoadAssetAtPath<CrowdPunch.Configuration.CampaignCatalog>(CampaignBuildRegistration.CatalogPath)
            .levels.Where(level=>level.Available).Select(level=>int.Parse(System.IO.Path.GetFileNameWithoutExtension(level.scenePath).Split('_').Last())).ToArray();

        [TestCaseSource(nameof(CampaignNumbers))]
        public void Vision006_EveryCampaignLevelHasProductionPresentation(int number)
        {
            var scene=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{number:00}.unity");
            var roots=scene.GetRootGameObjects();
            var volumes=roots.SelectMany(r=>r.GetComponentsInChildren<Volume>(true));
            var catalog=AssetDatabase.LoadAssetAtPath<CrowdPunch.Configuration.CampaignCatalog>(CampaignBuildRegistration.CatalogPath);
            int chapter=catalog.ChapterFor(number-1);
            var profile=volumes.Single(v=>v.enabled && v.isGlobal && v.weight==1 && v.priority==150).sharedProfile;
            Assert.That(profile.name,Is.EqualTo(ChapterProfiles[chapter]),scene.path);
            Assert.That(profile.TryGet<Bloom>(out var bloom) && bloom.active && bloom.intensity.overrideState,Is.True,scene.path);
            Assert.That(bloom.intensity.value,Is.EqualTo(.12f),scene.path);
            var lighting=roots.SelectMany(r=>r.GetComponentsInChildren<CelLevelLighting>(true)).Single();
            Assert.That(lighting.ambientMultiplier,Is.EqualTo(chapter==0?1.08f:1f)); Assert.That(lighting.sun,Is.Not.Null);
            CheckRenderers(roots,scene.path);
            var subscenes=roots.SelectMany(r=>r.GetComponentsInChildren<Unity.Scenes.SubScene>(true))
                .Select(s=>AssetDatabase.GetAssetPath(s.SceneAsset)).ToArray();
            Assert.That(subscenes,Is.Not.Empty);
            int courts=0;
            foreach (string sub in subscenes)
            {
                var subRoots=EditorSceneManager.OpenScene(sub).GetRootGameObjects();
                CheckRenderers(subRoots,sub);
                foreach (var floor in subRoots.SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>(true))
                    .Where(r=>r.name=="Continuous Convex Floor" || r.name=="Continuous Round Floor"))
                {
                    var bounds=floor.bounds;
                    var expected=new Vector4(bounds.center.x,bounds.center.z,bounds.extents.x,bounds.extents.z);
                    Assert.That(bounds.extents.x,Is.Positive,sub);
                    Assert.That(bounds.extents.z,Is.Positive,sub);
                    Assert.That(Vector4.Distance(floor.sharedMaterial.GetVector("_CourtPaintBounds"),expected),Is.LessThan(.01f),sub);
                    courts++;
                }
            }
            Assert.That(courts,Is.Positive,scene.path);
        }

        [Test]
        public void Vision006_AllGamePrefabsUseProductionSurfaces()
        {
            foreach (string path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/CrowdPunch"}).Select(AssetDatabase.GUIDToAssetPath))
            {
                // Enemy builders replace the source model variants' materials on the playable prefabs.
                if (path.StartsWith("Assets/CrowdPunch/Prefabs/Enemies/",System.StringComparison.Ordinal) &&
                    path.EndsWith(" Variant.prefab",System.StringComparison.Ordinal)) continue;
                CheckRenderers(new[]{AssetDatabase.LoadAssetAtPath<GameObject>(path)},path);
            }
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
                if (!renderer.enabled && renderer.GetComponentInParent<CrowdPunch.Authoring.EnemyAnimationAuthoring>(true)?.ProjectileOrigin==renderer.transform) continue;
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
