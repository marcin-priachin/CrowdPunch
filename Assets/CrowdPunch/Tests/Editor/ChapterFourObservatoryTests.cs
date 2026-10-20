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
    public sealed class ChapterFourObservatoryTests
    {
        private SceneSetup[] setup;
        [SetUp] public void SaveSetup()=>setup=EditorSceneManager.GetSceneManagerSetup();
        [TearDown] public void RestoreSetup()
        {
            if(setup.Any(s=>s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }

        [TestCase(31)] [TestCase(32)] [TestCase(33)] [TestCase(34)] [TestCase(35)]
        [TestCase(36)] [TestCase(37)] [TestCase(38)] [TestCase(39)] [TestCase(40)]
        public void Vision005_HazeKeepsTheSameOffsetFromActualFloorEdgesOnGpu(int stage)
        {
            var main=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}.unity");
            var lighting=main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CelLevelLighting>()).Single();
            Assert.That(lighting.hazeArenaEdges.Length,Is.InRange(3,CelLevelLighting.MaxHazeEdges));
            var shader=ShaderUtil.CreateShaderAsset(@"Shader ""Hidden/CrowdPunch/HazeDistanceTest"" { SubShader { Pass {
                ZTest Always ZWrite Off Cull Off
                HLSLPROGRAM
                #pragma vertex vert_img
                #pragma fragment frag
                #include ""UnityCG.cginc""
                #include ""Assets/CrowdPunch/Shaders/Cel/ArenaSceneryHaze.hlsl""
                float4 _SamplePosition;
                float4 frag(v2f_img input) : SV_Target {
                    return float4(ArenaSceneryDistance(_SamplePosition.xz),
                        ApplyArenaSceneryHaze(0,_SamplePosition.xyz,2).r,0,1);
                }
                ENDHLSL
            } } }");
            var material=new Material(shader);
            var target=RenderTexture.GetTemporary(1,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            var pixel=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
            var previousTarget=RenderTexture.active;
            var previousColor=Shader.GetGlobalVector("_CrowdPunchSceneryHazeColor");
            var previousBounds=Shader.GetGlobalVector("_CrowdPunchSceneryHazeBounds");
            var previousSettings=Shader.GetGlobalVector("_CrowdPunchSceneryHazeSettings");
            var previousEdges=Shader.GetGlobalVectorArray("_CrowdPunchSceneryHazeEdges");
            int previousCount=Shader.GetGlobalInt("_CrowdPunchSceneryHazeEdgeCount");
            try
            {
                lighting.Apply();
                Shader.SetGlobalColor("_CrowdPunchSceneryHazeColor",Color.white);
                Color Sample(Vector2 p)
                {
                    material.SetVector("_SamplePosition",new Vector4(p.x,lighting.hazeGroundHeight,p.y,0));
                    Graphics.Blit(null,target,material);
                    RenderTexture.active=target;pixel.ReadPixels(new Rect(0,0,1,1),0,0);pixel.Apply();
                    return pixel.GetPixel(0,0);
                }
                int checkedEdges=0;
                foreach(var edge in lighting.hazeArenaEdges)
                {
                    var a=new Vector2(edge.x,edge.y);var b=new Vector2(edge.z,edge.w);
                    var middle=(a+b)*.5f;var direction=b-a;
                    var outward=new Vector2(direction.y,-direction.x).normalized;
                    Assert.That(Sample(middle).r,Is.EqualTo(0).Within(.002f),"Arena border must remain clear.");
                    // Support edges have no neighbouring room nearer than the sampled edge.
                    if(lighting.hazeArenaEdges.Any(e=>Vector2.Dot(new Vector2(e.x,e.y)-middle,outward)>.002f))continue;
                    foreach(float distance in new[]{1f,3f,6f})
                    {
                        var sample=Sample(middle+outward*distance);
                        Assert.That(sample.r,Is.EqualTo(distance).Within(.002f),$"Stage {stage}, edge {edge}");
                        if(distance==1)Assert.That(sample.g,Is.EqualTo(0).Within(.002f));
                        if(distance==6)Assert.That(sample.g,Is.EqualTo(1).Within(.002f),"Exterior ground must be opaque by 6m.");
                    }
                    checkedEdges++;
                }
                Assert.That(checkedEdges,Is.GreaterThanOrEqualTo(3));
                Assert.That(ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),Is.Empty);
            }
            finally
            {
                Shader.SetGlobalVector("_CrowdPunchSceneryHazeColor",previousColor);
                Shader.SetGlobalVector("_CrowdPunchSceneryHazeBounds",previousBounds);
                Shader.SetGlobalVector("_CrowdPunchSceneryHazeSettings",previousSettings);
                Shader.SetGlobalVectorArray("_CrowdPunchSceneryHazeEdges",previousEdges.Length>0 ? previousEdges : new Vector4[CelLevelLighting.MaxHazeEdges]);
                Shader.SetGlobalInt("_CrowdPunchSceneryHazeEdgeCount",previousCount);
                RenderTexture.active=previousTarget;RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(pixel);Object.DestroyImmediate(material);Object.DestroyImmediate(shader);
            }
        }

        [TestCase(31)] [TestCase(32)] [TestCase(33)] [TestCase(34)] [TestCase(35)]
        [TestCase(36)] [TestCase(37)] [TestCase(38)] [TestCase(39)] [TestCase(40)]
        public void Vision005_SceneryHazeProtectsTheEntireArenaAndExcludesGameplayMaterials(int stage)
        {
            var main=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}.unity");
            var lighting=main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CelLevelLighting>()).Single();
            Assert.That(lighting.overrideAtmosphere && lighting.sceneryOnlyHaze,Is.True);
            Assert.That(lighting.skybox.shader.name,Is.EqualTo("CrowdPunch/Cel/Painted Haze Sky"));
            Assert.That(((Vector4)lighting.skybox.GetColor("_Horizon")-(Vector4)lighting.haze).sqrMagnitude,Is.LessThan(1e-8f));
            var root=main.GetRootGameObjects().Single(g=>g.name=="Chapter Four - Overgrown Observatory");
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
            foreach(var material in renderer.sharedMaterials)
                Assert.That(material.GetFloat("_SceneryHaze"),Is.EqualTo(renderer.name=="Continuous observatory earth" ? 2 : 1),renderer.name);
            var sub=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}/Campaign_{stage:00} Sub Scene.unity",OpenSceneMode.Additive);
            var floors=sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>())
                .Where(r=>r.name=="Continuous Convex Floor" || r.name=="Continuous Round Floor").ToArray();
            Assert.That(lighting.hazeGroundHeight,Is.EqualTo(floors.Min(r=>r.bounds.max.y)).Within(.001f),"The fog bank must follow the lowest arena floor.");
            foreach(var renderer in sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)))
            {
                if(renderer.name=="Continuous Convex Floor" || renderer.name=="Continuous Round Floor")
                {
                    var b=renderer.bounds;var envelope=lighting.hazeArenaBounds;
                    Assert.That(b.min.x,Is.GreaterThanOrEqualTo(envelope.x-envelope.z-.001f));
                    Assert.That(b.max.x,Is.LessThanOrEqualTo(envelope.x+envelope.z+.001f));
                    Assert.That(b.min.z,Is.GreaterThanOrEqualTo(envelope.y-envelope.w-.001f));
                    Assert.That(b.max.z,Is.LessThanOrEqualTo(envelope.y+envelope.w+.001f));
                }
                foreach(var material in renderer.sharedMaterials.Where(m=>m!=null && m.HasProperty("_SceneryHaze")))
                    Assert.That(material.GetFloat("_SceneryHaze"),Is.Zero,"Fog must exclude gameplay: "+renderer.name);
            }
        }

        [TestCase(31)] [TestCase(32)] [TestCase(33)] [TestCase(34)] [TestCase(35)]
        [TestCase(36)] [TestCase(37)] [TestCase(38)] [TestCase(39)] [TestCase(40)]
        public void Vision004_ObservatorySurroundsAreNoncollidingAndGroundRemainsBelowEveryFloor(int stage)
        {
            var main=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}.unity");
            var root=main.GetRootGameObjects().Single(g=>g.name=="Chapter Four - Overgrown Observatory");
            Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
            var ground=root.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name=="Continuous observatory earth");
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

        [TestCase(31)] [TestCase(32)] [TestCase(33)] [TestCase(34)] [TestCase(35)]
        [TestCase(36)] [TestCase(37)] [TestCase(38)] [TestCase(39)] [TestCase(40)]
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
                Assert.That(source,Is.EqualTo("Assets/CrowdPunch/Data/ChapterFourObservatory/Materials/Court.mat"));
            }
            foreach(var cue in sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<VillageExitCueAuthoring>(true)))
                Assert.That(GameObjectUtility.GetStaticEditorFlags(cue.gameObject).HasFlag(StaticEditorFlags.ContributeGI),Is.False);
        }

        [TestCase(31)] [TestCase(32)] [TestCase(33)] [TestCase(34)] [TestCase(35)]
        [TestCase(36)] [TestCase(37)] [TestCase(38)] [TestCase(39)] [TestCase(40)]
        public void Vision005_GardenTrianglesCloseAllHorizonsFromCourtEdgesAndCorners(int stage)
        {
            var main=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}.unity");
            var root=main.GetRootGameObjects().Single(g=>g.name=="Chapter Four - Overgrown Observatory");
            var cliff=root.GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="Continuous wooded garden horizon");
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
        [TestCase(31)] [TestCase(32)] [TestCase(33)] [TestCase(34)] [TestCase(35)]
        [TestCase(36)] [TestCase(37)] [TestCase(38)] [TestCase(39)] [TestCase(40)]
        public void Vision004_RotatedCloistersRetainFullArchitecturalModuleScale(int stage)
        {
            var scene=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage}.unity");
            var root=scene.GetRootGameObjects().Single(g=>g.name=="Chapter Four - Overgrown Observatory");
            int count=0;
            foreach(var arcade in root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Root-framed survey cloister"))
            foreach(var module in arcade.Cast<Transform>().Where(t=>t.name.StartsWith("Wall_Arch")))
            {
                var points=module.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(v=>arcade.InverseTransformPoint(f.transform.TransformPoint(v)))).ToArray();
                var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points.Skip(1))bounds.Encapsulate(p);
                Assert.That(bounds.size.x,Is.InRange(4f,5.66f),"Shrunken bay under rotated parent: "+arcade.localEulerAngles);
                Assert.That(bounds.size.y,Is.InRange(4f,6.51f));
                Assert.That(Mathf.Abs(bounds.min.y-.35f),Is.LessThan(.03f),"Module must meet its foundation");
                Assert.That(module.localScale.x,Is.EqualTo(module.localScale.y).Within(.0001f));
                Assert.That(module.localScale.x,Is.EqualTo(module.localScale.z).Within(.0001f));
                count++;
            }
            Assert.That(count,Is.GreaterThan(10));
        }

    }
}


