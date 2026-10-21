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
    public sealed class ChapterSixCanyonTests
    {
        private SceneSetup[] setup;
        [SetUp] public void SaveSetup()=>setup=EditorSceneManager.GetSceneManagerSetup();
        [TearDown] public void RestoreSetup()
        {
            if(setup.Any(s=>s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }

        [TestCase(51)] [TestCase(52)] [TestCase(53)] [TestCase(54)] [TestCase(55)]
        [TestCase(56)] [TestCase(57)] [TestCase(58)] [TestCase(59)] [TestCase(60)]
        public void Vision006_BakedScenesHaveNoDecorativeCollisionAndKeepDynamicCuesVisible(int stage)
        {
            var main=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage}.unity");
            var root=main.GetRootGameObjects().Single(g=>g.name=="Chapter Six - Sunscarred Canyon Outpost");
            Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
            var lighting=main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CelLevelLighting>()).Single();
            Assert.That(lighting.GetComponent<BakedShadowLighting>(),Is.Not.Null);
            Assert.That(lighting.sceneryOnlyHaze,Is.False,"Canyon uses arena-scaled dry distance haze.");
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
            foreach(var patch in sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GroundHazardAuthoring>(true)))
                Assert.That(patch.GetComponent<GroundHazardAppearanceAuthoring>().style,Is.EqualTo(GroundHazardStyle.Canyon));
        }

        [TestCase(51)] [TestCase(52)] [TestCase(53)] [TestCase(54)] [TestCase(55)]
        [TestCase(56)] [TestCase(57)] [TestCase(58)] [TestCase(59)] [TestCase(60)]
        public void Vision004_StoreWallJointsCloseAndAwningLineworkHasDepthClearance(int stage)
        {
            var scene=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage}.unity");
            var root=scene.GetRootGameObjects().Single(g=>g.name=="Chapter Six - Sunscarred Canyon Outpost");
            var canvases=root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Faded blue canvas awning").ToArray();
            Assert.That(canvases,Is.Not.Empty);
            foreach(var canvas in canvases)
            {
                var front=canvas.parent.GetComponentsInChildren<Transform>().First(t=>t.name=="Door frontage pier");
                var rear=canvas.parent.Find("Closed rear facade");
                foreach(var wing in canvas.parent.GetComponentsInChildren<Transform>().Where(t=>t.name=="Returning sandstone wing"))
                {
                    Assert.That(wing.localPosition.z-wing.localScale.z*.5f,Is.EqualTo(front.localPosition.z+front.localScale.z*.5f).Within(.00001f));
                    Assert.That(wing.localPosition.z+wing.localScale.z*.5f,Is.EqualTo(rear.localPosition.z-rear.localScale.z*.5f).Within(.00001f));
                }
                var seams=canvas.parent.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Drawn canvas seam").ToArray();
                Assert.That(seams.Length,Is.EqualTo(4));
                foreach(var seam in seams)
                foreach(var vertex in seam.sharedMesh.vertices)
                {
                    var local=canvas.InverseTransformPoint(seam.transform.TransformPoint(vertex));
                    float clearance=(local.y-.5f)*canvas.lossyScale.y;
                    Assert.That(clearance,Is.GreaterThan(.01f),"Near-coplanar strips flicker against the canvas.");
                    Assert.That(Mathf.Abs(local.x),Is.LessThan(.5f));
                    Assert.That(Mathf.Abs(local.z),Is.LessThan(.5f));
                }
            }
        }

        [TestCase(51)] [TestCase(52)] [TestCase(53)] [TestCase(54)] [TestCase(55)]
        [TestCase(56)] [TestCase(57)] [TestCase(58)] [TestCase(59)] [TestCase(60)]
        public void Vision006_SuppliesClearBuildingsYardWallsAndEachOther(int stage)
        {
            var scene=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage}.unity");
            var root=scene.GetRootGameObjects().Single(g=>g.name=="Chapter Six - Sunscarred Canyon Outpost").transform;
            var props=root.GetComponentsInChildren<Transform>().Where(t=>
                (t.parent!=null && (t.parent==root || t.parent.name=="Caravan service store" || t.parent.name=="Wind-eroded outpost")) &&
                (t.name=="Caravan supply chest" || t.name=="cactus_short" || t.name=="cactus_tall")).ToArray();
            Assert.That(props,Is.Not.Empty);
            var propRenderers=props.SelectMany(t=>t.GetComponentsInChildren<Renderer>()).ToArray();
            var architecture=root.GetComponentsInChildren<Renderer>().Where(r=>!propRenderers.Contains(r) &&
                r.name!="Chest iron straps" && r.name!="Continuous canyon earth" && r.name!="Continuous canyon mesa rim").ToArray();
            foreach(var prop in props)
            {
                var renderers=prop.GetComponentsInChildren<Renderer>();
                var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                bounds.Expand(.38f);
                foreach(var fixedObject in architecture)
                    Assert.That(bounds.Intersects(fixedObject.bounds),Is.False,$"{prop.name} intersects {fixedObject.name} in {stage}");
                foreach(var other in props.Where(t=>t!=prop))
                foreach(var renderer in other.GetComponentsInChildren<Renderer>())
                    Assert.That(bounds.Intersects(renderer.bounds),Is.False,$"{prop.name} intersects another supply prop in {stage}");
                if(prop.name!="Caravan supply chest")continue;
                var straps=prop.parent.GetComponentsInChildren<Transform>().Where(t=>t.parent==prop.parent && t.name=="Chest iron straps" &&
                    Mathf.Abs(t.localPosition.z-prop.localPosition.z)<.01f && Mathf.Abs(Mathf.Abs(t.localPosition.x-prop.localPosition.x)-.32f)<.01f).ToArray();
                Assert.That(straps.Length,Is.EqualTo(2),"Crate straps must move with their chest.");
            }
        }

        [Test] public void Vision004_Level52FloorHasOneUpwardSurfaceInsideItsAuthoredOutline()
        {
            var scene=EditorSceneManager.OpenScene("Assets/CrowdPunch/Scenes/Campaign/Campaign_52/Campaign_52 Sub Scene.unity");
            var floor=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>()).Single(f=>f.name=="Continuous Convex Floor");
            var collider=floor.GetComponent<MeshCollider>();
            Assert.That(AssetDatabase.GetAssetPath(collider.sharedMesh),Is.EqualTo("Assets/CrowdPunch/Data/Campaign/Layouts/Campaign_52_Floor.asset"));
            Assert.That(floor.sharedMesh,Is.Not.SameAs(collider.sharedMesh));
            var source=collider.sharedMesh.vertices;int count=source.Length/2;float top=source[0].y;
            var outline=source.Take(count).Select(p=>new Vector2(p.x,p.z)).ToArray();
            var vertices=floor.sharedMesh.vertices;var indices=floor.sharedMesh.triangles;
            var surface=new System.Collections.Generic.List<Vector2[]>();float area=0,expected=0;
            for(int i=0;i<count;i++){var a=outline[i];var b=outline[(i+1)%count];expected+=a.x*b.y-b.x*a.y;}
            for(int i=0;i<indices.Length;i+=3)
            {
                var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];
                if(Mathf.Abs(a.y-top)>.0001f || Mathf.Abs(b.y-top)>.0001f || Mathf.Abs(c.y-top)>.0001f)continue;
                var cross=Vector3.Cross(b-a,c-a);
                Assert.That(cross.normalized.y,Is.GreaterThan(.999f),"Back-facing floor fans caused the dark segment.");
                foreach(int index in new[]{indices[i],indices[i+1],indices[i+2]})Assert.That(floor.sharedMesh.normals[index].y,Is.GreaterThan(.999f));
                area+=cross.y*.5f;surface.Add(new[]{new Vector2(a.x,a.z),new Vector2(b.x,b.z),new Vector2(c.x,c.z)});
            }
            Assert.That(surface.Count,Is.EqualTo(count-2));Assert.That(area,Is.EqualTo(Mathf.Abs(expected)*.5f).Within(.001f));
            float Cross(Vector2 a,Vector2 b,Vector2 p)=>(b.x-a.x)*(p.y-a.y)-(b.y-a.y)*(p.x-a.x);
            bool Inside(Vector2 p)
            {
                bool inside=false;
                for(int i=0,j=count-1;i<count;j=i++)if((outline[i].y>p.y)!=(outline[j].y>p.y) &&
                    p.x<(outline[j].x-outline[i].x)*(p.y-outline[i].y)/(outline[j].y-outline[i].y)+outline[i].x)inside=!inside;
                return inside;
            }
            for(float x=-19.73f;x<20;x+=.5f)for(float z=-20.19f;z<20;z+=.5f)
            {
                var p=new Vector2(x,z);int coverage=0;bool onEdge=false;
                foreach(var triangle in surface)
                {
                    float a=Cross(triangle[0],triangle[1],p),b=Cross(triangle[1],triangle[2],p),c=Cross(triangle[2],triangle[0],p);
                    if(Mathf.Abs(a)<.001f || Mathf.Abs(b)<.001f || Mathf.Abs(c)<.001f){onEdge=true;break;}
                    if(a<0 && b<0 && c<0)coverage++;
                }
                if(!onEdge)Assert.That(coverage,Is.EqualTo(Inside(p)?1:0),$"Floor overlap/hole at {p}");
            }
        }

        [Test] public void Protect001_ThePavingBoundaryMatchesTheExistingBreachZone()
        {
            var sub=EditorSceneManager.OpenScene("Assets/CrowdPunch/Scenes/Campaign/Campaign_57/Campaign_57 Sub Scene.unity");
            var objective=sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProtectedPointAuthoring>()).Single();
            var root=sub.GetRootGameObjects().Single(g=>g.name=="Canyon protected supply inlay");
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
            foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/CrowdPunch/Data/ChapterSixCanyon/Textures"}))
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
