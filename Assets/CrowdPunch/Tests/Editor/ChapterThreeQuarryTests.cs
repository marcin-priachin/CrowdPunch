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
    public sealed class ChapterThreeQuarryTests
    {
        private SceneSetup[] setup;
        [SetUp] public void SaveSetup()=>setup=EditorSceneManager.GetSceneManagerSetup();
        [TearDown] public void RestoreSetup()
        {
            if(setup.Any(s=>s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }

        [TestCase(21)] [TestCase(22)] [TestCase(23)] [TestCase(24)] [TestCase(25)]
        [TestCase(26)] [TestCase(27)] [TestCase(28)] [TestCase(29)] [TestCase(30)]
        public void Vision004_QuarrySurroundsAreNoncollidingAndGroundRemainsBelowEveryFloor(int stage)
        {
            var main=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}.unity");
            var root=main.GetRootGameObjects().Single(g=>g.name=="Chapter Three - Highland Quarry and Aqueduct");
            Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
            var ground=root.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name=="Continuous quarry gravel");
            Assert.That(ground.sharedMaterial.GetFloat("_CameraCutaway"),Is.Zero);
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                Assert.That(float.IsNaN(r.bounds.extents.sqrMagnitude)||float.IsInfinity(r.bounds.extents.sqrMagnitude),Is.False,r.name);
                Assert.That(r.forceMeshLod,Is.Zero,r.name);
                Assert.That(r.sharedMaterial.shader.name,Is.EqualTo("CrowdPunch/Cel/Painted Foundry"));
                if(r!=ground)Assert.That(r.sharedMaterial.GetFloat("_CameraCutaway"),Is.EqualTo(1),r.name);
            }
            var sub=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}/Campaign_{stage:00} Sub Scene.unity",OpenSceneMode.Additive);
            foreach(var r in sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)))
            {
                if(r.name=="Continuous Convex Floor"||r.name=="Continuous Round Floor")
                    Assert.That(ground.bounds.max.y,Is.LessThanOrEqualTo(r.bounds.max.y-.119f));
                foreach(var m in r.sharedMaterials.Where(m=>m!=null && m.HasProperty("_CameraCutaway")))
                    Assert.That(m.GetFloat("_CameraCutaway"),Is.Zero,"Gameplay objects must remain visible: "+r.name);
            }
        }

        [TestCase(21)] [TestCase(22)] [TestCase(23)] [TestCase(24)] [TestCase(25)]
        [TestCase(26)] [TestCase(27)] [TestCase(28)] [TestCase(29)] [TestCase(30)]
        public void Vision006_SavedBakeHasMasksProbesAndExplicitEcsFloorReceivers(int stage)
        {
            var main=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}.unity");
            var policy=main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BakedShadowLighting>()).Single();
            Assert.That(policy.bakedProbes,Is.Not.Null);
            Assert.That(policy.bakedProbes.count,Is.GreaterThan(0));
            Assert.That(LightmapSettings.lightmaps.Length,Is.GreaterThan(0));
            foreach(var map in LightmapSettings.lightmaps)Assert.That(map.shadowMask,Is.Not.Null);
            var sub=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}/Campaign_{stage:00} Sub Scene.unity",OpenSceneMode.Additive);
            foreach(var floor in sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>()).Where(r=>r.name=="Continuous Convex Floor"||r.name=="Continuous Round Floor"))
            {
                Assert.That(floor.sharedMaterial.GetFloat("_UseBakedShadowMask"),Is.EqualTo(1));
                Assert.That(floor.sharedMaterial.GetTexture("_BakedShadowMask"),Is.Not.Null);
                Assert.That(floor.GetComponent<MeshFilter>().sharedMesh.uv2.Length,Is.EqualTo(floor.GetComponent<MeshFilter>().sharedMesh.vertexCount));
                string source=floor.sharedMaterial.GetTag("CrowdPunchSourceMaterial",false,"");
                Assert.That(source,Is.EqualTo("Assets/CrowdPunch/Data/ChapterThreeQuarry/Materials/Court.mat"));
            }
            foreach(var cue in sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<VillageExitCueAuthoring>(true)))
                Assert.That(GameObjectUtility.GetStaticEditorFlags(cue.gameObject).HasFlag(StaticEditorFlags.ContributeGI),Is.False);
        }

        [TestCase(21)] [TestCase(22)] [TestCase(23)] [TestCase(24)] [TestCase(25)]
        [TestCase(26)] [TestCase(27)] [TestCase(28)] [TestCase(29)] [TestCase(30)]
        public void Vision005_CliffTrianglesCloseAllHorizonsFromCourtEdgesAndCorners(int stage)
        {
            var main=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}.unity");
            var root=main.GetRootGameObjects().Single(g=>g.name=="Chapter Three - Highland Quarry and Aqueduct");
            var cliff=root.GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="Continuous quarry escarpment");
            var vertices=cliff.sharedMesh.vertices.Select(cliff.transform.TransformPoint).ToArray();var indices=cliff.sharedMesh.triangles;
            var sub=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}/Campaign_{stage:00} Sub Scene.unity",OpenSceneMode.Additive);
            var floors=sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>()).Where(r=>r.name.Contains("Continuous")&&r.name.Contains("Floor")).ToArray();
            var court=floors[0].bounds;foreach(var f in floors.Skip(1))court.Encapsulate(f.bounds);
            foreach(float x in new[]{-.85f,0,.85f})foreach(float z in new[]{-.85f,0,.85f})foreach(float height in new[]{3f,8f})
            for(int angle=0;angle<360;angle+=10)
            {
                var origin=new Vector3(court.center.x+x*court.extents.x,court.max.y+height,court.center.z+z*court.extents.z);
                var direction=Quaternion.Euler(0,angle,0)*Vector3.forward;bool hit=false;
                for(int i=0;i<indices.Length && !hit;i+=3)
                {
                    var a=vertices[indices[i]];var edge1=vertices[indices[i+1]]-a;var edge2=vertices[indices[i+2]]-a;
                    var p=Vector3.Cross(direction,edge2);float determinant=Vector3.Dot(edge1,p);
                    if(determinant<.00001f)continue; // Back faces cannot close a rendered horizon.
                    var delta=origin-a;float u=Vector3.Dot(delta,p)/determinant;if(u<0||u>1)continue;
                    var q=Vector3.Cross(delta,edge1);float v=Vector3.Dot(direction,q)/determinant;if(v<0||u+v>1)continue;
                    float distance=Vector3.Dot(edge2,q)/determinant;hit=distance>0 && distance<250;
                }
                Assert.That(hit,Is.True,$"Uncovered quarry view: stage {stage}, {origin}, orbit {angle}");
            }
        }
    }
}
