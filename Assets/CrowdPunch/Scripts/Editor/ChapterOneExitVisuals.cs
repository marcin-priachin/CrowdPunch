using System;
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
    public static class ChapterOneExitVisuals
    {
        private const string Folder="Assets/CrowdPunch/Data/ChapterOneVillage/Exits";

        [MenuItem("Crowd Punch/Art Direction/Refresh Chapter One Exit Landmarks")]
        public static void RefreshSaved()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0,SceneManager.sceneCount)
                .Any(i=>SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Stop Play mode and save open scenes first.");
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
            if(gate.exit==null || gate.completeOnDestruction) return;
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            foreach(var child in gate.exit.Cast<Transform>().ToArray())
                if(child.name=="Village exit destination" || child.name=="Exit landing" || child.name.StartsWith("Exit post "))
                    Object.DestroyImmediate(child.gameObject);
            foreach(var old in gate.gameObject.scene.GetRootGameObjects().Where(g=>g.name=="Village exit route cues"))
                Object.DestroyImmediate(old);
            var root=new GameObject("Village exit destination").transform;
            root.SetParent(gate.exit,false);
            root.position=new Vector3(gate.exit.position.x,gate.transform.position.y-gate.size.y*.5f,gate.exit.position.z);
            // A static ancestor makes Entities baking flatten the entire hierarchy,
            // even if a child clears its own static flags. Keep moving cues separate.
            var cueRoot=new GameObject("Village exit route cues").transform;
            SceneManager.MoveGameObjectToScene(cueRoot.gameObject,gate.gameObject.scene);
            cueRoot.position=root.position;
            var wood=Material("Carved warm oak",new Color(.43f,.23f,.085f));
            var end=Material("Oak end grain",new Color(.65f,.39f,.16f));
            var stone=Material("Warm limestone",new Color(.66f,.64f,.48f));
            var gold=Material("Golden trim",new Color(.98f,.66f,.17f));
            var blue=Material("Village blue",new Color(.12f,.42f,.58f));
            blue.SetFloat("_Cull",0);
            var dark=Material("Sign inset",new Color(.055f,.18f,.24f));
            var cream=Material("Ivory direction",new Color(.97f,.92f,.68f));
            var cue=Material("Open route mint",new Color(.28f,.40f,.42f));
            foreach(int side in new[]{-1,1})
            {
                Piece(root,"Stone waygate footing",new Vector3(side*2.65f,.23f,1.35f),new Vector3(.9f,.46f,.88f),stone);
                Piece(root,"Carved oak upright",new Vector3(side*2.65f,2.25f,1.35f),new Vector3(.4f,4.15f,.42f),wood);
                Piece(root,"Gold post collar",new Vector3(side*2.65f,3.7f,1.35f),new Vector3(.47f,.14f,.49f),gold);
                Piece(root,"Stone footing cap",new Vector3(side*2.65f,.49f,1.35f),new Vector3(1.01f,.13f,.98f),stone);
                var brace=Piece(root,"Angled corner brace",new Vector3(side*2.20f,3.67f,1.35f),new Vector3(1.36f,.23f,.3f),end);
                brace.transform.localRotation=Quaternion.Euler(0,0,side*45);
                Piece(root,"Blue swallowtail pennant",new Vector3(side*2.65f,3.12f,1.07f),Vector3.one,blue,Pennant());
                Piece(root,"Pennant gold hem",new Vector3(side*2.65f,3.59f,1.04f),new Vector3(.73f,.09f,.045f),gold);
            }
            Piece(root,"Oversized lintel",new Vector3(0,4.34f,1.35f),new Vector3(6.15f,.38f,.62f),wood);
            Piece(root,"Sunlit lintel cap",new Vector3(0,4.57f,1.35f),new Vector3(6.42f,.13f,.75f),end);
            var frame=Piece(root,"Gold destination crest",new Vector3(0,4.31f,.93f),new Vector3(1.38f,1.38f,.18f),gold);
            frame.transform.localRotation=Quaternion.Euler(0,0,45);
            var inset=Piece(root,"Blue destination crest",new Vector3(0,4.31f,.81f),new Vector3(1.19f,1.19f,.10f),dark);
            inset.transform.localRotation=Quaternion.Euler(0,0,45);
            // A forward arrow is readable without text, including at the game camera's scale.
            var symbol=Piece(root,"Ivory forward emblem",new Vector3(0,4.31f,.747f),new Vector3(.87f,.95f,1),cream,Arrow());
            symbol.transform.localRotation=Quaternion.Euler(-90,0,0);

            // The outer edge matches the existing completion radius exactly; the surface stays walkable.
            Piece(root,"Gold destination perimeter",new Vector3(0,.065f,0),Vector3.one,gold,Ring(gate.exitRadius,.10f,"Destination border"));
            var ring=Piece(cueRoot,"Exit ready ring",new Vector3(0,.075f,0),Vector3.one,cue,Ring(gate.exitRadius-.15f,.13f,"Ready ring"),true);
            Cue(ring,gate,0,false);
            for(int i=0;i<3;i++)
            {
                var arrow=Piece(cueRoot,"Open route chevron",new Vector3(0,.09f,-3.2f+i*.78f),new Vector3(.75f,1,.7f),cue,Arrow(),true);
                Cue(arrow,gate,i*.9f,true);
            }
        }

        private static void Cue(GameObject go,BarricadeAuthoring gate,float phase,bool arrow)
        {
            var cue=go.AddComponent<VillageExitCueAuthoring>();cue.gate=gate;cue.phase=phase;cue.arrow=arrow;
            cue.openColor=arrow?new Color(.65f,1,.76f):new Color(.22f,.94f,.66f);
        }

        private static GameObject Piece(Transform parent,string name,Vector3 position,Vector3 scale,Material material,Mesh mesh=null,bool animated=false)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            if(mesh!=null) go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.forceMeshLod=0;
            renderer.shadowCastingMode=animated?ShadowCastingMode.Off:ShadowCastingMode.On;
            GameObjectUtility.SetStaticEditorFlags(go,animated?0:StaticEditorFlags.BatchingStatic|StaticEditorFlags.ContributeGI|
                StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic|StaticEditorFlags.ReflectionProbeStatic);
            return go;
        }

        private static Material Material(string name,Color color)
        {
            string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("CrowdPunch/Cel/Environment"));AssetDatabase.CreateAsset(material,path);}
            material.name=name;material.color=color;material.enableInstancing=true;material.SetFloat("_Smoothness",.05f);
            EditorUtility.SetDirty(material);return material;
        }

        private static Mesh Ring(float radius,float width,string name)
        {
            string path=Folder+"/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved!=null)return saved;
            const int count=48;var vertices=new Vector3[count*4];var triangles=new int[count*6];
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count,b=(i+.87f)*Mathf.PI*2/count;
                vertices[i*4]=new Vector3(Mathf.Sin(a)*radius,0,Mathf.Cos(a)*radius);
                vertices[i*4+1]=new Vector3(Mathf.Sin(a)*(radius-width),0,Mathf.Cos(a)*(radius-width));
                vertices[i*4+2]=new Vector3(Mathf.Sin(b)*radius,0,Mathf.Cos(b)*radius);
                vertices[i*4+3]=new Vector3(Mathf.Sin(b)*(radius-width),0,Mathf.Cos(b)*(radius-width));
                int v=i*4,t=i*6;triangles[t]=v;triangles[t+1]=v+2;triangles[t+2]=v+1;
                triangles[t+3]=v+1;triangles[t+4]=v+2;triangles[t+5]=v+3;
            }
            return MeshAsset(path,name,vertices,triangles);
        }

        private static Mesh Arrow()
        {
            string path=Folder+"/Forward arrow.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved!=null)return saved;
            return MeshAsset(path,"Forward arrow",new[]{new Vector3(-.55f,0,-.05f),new Vector3(0,0,.55f),new Vector3(.55f,0,-.05f),
                new Vector3(.32f,0,-.26f),new Vector3(0,0,.10f),new Vector3(-.32f,0,-.26f)},new[]{0,1,4,0,4,5,1,2,3,1,3,4});
        }

        private static Mesh Pennant()
        {
            string path=Folder+"/Swallowtail pennant.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved!=null)return saved;
            return MeshAsset(path,"Swallowtail pennant",new[]{new Vector3(-.35f,.5f,0),new Vector3(.35f,.5f,0),
                new Vector3(.35f,-.65f,0),new Vector3(0,-.40f,0),new Vector3(-.35f,-.65f,0)},
                new[]{0,1,3,1,2,3,0,3,4});
        }

        private static Mesh MeshAsset(string path,string name,Vector3[] vertices,int[] triangles)
        {
            var mesh=new Mesh{name=name,vertices=vertices,triangles=triangles};
            mesh.uv=vertices.Select(v=>new Vector2(v.x,v.z)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
    }
}
