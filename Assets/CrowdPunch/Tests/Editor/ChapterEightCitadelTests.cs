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
    public sealed class ChapterEightCitadelTests
    {
        private SceneSetup[] setup;
        [SetUp] public void SaveSetup()=>setup=EditorSceneManager.GetSceneManagerSetup();
        [TearDown] public void RestoreSetup()
        {
            if(setup.Any(s=>s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }

        [TestCase(71)] [TestCase(72)] [TestCase(73)] [TestCase(74)] [TestCase(75)]
        [TestCase(76)] [TestCase(77)] [TestCase(78)] [TestCase(79)] [TestCase(80)]
        public void Vision006_BakedScenesHaveNoDecorativeCollisionAndKeepDynamicCuesVisible(int stage)
        {
            var main=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage}.unity");
            var root=main.GetRootGameObjects().Single(g=>g.name=="Chapter Eight - Ashen Crown Citadel");
            Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
            var lighting=main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CelLevelLighting>()).Single();
            Assert.That(lighting.GetComponent<BakedShadowLighting>(),Is.Not.Null);
            Assert.That(lighting.sceneryOnlyHaze,Is.False,"Citadel haze must start beyond the complete combat bounds.");
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
                Assert.That(floor.lightmapIndex,Is.InRange(0,LightmapSettings.lightmaps.Length-1));
                Assert.That(material.GetTexture("_BakedShadowMask"),Is.EqualTo(LightmapSettings.lightmaps[floor.lightmapIndex].shadowMask));
                Assert.That(Vector4.Distance(material.GetVector("_BakedShadowST"),floor.lightmapScaleOffset),Is.LessThan(.00001f));
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
                if(r.name=="Continuous citadel ash earth")
                {
                    Assert.That(p.max.y,Is.LessThan(floors.Min(f=>f.bounds.max.y)-.10f));
                    float padding=Mathf.Min(court.min.x-p.min.x,p.max.x-court.max.x,court.min.z-p.min.z,p.max.z-court.max.z);
                    Assert.That(padding,Is.GreaterThan(lighting.hazeEnd),"Exterior ground must continue beyond haze from every arena edge.");
                    continue;
                }
                if(r.name.StartsWith("Closed volcanic skyline "))
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
                foreach(var m in r.sharedMaterials) Assert.That(m.GetFloat("_CameraCutaway"),Is.EqualTo(r.name=="Unused scenery exception"?0:1));
            }
            foreach(var patch in sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GroundHazardAuthoring>(true)))
                Assert.That(patch.GetComponent<GroundHazardAppearanceAuthoring>().style,Is.EqualTo(GroundHazardStyle.Citadel));
        }

        [Test] public void Ground007_CitadelParticlesUseTheRealPhaseAndClearWhenUnintroduced()
        {
            using var world=new Unity.Entities.World("Citadel particle presentation regression");
            var em=world.EntityManager;
            var entity=em.CreateEntity(typeof(GroundHazard),typeof(GroundHazardState),typeof(GroundHazardAppearance));
            em.SetComponentData(entity,new GroundHazard{Shape=GroundHazardShape.Circle,Radius=3});
            em.SetComponentData(entity,new GroundHazardAppearance{Style=GroundHazardStyle.Citadel});
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
        [TestCase(75)] [TestCase(77)]
        public void Protect004_InlayRetainsTheExactBreachBoundaryWithoutCollision(int stage)
        {
            var scene=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage}/Campaign_{stage} Sub Scene.unity");
            var objective=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProtectedPointAuthoring>()).Single();
            var inlay=scene.GetRootGameObjects().Single(g=>g.name=="Citadel protected supply inlay");
            Assert.That(inlay.GetComponentsInChildren<Collider>(true),Is.Empty);
            var parts=inlay.GetComponentsInChildren<MeshRenderer>();
            var b=parts[0].bounds;foreach(var part in parts.Skip(1))b.Encapsulate(part.bounds);
            Assert.That(b.min.x,Is.EqualTo(objective.zoneCenter.x-objective.zoneSize.x*.5f).Within(.001f));
            Assert.That(b.max.x,Is.EqualTo(objective.zoneCenter.x+objective.zoneSize.x*.5f).Within(.001f));
            Assert.That(b.min.z,Is.EqualTo(objective.zoneCenter.y-objective.zoneSize.y*.5f).Within(.001f));
            Assert.That(b.max.z,Is.EqualTo(objective.zoneCenter.y+objective.zoneSize.y*.5f).Within(.001f));
            Assert.That(b.size.y,Is.LessThan(.05f));
        }
        [Test] public void Shell005_ReliquaryHasOneBranchingFracturePerStageAndExposedFace()
        {
            var scene=EditorSceneManager.OpenScene("Assets/CrowdPunch/Scenes/Campaign/Campaign_76/Campaign_76 Sub Scene.unity");
            var gate=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ShellTargetAuthoring>()).Single().GetComponent<BarricadeAuthoring>();
            var cracks=gate.GetComponentsInChildren<BarricadeVisualAuthoring>().Where(v=>v.crackStage>0 && v.name!="Damage stage 1" && v.name!="Damage stage 2" && v.transform.parent.name=="Citadel shell assembly").ToArray();
            foreach(bool core in new[]{false,true})foreach(int stage in new[]{1,2})
            {
                var faces=cracks.Where(v=>v.shellCore==core && v.crackStage==stage).ToArray();Assert.That(faces.Length,Is.EqualTo(4));
                Assert.That(faces.Select(v=>Mathf.RoundToInt(v.transform.localEulerAngles.y/90)).Distinct().Count(),Is.EqualTo(4));
                foreach(var face in faces)Assert.That(face.GetComponent<MeshFilter>().sharedMesh.vertexCount,Is.GreaterThan(20));
            }
        }
    }
}
