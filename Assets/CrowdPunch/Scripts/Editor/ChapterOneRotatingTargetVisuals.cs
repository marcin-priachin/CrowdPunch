using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Utilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace CrowdPunch.Editor
{
    // COVER-001/004, VISION-006: collider-aligned village machinery, presentation only.
    public static class ChapterOneRotatingTargetVisuals
    {
        private const string Folder="Assets/CrowdPunch/Data/ChapterOneVillage/RotatingTarget";
        private const string ScenePath="Assets/CrowdPunch/Scenes/Campaign/Campaign_07/Campaign_07 Sub Scene.unity";

        [MenuItem("Crowd Punch/Art Direction/Refresh Chapter One Rotating Target")]
        public static void RefreshSaved()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0,SceneManager.sceneCount)
                .Any(i=>SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Stop Play mode and save open scenes first.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene=EditorSceneManager.OpenScene(ScenePath);
                var cover=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RotatingCoverAuthoring>(true)).Single();
                Build(cover);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            }
            finally {EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }

        internal static void Build(RotatingCoverAuthoring cover)
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var blue=Material("Painted lagoon blue",new Color(.12f,.40f,.49f));
            var lightBlue=Material("Sun faded blue",new Color(.17f,.49f,.56f));
            var brass=Material("Warm brass",new Color(.94f,.63f,.18f));
            var iron=Material("Dark iron rivets",new Color(.10f,.17f,.19f));
            var cream=Material("Ivory target paint",new Color(.97f,.88f,.62f));
            var red=Material("Vermilion target paint",new Color(.83f,.23f,.12f));
            var timber=Material("Carved redwood",new Color(.49f,.23f,.11f));
            var stone=Material("Warm stone dais",new Color(.50f,.49f,.36f));
            var crack=Material("Split redwood",new Color(.12f,.065f,.035f));
            var mesh=ShieldMesh(false);var arrowMesh=ShieldMesh(true);
            foreach(var panel in cover.GetComponentsInChildren<CoverPanelAuthoring>(true))
            {
                bool edge=panel.index==0 || panel.index==CoverGeometry.PanelCount-1;
                panel.GetComponent<MeshFilter>().sharedMesh=panel.index%6==3?arrowMesh:mesh;
                var renderer=panel.GetComponent<MeshRenderer>();
                renderer.sharedMaterials=new[]{edge?cream:panel.index%3==0?lightBlue:blue,brass,iron};
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.forceMeshLod=0;
                GameObjectUtility.SetStaticEditorFlags(panel.gameObject,0);
                var geometry=new CrowdPunch.Components.RotatingCover {Radius=cover.settings.radius,Height=cover.settings.height,
                    Thickness=cover.settings.thickness,OpeningRadians=cover.settings.openingDegrees*Mathf.Deg2Rad};
                CoverGeometry.Panel(geometry,panel.index,out var position,out var rotation,out var size);
                panel.transform.localPosition=position;panel.transform.localRotation=rotation;panel.transform.localScale=size;
            }

            var target=cover.target;
            foreach(var piece in target.GetComponentsInChildren<BarricadeVisualAuthoring>(true)) Object.DestroyImmediate(piece.gameObject);
            int index=0;float height=target.size.y,width=target.size.x,depth=target.size.z;
            for(int tier=0;tier<3;tier++)
                TargetPiece(target,"Segmented redwood target",new Vector3(0,-height*.5f+(tier+.5f)*height/3,0),
                    new Vector3(width,height/3-.018f,depth),timber,index++);
            foreach(int side in new[]{-1,1})
                TargetPiece(target,"Brass target cap",new Vector3(0,side*(height*.5f-.10f),0),new Vector3(width+.08f,.20f,depth+.08f),brass,index++);
            for(int face=0;face<4;face++)
            {
                var turn=Quaternion.Euler(0,face*90,0);
                var normal=turn*Vector3.back;
                var center=new Vector3(0,.12f,0)+normal*(depth*.5f);
                var disks=new[]{(1.98f,.035f,brass),(1.77f,.075f,cream),(1.17f,.115f,red),(.48f,.155f,cream)};
                foreach(var (diameter,offset,material) in disks)
                {
                    var disk=TargetPiece(target,"Painted bullseye",center+normal*offset,new Vector3(diameter,.035f,diameter),material,index++,cylinder:true);
                    disk.transform.localRotation=turn*Quaternion.Euler(90,0,0);
                }
                for(int stage=1;stage<=2;stage++) for(int segment=0;segment<3;segment++)
                {
                    float y=stage==1?-.58f:.72f;
                    var position=turn*new Vector3((segment-1)*.63f,y+(segment%2)*.14f,-depth*.5f-.20f);
                    var split=TargetPiece(target,"Progressive timber split",position,new Vector3(.74f,.065f,.045f),crack,index++,stage:stage);
                    split.transform.localRotation=turn*Quaternion.Euler(0,0,segment%2==0?18:-18);
                }
            }

            foreach(var old in cover.gameObject.scene.GetRootGameObjects().Where(g=>g.name=="Central enclosure plinth" || g.name=="Village training turntable"))
                Object.DestroyImmediate(old);
            var dais=new GameObject("Village training turntable").transform;
            SceneManager.MoveGameObjectToScene(dais.gameObject,cover.gameObject.scene);
            dais.position=new Vector3(cover.transform.position.x,cover.transform.position.y-cover.settings.height*.5f,cover.transform.position.z);
            float radius=cover.settings.radius-cover.settings.thickness;
            FixedPiece(dais,"Stone walking exclusion base",new Vector3(0,.04f,0),new Vector3(radius*2,.065f,radius*2),stone,true);
            FixedPiece(dais,"Brass base rim",new Vector3(0,.085f,0),new Vector3(radius*2+.05f,.018f,radius*2+.05f),brass,true);
            FixedPiece(dais,"Inset blue turntable",new Vector3(0,.112f,0),new Vector3(radius*2-.27f,.018f,radius*2-.27f),blue,true);
            for(int i=0;i<16;i++)
            {
                var turn=Quaternion.Euler(0,i*360f/16,0);
                var cog=FixedPiece(dais,"Brass turntable tooth",turn*new Vector3(0,.16f,radius-.3f),new Vector3(.36f,.10f,.34f),brass);
                cog.transform.localRotation=turn;
            }
        }

        private static GameObject TargetPiece(BarricadeAuthoring target,string name,Vector3 position,Vector3 scale,Material material,int index,int stage=0,bool cylinder=false)
        {
            var go=Primitive(target.transform,name,position,scale,material,cylinder);
            go.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go,0);
            var visual=go.AddComponent<BarricadeVisualAuthoring>();visual.barricade=target;visual.crackStage=stage;
            var piece=go.AddComponent<VillageGatePieceAuthoring>();piece.kind=stage>0?1:0;piece.damageStage=stage;
            piece.seed=(index*37%101)/100f;piece.velocity=new Vector3((piece.seed-.5f)*5,3+piece.seed*2,2+piece.seed*2);
            piece.spin=new Vector3(2+piece.seed*3,1+piece.seed*4,2);piece.restingHeight=.1f;
            return go;
        }

        private static GameObject FixedPiece(Transform root,string name,Vector3 position,Vector3 scale,Material material,bool cylinder=false)
        {
            var go=Primitive(root,name,position,scale,material,cylinder);
            GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.ContributeGI|
                StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic|StaticEditorFlags.ReflectionProbeStatic);
            return go;
        }

        private static GameObject Primitive(Transform root,string name,Vector3 position,Vector3 scale,Material material,bool cylinder)
        {
            var go=GameObject.CreatePrimitive(cylinder?PrimitiveType.Cylinder:PrimitiveType.Cube);go.name=name;
            Object.DestroyImmediate(go.GetComponent<Collider>());go.transform.SetParent(root,false);
            go.transform.localPosition=position;go.transform.localScale=scale;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.forceMeshLod=0;
            return go;
        }

        private static Material Material(string name,Color color)
        {
            string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("CrowdPunch/Cel/Environment"));AssetDatabase.CreateAsset(material,path);}
            material.name=name;material.color=color;material.enableInstancing=true;material.SetFloat("_Smoothness",.05f);
            EditorUtility.SetDirty(material);return material;
        }

        private static Mesh ShieldMesh(bool arrow)
        {
            string path=Folder+(arrow?"/Braced shutter with direction.asset":"/Braced shutter.asset");
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var vertices=new List<Vector3>();var uv=new List<Vector2>();
            var triangles=new[]{new List<int>(),new List<int>(),new List<int>()};
            void Box(Vector3 center,Vector3 size,int material,float angle=0)
            {
                var turn=Quaternion.Euler(0,0,angle);
                var corners=new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),
                    new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)};
                int[][] faces={new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,4,7,3},new[]{1,2,6,5},new[]{3,7,6,2},new[]{0,1,5,4}};
                foreach(var face in faces)
                {
                    int start=vertices.Count;
                    foreach(int k in face) vertices.Add(center+turn*Vector3.Scale(corners[k]*.5f,size));
                    uv.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right});
                    triangles[material].AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                }
            }
            for(int plank=-1;plank<=1;plank++) Box(new Vector3(plank/3f,0,0),new Vector3(.328f,1,1),0);
            foreach(int face in new[]{-1,1})
            {
                foreach(int row in new[]{-1,1})
                {
                    Box(new Vector3(0,row*.41f,face*.52f),new Vector3(1,.07f,.07f),1);
                    foreach(int stud in new[]{-1,1}) Box(new Vector3(stud*.32f,row*.41f,face*.575f),new Vector3(.075f,.03f,.075f),2);
                }
                if(arrow)
                {
                    // A single stamped chevron stays legible after nonuniform scaling.
                    int start=vertices.Count;
                    vertices.AddRange(new[]{new Vector3(-.43f,.075f,face*.56f),new Vector3(.43f,0,face*.56f),
                        new Vector3(-.43f,-.075f,face*.56f),new Vector3(-.15f,0,face*.56f)});
                    uv.AddRange(new[]{Vector2.up,Vector2.one,Vector2.right,Vector2.zero});
                    triangles[1].AddRange(face<0?new[]{start,start+1,start+3,start+1,start+2,start+3}:
                        new[]{start,start+3,start+1,start+1,start+3,start+2});
                }
            }
            var mesh=new Mesh{name=arrow?"Directional brass shutter":"Brass bound timber shutter"};
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=3;
            for(int i=0;i<3;i++) mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;
        }
    }
}
