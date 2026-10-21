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
    public sealed class ChapterNineCrownTests
    {
        private SceneSetup[] setup;
        private const string Main = "Assets/CrowdPunch/Scenes/Campaign/Campaign_81.unity";
        private const string Sub = "Assets/CrowdPunch/Scenes/Campaign/Campaign_81/Campaign_81 Sub Scene.unity";
        [SetUp] public void SaveSetup() => setup=EditorSceneManager.GetSceneManagerSetup();
        [TearDown] public void Restore()
        {
            if(setup.Any(s=>s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }

        [Test] public void Vision006_CloudCrownKeepsItsEntireCombatEnvelopeClear()
        {
            var main=EditorSceneManager.OpenScene(Main);
            var root=main.GetRootGameObjects().Single(g=>g.name=="Chapter Nine - Crown Above the Clouds");
            Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var b=r.bounds;
                if(r.name=="Continuous cloud earth") {Assert.That(b.max.y,Is.LessThan(-10));continue;}
                if(r.name.Contains("cloud banks"))
                {
                    foreach(var p in r.GetComponent<MeshFilter>().sharedMesh.vertices)
                    {var v=r.transform.TransformPoint(p);Assert.That(Mathf.Abs(v.x)>50 || Mathf.Abs(v.z)>50,Is.True);}
                    continue;
                }
                Assert.That(b.max.x<=-50 || b.min.x>=50 || b.max.z<=-50 || b.min.z>=50,Is.True,r.name);
                Assert.That(r.forceMeshLod,Is.Zero);
            }
            Assert.That(root.transform.Cast<Transform>().Count(t=>t.name.Contains("gallery") || t.name.Contains("palace") || t.name.Contains("cloister") || t.name.Contains("gate")),Is.EqualTo(4));
        }

        [Test] public void Vision006_BakedLightingHasUsableMasksAndProbeData()
        {
            var main=EditorSceneManager.OpenScene(Main);
            var lighting=main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CelLevelLighting>()).Single();
            Assert.That(lighting.GetComponent<BakedShadowLighting>()?.bakedProbes,Is.Not.Null);
            Assert.That(LightmapSettings.lightmaps.Any(l=>l.shadowMask!=null),Is.True);
            var sub=EditorSceneManager.OpenScene(Sub,OpenSceneMode.Additive);
            var floor=sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>()).Single(r=>r.name=="Continuous Convex Floor");
            foreach(var m in floor.sharedMaterials)
            {
                Assert.That(m.GetFloat("_UseBakedShadowMask"),Is.EqualTo(1));Assert.That(m.GetTexture("_BakedShadowMask"),Is.Not.Null);
                Assert.That(m.GetFloat("_CameraCutaway"),Is.Zero);Assert.That(m.GetTag("CrowdPunchSourceMaterial",false,""),Does.Contain("ChapterNineCrown"));
            }
        }

        [Test] public void Finale001_FourDifferentStationsKeepTheirOriginalOwnershipAndDynamicSkins()
        {
            var sub=EditorSceneManager.OpenScene(Sub);
            var anchors=sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<FinaleAnchorAuthoring>(true)).ToArray();
            Assert.That(anchors.Length,Is.EqualTo(4));Assert.That(anchors.Select(a=>a.boss).Distinct().Count(),Is.EqualTo(1));
            Assert.That(anchors.Count(a=>a.GetComponent<ShellTargetAuthoring>()!=null),Is.EqualTo(1));
            Assert.That(anchors.Count(a=>a.GetComponent<TrackObjectAuthoring>()!=null),Is.EqualTo(1));
            var cover=sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RotatingCoverAuthoring>()).Single();
            Assert.That(cover.GetComponentsInChildren<CoverPanelAuthoring>().Length,Is.EqualTo(CrowdPunch.Utilities.CoverGeometry.PanelCount));
            foreach(var a in anchors)foreach(var r in a.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled))
            {
                Assert.That(r.shadowCastingMode,Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(r.GetComponentsInParent<Transform>(true).All(t=>(GameObjectUtility.GetStaticEditorFlags(t.gameObject)&StaticEditorFlags.ContributeGI)==0),Is.True,r.name);
                foreach(var m in r.sharedMaterials)
                {Assert.That(AssetDatabase.GetAssetPath(m),Does.Contain("ChapterNineCrown"));Assert.That(m.GetFloat("_CameraCutaway"),Is.Zero);Assert.That(m.GetFloat("_ObjectSpacePaint"),Is.EqualTo(1));}
            }
        }

        [Test] public void Finale002_RailAndShellUseCompletePhysicalSkinsAndFourFaceProgress()
        {
            var sub=EditorSceneManager.OpenScene(Sub);
            var roots=sub.GetRootGameObjects();
            var track=roots.SelectMany(g=>g.GetComponentsInChildren<TrackObjectAuthoring>()).Single();
            var cart=track.transform.Find("Royal anchor transfer trolley");Assert.That(cart,Is.Not.Null);
            Assert.That(cart.GetComponentsInChildren<MeshRenderer>().Count(r=>r.name=="Low guide roller"),Is.EqualTo(4));
            Assert.That(cart.GetComponentsInChildren<Collider>(),Is.Empty);
            var shell=roots.SelectMany(g=>g.GetComponentsInChildren<ShellTargetAuthoring>()).Single();
            var pieces=shell.GetComponentsInChildren<BarricadeVisualAuthoring>().Where(v=>v.GetComponent<MeshRenderer>().enabled).ToArray();
            Assert.That(pieces.Count(p=>p.shellVisual && !p.shellCore && p.crackStage==0),Is.GreaterThan(20));
            Assert.That(pieces.Count(p=>p.shellCore && p.crackStage>0),Is.EqualTo(8));
            foreach(var gate in roots.SelectMany(g=>g.GetComponentsInChildren<FinaleAnchorAuthoring>()).Where(a=>a.GetComponent<ShellTargetAuthoring>()==null && a.GetComponent<TrackObjectAuthoring>()==null))
                Assert.That(gate.GetComponentsInChildren<BarricadeVisualAuthoring>().Count(v=>v.name=="Branching anchor fracture"),Is.EqualTo(8));
        }
    }
}
