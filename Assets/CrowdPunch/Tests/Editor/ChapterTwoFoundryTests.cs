using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class ChapterTwoFoundryTests
    {
        private SceneSetup[] setup;
        [SetUp] public void SaveSetup()=>setup=EditorSceneManager.GetSceneManagerSetup();
        [TearDown]
        public void RestoreSetup()
        {
            if(setup.Any(scene=>scene.isLoaded && scene.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }

        [TestCase(11)] [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)]
        [TestCase(16)] [TestCase(17)] [TestCase(18)] [TestCase(19)] [TestCase(20)]
        public void Vision005_OrbitViewsHaveFactorySurroundingsAndPavingHasNoSharedTopFaces(int stage)
        {
            var main=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}.unity");
            var root=main.GetRootGameObjects().Single(g=>g.name=="Chapter Two - Emberworks Foundry");
            var sub=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}/Campaign_{stage:00} Sub Scene.unity",OpenSceneMode.Additive);
            var floor=sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>())
                .Single(r=>r.name=="Continuous Convex Floor" || r.name=="Continuous Round Floor").bounds;
            var renderers=root.GetComponentsInChildren<MeshRenderer>();
            var buildings=renderers.Where(r=>r.name=="Foundry brick workshop" || r.name=="Recessed continuous party wall").Select(r=>r.bounds).ToArray();
            // Sample from the centre, edges and corners at two heights. This catches
            // aligned expansion joints as well as missing sides of the district.
            foreach(float x in new[]{-.85f,0,.85f})foreach(float z in new[]{-.85f,0,.85f})
            foreach(float height in new[]{3f,8f})for(int angle=0;angle<360;angle+=5)
            {
                var origin=new Vector3(floor.center.x+floor.extents.x*x,floor.max.y+height,floor.center.z+floor.extents.z*z);
                var ray=new Ray(origin,Quaternion.Euler(0,angle,0)*Vector3.forward);
                Assert.That(buildings.Any(b=>b.IntersectRay(ray,out float distance) && distance<200),Is.True,
                    $"Exposed backdrop from {origin}, orbit {angle}");
            }
            var paving=renderers.Where(r=>r.name.Contains("paving") || r.name=="Rear loading concourse" || r.name=="Delivery loading apron").ToArray();
            for(int i=0;i<paving.Length;i++)for(int j=i+1;j<paving.Length;j++)
            {
                var a=paving[i].bounds;var b=paving[j].bounds;
                bool sharedTop=Mathf.Abs(a.max.y-b.max.y)<.001f &&
                    Mathf.Min(a.max.x,b.max.x)-Mathf.Max(a.min.x,b.min.x)>.005f &&
                    Mathf.Min(a.max.z,b.max.z)-Mathf.Max(a.min.z,b.min.z)>.005f;
                Assert.That(sharedTop,Is.False,paving[i].name+" / "+paving[j].name+": competing paving receivers");
            }
        }

        [Test]
        public void Vision006_ContactShadingUsesStableFullResolutionSampling()
        {
            var data=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Assets/CrowdPunch/Data/Presentation/CelRenderer.asset");
            var feature=data.rendererFeatures.Single(f=>f.GetType().Name=="ScreenSpaceAmbientOcclusion");
            var settings=new SerializedObject(feature);
            Assert.That(settings.FindProperty("m_Settings.AOMethod").enumDisplayNames[settings.FindProperty("m_Settings.AOMethod").enumValueIndex],Is.EqualTo("Interleaved Gradient"));
            Assert.That(settings.FindProperty("m_Settings.Downsample").boolValue,Is.False);
        }

        [TestCase(11)] [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)]
        [TestCase(16)] [TestCase(17)] [TestCase(18)] [TestCase(19)] [TestCase(20)]
        public void Vision005_FoundryEntrancesAndGlazingHaveClearance(int stage)
        {
            var scene=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}.unity");
            var root=scene.GetRootGameObjects().Single(g=>g.name=="Chapter Two - Emberworks Foundry");
            var renderers=root.GetComponentsInChildren<MeshRenderer>();
            var workshops=renderers.Where(r=>r.name=="Foundry brick workshop").ToArray();
            var roofs=renderers.Where(r=>r.name.Contains("roof") || r.name.Contains("awning")).ToArray();
            var piers=renderers.Where(r=>r.name=="Steel facade pier").ToArray();

            foreach(var workshop in workshops)
            {
                var door=workshop.transform.parent.Find("door-wide-closed")?.GetComponentInChildren<MeshRenderer>();
                if(door==null) continue;
                Assert.That(door.bounds.max.y,Is.LessThan(workshop.bounds.max.y-.25f),workshop.transform.position.ToString());
                var approach=door.bounds;
                var forward=workshop.transform.parent.forward;
                approach.Expand(new Vector3(.02f+.8f*Mathf.Abs(forward.x),0,.02f+.8f*Mathf.Abs(forward.z)));
                Assert.That(piers.Any(p=>Penetrates(approach,p.bounds)),Is.False,$"Blocked shutter at {door.bounds.center}");
            }

            foreach(var window in renderers.Where(r=>r.name=="High clerestory window"))
                Assert.That(roofs.Concat(piers).Any(r=>Penetrates(window.bounds,r.bounds)),Is.False,
                    $"Obstructed clerestory at {window.bounds.center}");

            foreach(var roof in roofs)
                Assert.That(workshops.Any(r=>r.transform.parent!=roof.transform.parent && Penetrates(r.bounds,roof.bounds)),
                    Is.False,$"{roof.name} cuts through masonry at {roof.bounds.center}");

            foreach(var bunker in renderers.Where(r=>r.name.StartsWith("Coal bunker")))
                Assert.That(renderers.Any(r=>r.name=="Connected process pipe" && Penetrates(r.bounds,bunker.bounds)),
                    Is.False,$"Process pipe cuts through {bunker.name} at {bunker.bounds.center}");

            foreach(var bridge in renderers.Where(r=>r.name=="Exit service bridge"))
                Assert.That(roofs.Any(r=>Penetrates(r.bounds,bridge.bounds)),Is.False,"Exit service bridge cuts into a roof");

            var freightProps=renderers.Where(r=>r.name.StartsWith("box-large") || r.name.StartsWith("crane")).ToArray();
            foreach(var siding in renderers.Where(r=>r.name.StartsWith("Freight siding")))
            {
                var clearance=siding.bounds;
                clearance.Expand(.05f);
                Assert.That(freightProps.Any(r=>Penetrates(r.bounds,clearance)),Is.False,
                    $"{siding.name} cuts through cargo or loading machinery at {siding.bounds.center}");
            }
        }

        private static bool Penetrates(Bounds a,Bounds b)
        {
            var overlap=Vector3.Min(a.max,b.max)-Vector3.Max(a.min,b.min);
            return overlap.x>.015f && overlap.y>.015f && overlap.z>.015f;
        }

        [TestCase(11)] [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)]
        [TestCase(16)] [TestCase(17)] [TestCase(18)] [TestCase(19)] [TestCase(20)]
        public void Vision004_ImportedPlanesHaveFinitePlacementsAndSceneryHasNoCollision(int stage)
        {
            var scene=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}.unity");
            var root=scene.GetRootGameObjects().Single(g=>g.name=="Chapter Two - Emberworks Foundry");
            Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
                foreach(float value in new[]{t.localPosition.x,t.localPosition.y,t.localPosition.z,t.localScale.x,t.localScale.y,t.localScale.z})
                    Assert.That(float.IsNaN(value)||float.IsInfinity(value),Is.False,t.name+": invalid planar-model fit");
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
                Assert.That(float.IsNaN(r.bounds.extents.sqrMagnitude)||float.IsInfinity(r.bounds.extents.sqrMagnitude),Is.False,r.name);
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                if(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CrowdPunch/Models/kenney_factory-kit_3.0/Models/FBX format/"+t.name+".fbx")==null)continue;
                Assert.That(t.localScale.x,Is.EqualTo(t.localScale.y).Within(.0001f),t.name+": preserve imported machine proportions");
                Assert.That(t.localScale.x,Is.EqualTo(t.localScale.z).Within(.0001f),t.name+": preserve circular pipe fittings");
            }

            // Vertex fog on one enormous quad incorrectly covers the nearby ground in haze.
            var ground=root.GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="Continuous exterior earth terrace");
            var vertices=ground.sharedMesh.vertices;var indices=ground.sharedMesh.triangles;
            for(int i=0;i<indices.Length;i+=3)
                for(int edge=0;edge<3;edge++)
                    Assert.That(Vector3.Distance(ground.transform.TransformPoint(vertices[indices[i+edge]]),
                        ground.transform.TransformPoint(vertices[indices[i+(edge+1)%3]])),Is.LessThan(20),"Ground fog interpolation needs local vertices.");
            var sub=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}/Campaign_{stage:00} Sub Scene.unity",OpenSceneMode.Additive);
            var floors=sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true))
                .Where(r=>r.name=="Continuous Convex Floor" || r.name=="Continuous Round Floor").ToArray();
            var court=floors[0].bounds;foreach(var floor in floors.Skip(1))court.Encapsulate(floor.bounds);
            foreach(var door in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("door-wide-closed")))
                Assert.That(door.bounds.max.x<court.min.x || door.bounds.min.x>court.max.x ||
                    door.bounds.max.z<court.min.z || door.bounds.min.z>court.max.z,Is.True,"Facade fit must not leave a rejected transform at the gameplay origin.");
        }
    }
}
