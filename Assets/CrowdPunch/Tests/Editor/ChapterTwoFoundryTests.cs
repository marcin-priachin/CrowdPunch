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
        [TearDown] public void RestoreSetup()=>EditorSceneManager.RestoreSceneManagerSetup(setup);

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
