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
    public sealed class ChapterFiveNecropolisTests
    {
        private SceneSetup[] setup;
        [SetUp] public void SaveSetup()=>setup=EditorSceneManager.GetSceneManagerSetup();
        [TearDown] public void RestoreSetup()
        {
            if(setup.Any(s=>s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }

        [TestCase(41)] [TestCase(42)] [TestCase(43)] [TestCase(44)] [TestCase(45)]
        [TestCase(46)] [TestCase(47)] [TestCase(48)] [TestCase(49)] [TestCase(50)]
        public void Vision006_BakedScenesHaveNoDecorativeCollisionAndKeepDynamicCuesVisible(int stage)
        {
            var main=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage}.unity");
            var root=main.GetRootGameObjects().Single(g=>g.name=="Chapter Five - Twilight Necropolis");
            Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
            var lighting=main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CelLevelLighting>()).Single();
            Assert.That(lighting.GetComponent<BakedShadowLighting>(),Is.Not.Null);
            Assert.That(lighting.sceneryOnlyHaze,Is.True,"Fog must keep combat inside the floor boundary clear.");
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                Assert.That(GameObjectUtility.GetStaticEditorFlags(renderer.gameObject).HasFlag(StaticEditorFlags.ContributeGI),Is.True);
                Assert.That(renderer.lightmapIndex,Is.InRange(0,253));
                Assert.That(renderer.GetComponent<MeshFilter>().sharedMesh.uv2,Is.Not.Empty);
            }
            var sub=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage}/Campaign_{stage} Sub Scene.unity",OpenSceneMode.Additive);
            var renderers=sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            var floors=renderers.Where(r=>r.name=="Continuous Convex Floor" || r.name=="Continuous Round Floor").ToArray();
            Assert.That(lighting.hazeArenaEdges,Is.EqualTo(CrowdPunch.Editor.SceneryHazeFloorOutline.Build(floors)),"Fog must follow every authored floor, including disconnected rooms.");
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
            foreach(var patch in sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GroundHazardAuthoring>(true)))
                Assert.That(patch.GetComponent<GroundHazardAppearanceAuthoring>().style,Is.EqualTo(GroundHazardStyle.Necropolis));
        }

        [Test] public void Vision005_DistantSceneryIsADarkSilhouetteWithoutTintingGameplayOnGpu()
        {
            var shader=ShaderUtil.CreateShaderAsset(@"Shader ""Hidden/CrowdPunch/NecropolisSilhouetteTest"" { SubShader { Pass {
                ZTest Always ZWrite Off Cull Off
                HLSLPROGRAM
                #pragma vertex vert_img
                #pragma fragment frag
                #include ""UnityCG.cginc""
                #include ""Assets/CrowdPunch/Shaders/Cel/ArenaSceneryHaze.hlsl""
                float4 _SamplePosition, _InputColor;
                float _Role;
                float4 frag(v2f_img input) : SV_Target {
                    return float4(ApplyArenaSceneryHaze(_InputColor.rgb,_SamplePosition.xyz,_Role),1);
                }
                ENDHLSL
            } } }");
            var material=new Material(shader);
            var target=RenderTexture.GetTemporary(1,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            var pixel=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
            var previousTarget=RenderTexture.active;
            var previousBounds=Shader.GetGlobalVector("_CrowdPunchSceneryHazeBounds");
            var previousColor=Shader.GetGlobalVector("_CrowdPunchSceneryHazeColor");
            var previousSettings=Shader.GetGlobalVector("_CrowdPunchSceneryHazeSettings");
            int previousCount=Shader.GetGlobalInt("_CrowdPunchSceneryHazeEdgeCount");
            try
            {
                Shader.SetGlobalVector("_CrowdPunchSceneryHazeBounds",new Vector4(0,0,10,10));
                Shader.SetGlobalInt("_CrowdPunchSceneryHazeEdgeCount",0);
                Shader.SetGlobalColor("_CrowdPunchSceneryHazeColor",Color.white);
                Shader.SetGlobalVector("_CrowdPunchSceneryHazeSettings",new Vector4(1,0,4.5f,72));
                Color Sample(Vector3 position,Color color,float role)
                {
                    material.SetVector("_SamplePosition",position);material.SetColor("_InputColor",color);material.SetFloat("_Role",role);
                    Graphics.Blit(null,target,material);RenderTexture.active=target;
                    pixel.ReadPixels(new Rect(0,0,1,1),0,0);pixel.Apply();return pixel.GetPixel(0,0);
                }
                Assert.That(Sample(Vector3.zero,Color.red,3).r,Is.EqualTo(1).Within(.002));
                Assert.That(Sample(new Vector3(90,0,0),Color.red,0).g,Is.Zero.Within(.002));
                foreach(float height in new[]{0f,10f})
                {
                    var far=new Vector3(90,height,0);
                    var pale=Sample(far,Color.white,3);var dark=Sample(far,Color.black,3);
                    Assert.That(pale.r,Is.LessThan(.3f),"Distant ground and objects must stay dark against the fog.");
                    Assert.That(pale.r,Is.EqualTo(dark.r).Within(.002),"Distant detail must collapse into a flat silhouette.");
                    Assert.That(Sample(far,Color.black,1).r,Is.EqualTo(1).Within(.002),"Existing chapter fog must still dissolve far scenery.");
                }
                Assert.That(ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),Is.Empty);
            }
            finally
            {
                Shader.SetGlobalVector("_CrowdPunchSceneryHazeBounds",previousBounds);
                Shader.SetGlobalVector("_CrowdPunchSceneryHazeColor",previousColor);
                Shader.SetGlobalVector("_CrowdPunchSceneryHazeSettings",previousSettings);
                Shader.SetGlobalInt("_CrowdPunchSceneryHazeEdgeCount",previousCount);
                RenderTexture.active=previousTarget;RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(pixel);Object.DestroyImmediate(material);Object.DestroyImmediate(shader);
            }
        }

        [Test] public void Protect001_ThePavingBoundaryMatchesTheExistingBreachZone()
        {
            var sub=EditorSceneManager.OpenScene("Assets/CrowdPunch/Scenes/Campaign/Campaign_47/Campaign_47 Sub Scene.unity");
            var objective=sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProtectedPointAuthoring>()).Single();
            var root=sub.GetRootGameObjects().Single(g=>g.name=="Necropolis protected supply inlay");
            Assert.That(root.GetComponentsInChildren<Collider>(),Is.Empty);
            var edges=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Exact protected")).ToArray();
            var bounds=edges[0].bounds;foreach(var edge in edges.Skip(1))bounds.Encapsulate(edge.bounds);
            Assert.That(bounds.center.x,Is.EqualTo(objective.zoneCenter.x).Within(.001));
            Assert.That(bounds.center.z,Is.EqualTo(objective.zoneCenter.y).Within(.001));
            Assert.That(bounds.size.x,Is.EqualTo(objective.zoneSize.x).Within(.001));
            Assert.That(bounds.size.z,Is.EqualTo(objective.zoneSize.y).Within(.001));
        }

        [Test] public void Vision006_OwnedTextureImportsRetainFullMipChains()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/CrowdPunch/Data/ChapterFiveNecropolis/Textures"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(texture.width,Is.LessThanOrEqualTo(2048));Assert.That(texture.mipmapCount,Is.GreaterThan(1));
                Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Trilinear));Assert.That(importer.anisoLevel,Is.EqualTo(4));
                Assert.That(importer.wrapMode,Is.EqualTo(TextureWrapMode.Repeat));
            }
        }
    }
}
