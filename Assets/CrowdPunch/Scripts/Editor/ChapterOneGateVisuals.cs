using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace CrowdPunch.Editor
{
    // BARRICADE-004 / VISION-006: replace presentation, preserving shared collision and durability.
    public static class ChapterOneGateVisuals
    {
        private const string Folder="Assets/CrowdPunch/Data/ChapterOneVillage/Gates";

        [MenuItem("Crowd Punch/Art Direction/Refresh Chapter One Breakable Gates")]
        public static void RefreshSaved()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save open scenes first.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach(int stage in new[]{4,6})
                {
                    var scene=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}/Campaign_{stage:00} Sub Scene.unity");
                    foreach(var gate in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BarricadeAuthoring>(true))) Build(gate);
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally {EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }

        internal static void Build(BarricadeAuthoring gate)
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            foreach(var visual in gate.GetComponentsInChildren<BarricadeVisualAuthoring>(true)) Object.DestroyImmediate(visual.gameObject);
            var woods=new[] {Material("Honey oak",new Color(.57f,.31f,.12f),true),
                Material("Golden oak",new Color(.69f,.41f,.19f),true),Material("Old oak",new Color(.44f,.23f,.09f),true)};
            var iron=Material("Forged iron",new Color(.16f,.20f,.22f));
            var scar=Material("Split wood",new Color(.085f,.047f,.021f));
            var chip=Material("Fresh splinters",new Color(.84f,.60f,.30f));
            var dust=Material("Wood dust",new Color(.63f,.51f,.33f));
            var meshes=Enumerable.Range(0,3).Select(FractureMesh).ToArray();
            float width=gate.size.x,height=gate.size.y;
            int count=Mathf.CeilToInt(width/.95f),index=0;
            float step=width/count;
            for(int board=0;board<count;board++)
            {
                float x=-width*.5f+(board+.5f)*step;
                for(int section=0;section<3;section++)
                    Piece(gate,"Fractured oak plank",new Vector3(x,-height*.5f+(section+.5f)*height/3,0),
                        new Vector3(step-.018f,height/3,.88f),woods[board%3],index++,mesh:meshes[section]);
                for(int damage=1;damage<=2;damage++) for(int face=-1;face<=1;face+=2)
                    Piece(gate,"Opening timber fracture",new Vector3(x,-height*.5f+damage*height/3,face*.455f),
                        new Vector3(step*.95f,.075f,.025f),scar,index++,kind:1,stage:damage,mesh:meshes[1]);
            }
            int bays=Mathf.CeilToInt(width/4);
            float bay=width/bays;
            for(int i=0;i<bays;i++)
            {
                float x=-width*.5f+(i+.5f)*bay;
                foreach(int face in new[]{-1,1})
                {
                    foreach(int row in new[]{-1,1})
                    {
                        Piece(gate,"Iron bound cross rail",new Vector3(x,row*1.3f,face*.52f),new Vector3(bay-.03f,.23f,.14f),woods[2],index++);
                        foreach(int end in new[]{-1,1})
                            Piece(gate,"Iron square bolt",new Vector3(x+end*(bay*.5f-.24f),row*1.3f,face*.605f),
                                new Vector3(.13f,.13f,.06f),iron,index++);
                    }
                    var brace=Piece(gate,"Diagonal timber brace",new Vector3(x,0,face*.52f),
                        new Vector3(bay*.93f,.18f,.14f),woods[2],index++);
                    brace.transform.localRotation=Quaternion.Euler(0,0,(i%2==0?1:-1)*32);
                }
            }
            for(int i=0;i<24;i++)
                Piece(gate,"Burst wood splinter",new Vector3((i/23f-.5f)*width,((i*7)%11/10f-.5f)*2,0),
                    new Vector3(.08f,.10f,.30f+(i%3)*.12f),chip,index++,kind:2);
            for(int i=0;i<12;i++)
                Piece(gate,"Brief wood dust puff",new Vector3((i/11f-.5f)*width,0,0),
                    Vector3.one*(.55f+i%3*.14f),dust,index++,kind:3,sphere:true);
        }

        private static GameObject Piece(BarricadeAuthoring gate,string name,Vector3 position,Vector3 size,
            Material material,int index,int kind=0,int stage=0,Mesh mesh=null,bool sphere=false)
        {
            var go=GameObject.CreatePrimitive(sphere?PrimitiveType.Sphere:PrimitiveType.Cube);
            go.name=name; Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(gate.transform,false); go.transform.localPosition=position; go.transform.localScale=size;
            if(mesh!=null) go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.forceMeshLod=0; renderer.shadowCastingMode=ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go,0);
            var visual=go.AddComponent<BarricadeVisualAuthoring>(); visual.barricade=gate; visual.crackStage=stage;
            var piece=go.AddComponent<VillageGatePieceAuthoring>(); piece.kind=kind; piece.damageStage=stage;
            piece.seed=(index*37%101)/100f;
            piece.velocity=new Vector3(Mathf.Sign(position.x)*(.8f+piece.seed*2),2.3f+piece.seed*3,2.8f+piece.seed*3);
            if(kind>=2) piece.velocity=new Vector3((piece.seed-.5f)*8,2+piece.seed*5,1+piece.seed*3);
            piece.spin=new Vector3(2+piece.seed*5,3-piece.seed*6,1+piece.seed*4);
            piece.restingHeight=kind>=2?.04f:size.z*.5f;
            return go;
        }

        private static Material Material(string name,Color color,bool wood=false)
        {
            string path=Folder+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) {material=new Material(Shader.Find("CrowdPunch/Cel/Environment"));AssetDatabase.CreateAsset(material,path);}
            material.name=name; material.color=color; material.enableInstancing=true;
            material.SetFloat("_Smoothness",.05f);
            if(wood)
            {
                var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/CrowdPunch/Data/ChapterOneVillage/Materials/Gate timber.mat");
                if(source!=null) material.SetTexture("_BaseMap",source.GetTexture("_BaseMap"));
            }
            EditorUtility.SetDirty(material); return material;
        }

        private static Mesh FractureMesh(int section)
        {
            string path=Folder+$"/Plank section {section}.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved!=null) return saved;
            float[] xs={-.5f,-.2f,.15f,.5f}; float[] dents={0,.035f,-.045f,0};
            var outline=new List<Vector2>();
            for(int i=0;i<4;i++) outline.Add(new Vector2(xs[i],-.5f+(section>0?dents[i]:0)));
            for(int i=3;i>=0;i--) outline.Add(new Vector2(xs[i],.5f+(section<2?dents[i]:0)));
            var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var triangles=new List<int>();
            void Vertex(Vector3 p) {vertices.Add(p);uv.Add(new Vector2(p.x+.5f,p.y+.5f));}
            foreach(int side in new[]{-1,1})
            {
                int center=vertices.Count; Vertex(new Vector3(0,0,side*.5f));
                foreach(var p in outline) Vertex(new Vector3(p.x,p.y,side*.5f));
                for(int i=0;i<outline.Count;i++)
                {
                    int a=center+1+i,b=center+1+(i+1)%outline.Count;
                    triangles.AddRange(side<0?new[]{center,b,a}:new[]{center,a,b});
                }
            }
            for(int i=0;i<outline.Count;i++)
            {
                var a=outline[i];var b=outline[(i+1)%outline.Count];int start=vertices.Count;
                Vertex(new Vector3(a.x,a.y,-.5f));Vertex(new Vector3(b.x,b.y,-.5f));
                Vertex(new Vector3(a.x,a.y,.5f));Vertex(new Vector3(b.x,b.y,.5f));
                triangles.AddRange(new[]{start,start+1,start+2,start+1,start+3,start+2});
            }
            var mesh=new Mesh {name=$"Plank section {section}"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);
            mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
            return mesh;
        }
    }
}
