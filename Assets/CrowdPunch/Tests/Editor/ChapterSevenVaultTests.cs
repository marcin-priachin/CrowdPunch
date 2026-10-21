using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Mono.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Tests
{
    public sealed class ChapterSevenVaultTests
    {
        private SceneSetup[] setup;
        [SetUp] public void SaveSetup()=>setup=EditorSceneManager.GetSceneManagerSetup();
        [TearDown] public void RestoreSetup()
        {
            if(setup.Any(s=>s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }

        [TestCase(61)] [TestCase(62)] [TestCase(63)] [TestCase(64)] [TestCase(65)]
        [TestCase(66)] [TestCase(67)] [TestCase(68)] [TestCase(69)] [TestCase(70)]
        public void Vision006_BakedScenesHaveNoDecorativeCollisionAndKeepDynamicCuesVisible(int stage)
        {
            var main=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage}.unity");
            var root=main.GetRootGameObjects().Single(g=>g.name=="Chapter Seven - Frostbound Vault");
            Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
            var lighting=main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CelLevelLighting>()).Single();
            Assert.That(lighting.GetComponent<BakedShadowLighting>(),Is.Not.Null);
            Assert.That(lighting.sceneryOnlyHaze,Is.False,"Vault haze must start beyond the complete combat bounds.");
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                Assert.That(GameObjectUtility.GetStaticEditorFlags(renderer.gameObject).HasFlag(StaticEditorFlags.ContributeGI),Is.True);
                Assert.That(renderer.lightmapIndex,Is.InRange(0,253));
                Assert.That(renderer.GetComponent<MeshFilter>().sharedMesh.uv2,Is.Not.Empty);
            }
            var sub=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage}/Campaign_{stage} Sub Scene.unity",OpenSceneMode.Additive);
            var renderers=sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            var floors=renderers.Where(r=>r.name=="Continuous Convex Floor" || r.name=="Continuous Round Floor").ToArray();
            var court=floors[0].bounds;foreach(var f in floors.Skip(1))court.Encapsulate(f.bounds);
            Assert.That(lighting.hazeStart,Is.GreaterThanOrEqualTo(court.extents.magnitude+30));
            foreach(var floor in renderers.Where(r=>r.name=="Continuous Convex Floor" || r.name=="Continuous Round Floor"))
            {
                var material=floor.sharedMaterial;
                Assert.That(material.GetFloat("_CameraCutaway"),Is.Zero);
                Assert.That(material.GetFloat("_SceneryHaze"),Is.Zero);
                Assert.That(material.GetFloat("_UseBakedShadowMask"),Is.EqualTo(1));
                Assert.That(material.GetTexture("_BakedShadowMask"),Is.Not.Null);
                string source=material.GetTag("CrowdPunchSourceMaterial",false,"");
                Assert.That(source,Is.Not.EqualTo(AssetDatabase.GetAssetPath(material)));
                Assert.That(AssetDatabase.LoadAssetAtPath<Material>(source),Is.Not.Null);
            }
            foreach(var r in renderers.Where(r=>r.GetComponent<BarricadeVisualAuthoring>()!=null || r.GetComponent<CoverPanelAuthoring>()!=null || r.GetComponent<VillageExitCueAuthoring>()!=null))
            {
                Assert.That(r.shadowCastingMode,Is.EqualTo(ShadowCastingMode.Off));
                for(var t=r.transform;t!=null;t=t.parent)Assert.That(t.gameObject.isStatic,Is.False,"Dynamic presentation must not inherit static flags.");
                foreach(var material in r.sharedMaterials)
                {
                    if(material.HasProperty("_CameraCutaway"))Assert.That(material.GetFloat("_CameraCutaway"),Is.Zero);
                    if(material.HasProperty("_SceneryHaze"))Assert.That(material.GetFloat("_SceneryHaze"),Is.Zero);
                }
            }
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var p=r.bounds;
                if(r.name=="Continuous frost earth") { Assert.That(p.max.y,Is.LessThan(floors.Min(f=>f.bounds.max.y)-.10f)); continue; }
                if(r.name.StartsWith("Closed glacial skyline "))
                {
                    foreach(var vertex in r.GetComponent<MeshFilter>().sharedMesh.vertices)
                    {
                        var point=r.transform.TransformPoint(vertex);
                        Assert.That(point.x<court.min.x || point.x>court.max.x || point.z<court.min.z || point.z>court.max.z,Is.True);
                    }
                    continue;
                }
                Assert.That(p.max.x<court.min.x || p.min.x>court.max.x || p.max.z<court.min.z || p.min.z>court.max.z,Is.True,r.name+" enters combat");
                Assert.That(r.forceMeshLod,Is.Zero);
                foreach(var m in r.sharedMaterials) Assert.That(m.GetFloat("_CameraCutaway"),Is.EqualTo(r.name=="Recessed meltwater channel"?0:1));
            }
            foreach(var patch in sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GroundHazardAuthoring>(true)))
                Assert.That(patch.GetComponent<GroundHazardAppearanceAuthoring>().style,Is.EqualTo(GroundHazardStyle.Vault));
        }

        [TestCase(61)] [TestCase(62)] [TestCase(63)] [TestCase(64)] [TestCase(65)]
        [TestCase(66)] [TestCase(67)] [TestCase(68)] [TestCase(69)] [TestCase(70)]
        public void Vision005_SnowIsBoundedSceneWeatherThatPausesWithoutGameplayComponents(int stage)
        {
            var scene=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage}.unity");
            var snow=scene.GetRootGameObjects().Single(g=>g.name=="Chapter Seven - Cartoon Snowfall");
            var particles=snow.GetComponent<ParticleSystem>();var main=particles.main;
            Assert.That(main.loop && main.prewarm && main.playOnAwake,Is.True);
            Assert.That(main.useUnscaledTime,Is.False);
            Assert.That(main.simulationSpace,Is.EqualTo(ParticleSystemSimulationSpace.World));
            Assert.That(main.maxParticles,Is.InRange(1,1024));
            Assert.That(particles.emission.rateOverTime.constant,Is.InRange(18,100));
            Assert.That(particles.velocityOverLifetime.y.constantMax,Is.LessThan(0));
            Assert.That(particles.collision.enabled || particles.trigger.enabled || particles.lights.enabled,Is.False);
            Assert.That(snow.GetComponentsInChildren<Collider>(true),Is.Empty);
            Assert.That(snow.GetComponentsInChildren<MonoBehaviour>(true),Is.Empty);
            Assert.That((int)GameObjectUtility.GetStaticEditorFlags(snow),Is.Zero);
            var renderer=snow.GetComponent<ParticleSystemRenderer>();
            Assert.That(renderer.shadowCastingMode,Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(renderer.receiveShadows,Is.False);
            Assert.That(renderer.maxParticleSize,Is.LessThanOrEqualTo(.012f));
            Assert.That(renderer.sharedMaterial.shader.name,Is.EqualTo("CrowdPunch/Chapter Seven Cartoon Snow"));
            Assert.That(ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader),Is.False);
        }

        [Test] public void Vision006_OwnedTexturesPreserveMipsAndFiltering()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/CrowdPunch/Data/ChapterSevenVault/Textures"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(texture.mipmapCount,Is.GreaterThan(1));Assert.That(Mathf.Max(texture.width,texture.height),Is.LessThanOrEqualTo(2048));
                Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Trilinear));Assert.That(importer.anisoLevel,Is.EqualTo(4));Assert.That(importer.wrapMode,Is.EqualTo(TextureWrapMode.Repeat));
            }
        }

        [TestCase(61)] [TestCase(62)] [TestCase(68)] [TestCase(70)]
        public void Vision006_LongWallShadowChartsHaveUniformPhysicalScale(int stage)
        {
            var scene=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage}.unity");
            var root=scene.GetRootGameObjects().Single(g=>g.name=="Chapter Seven - Frostbound Vault");
            var walls=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Connected vault side gallery" || f.name=="Vault return wall").ToArray();
            Assert.That(walls.Length,Is.EqualTo(4));
            foreach(var wall in walls)
            {
                var mesh=wall.sharedMesh;var vertices=mesh.vertices;var uv=mesh.uv2;var indices=mesh.triangles;
                float min=float.PositiveInfinity,max=0;
                for(int i=0;i<indices.Length;i+=3)for(int edge=0;edge<3;edge++)
                {
                    int a=indices[i+edge],b=indices[i+(edge+1)%3];
                    float distance=wall.transform.TransformVector(vertices[b]-vertices[a]).magnitude;
                    float ratio=Vector2.Distance(uv[a],uv[b])/distance;min=Mathf.Min(min,ratio);max=Mathf.Max(max,ratio);
                }
                Assert.That(min,Is.GreaterThan(0));Assert.That(max/min,Is.LessThan(1.01f),"Stretched cube islands made pillar shadows into staircases.");
            }
        }

        [TestCase(61)] [TestCase(62)] [TestCase(68)] [TestCase(70)]
        public void Vision004_ReturnWallsDoNotOverlapExposedFrontOrRearFaces(int stage)
        {
            var scene=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage}.unity");
            var root=scene.GetRootGameObjects().Single(g=>g.name=="Chapter Seven - Frostbound Vault");
            foreach(var vault in root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Complete frozen vault entrance"))
            {
                var rear=vault.Find("Closed vault rear");var front=vault.GetComponentsInChildren<Transform>().First(t=>t.name=="Vault doorway pier");
                foreach(var wall in vault.GetComponentsInChildren<Transform>().Where(t=>t.name=="Vault returning wall"))
                {
                    Assert.That(wall.localPosition.z-wall.localScale.z*.5f,Is.EqualTo(front.localPosition.z+front.localScale.z*.5f).Within(.0001f));
                    Assert.That(wall.localPosition.z+wall.localScale.z*.5f,Is.EqualTo(rear.localPosition.z-rear.localScale.z*.5f).Within(.0001f));
                }
            }
        }
        [Test] public void Ground007_VaultParticlesUseTheRealPhaseAndClearWhenUnintroduced()
        {
            using var world=new Unity.Entities.World("Vault particle presentation regression");
            var em=world.EntityManager;
            var entity=em.CreateEntity(typeof(GroundHazard),typeof(GroundHazardState),typeof(GroundHazardAppearance));
            em.SetComponentData(entity,new GroundHazard{Shape=GroundHazardShape.Circle,Radius=3});
            em.SetComponentData(entity,new GroundHazardAppearance{Style=GroundHazardStyle.Vault});
            var camera=new GameObject("Particle test camera",typeof(Camera));camera.tag="MainCamera";
            Mesh generated=null;
            try
            {
                var system=world.GetOrCreateSystemManaged<CrowdPunch.Systems.Presentation.GroundHazardParticlePresentationSystem>();
                var field=system.GetType().GetField("mesh",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                generated=(Mesh)field.GetValue(system);
                foreach(var phase in new[]{GroundHazardPhase.Inactive,GroundHazardPhase.Warning,GroundHazardPhase.Active})
                {
                    world.SetTime(new Unity.Core.TimeData(1,.02f));em.SetComponentData(entity,new GroundHazardState{Introduced=1,Phase=phase});system.Update();
                    Assert.That(((Mesh)field.GetValue(system)).vertexCount,Is.GreaterThan(0),phase.ToString());
                }
                em.SetComponentData(entity,new GroundHazardState());system.Update();Assert.That(((Mesh)field.GetValue(system)).vertexCount,Is.Zero);
            }
            finally{if(generated!=null)Object.DestroyImmediate(generated);Object.DestroyImmediate(camera);}
        }
        [Test] public void Shell005_ReliquaryHasOneBranchingFracturePerStageAndExposedFace()
        {
            var scene=EditorSceneManager.OpenScene("Assets/CrowdPunch/Scenes/Campaign/Campaign_64/Campaign_64 Sub Scene.unity");
            var gate=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ShellTargetAuthoring>()).Single().GetComponent<BarricadeAuthoring>();
            var cracks=gate.GetComponentsInChildren<BarricadeVisualAuthoring>().Where(v=>v.crackStage>0 && v.name!="Damage stage 1" && v.name!="Damage stage 2" && v.transform.parent.name=="Vault shell assembly").ToArray();
            foreach(bool core in new[]{false,true})foreach(int stage in new[]{1,2})
            {
                var faces=cracks.Where(v=>v.shellCore==core && v.crackStage==stage).ToArray();Assert.That(faces.Length,Is.EqualTo(4));
                Assert.That(faces.Select(v=>Mathf.RoundToInt(v.transform.localEulerAngles.y/90)).Distinct().Count(),Is.EqualTo(4));
                foreach(var face in faces)Assert.That(face.GetComponent<MeshFilter>().sharedMesh.vertexCount,Is.GreaterThan(20));
            }
        }
    }
}
