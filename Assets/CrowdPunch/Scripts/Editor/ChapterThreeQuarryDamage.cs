using System;
using System.Collections.Generic;
using System.Linq;
using CrowdPunch.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static CrowdPunch.Editor.ChapterThreeQuarryAssets;

namespace CrowdPunch.Editor
{
    // BARRICADE-004 / COVER-005: staged fractures consume the existing hit state.
    internal static class ChapterThreeQuarryDamage
    {
        [MenuItem("Crowd Punch/Art Direction/Refresh Chapter Three Damage Fractures")]
        public static void Refresh()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
                throw new InvalidOperationException("Stop Play mode and baking before refreshing fractures.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            if(setup.Any(s=>UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))
                throw new InvalidOperationException("Save scene edits before refreshing fractures.");
            try
            {
                foreach(int stage in new[]{22,29})
                {
                    var scene=EditorSceneManager.OpenScene(ChapterThreeQuarryBuilder.SubPath(stage));
                    foreach(var gate in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BarricadeAuthoring>(true))) Apply(gate);
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally {EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }

        internal static void Apply(BarricadeAuthoring gate)
        {
            var cracks=gate.GetComponentsInChildren<BarricadeVisualAuthoring>(true).Where(v=>v.crackStage>0);
            foreach(var group in cracks.GroupBy(v=>(stage:v.crackStage,face:Face(v.transform.localPosition))))
            {
                // Keep one staged fracture per face; suppress the old bars and duplicate slashes.
                var chosen=group.OrderByDescending(v=>v.GetComponent<VillageGatePieceAuthoring>()!=null).First();
                foreach(var visual in group)
                {
                    var renderer=visual.GetComponent<MeshRenderer>();renderer.enabled=visual==chosen;
                    EditorUtility.SetDirty(renderer);
                }
                bool side=group.Key.face>=2;
                float sign=group.Key.face%2==0?-1:1;
                float faceOffset=gate.cover!=null?.14f:.05f;
                chosen.transform.localPosition=side ? new Vector3(sign*(gate.size.x*.5f+faceOffset),0,0) : new Vector3(0,0,sign*(gate.size.z*.5f+faceOffset));
                chosen.transform.localRotation=Quaternion.Euler(0,side?90:0,0);
                chosen.transform.localScale=new Vector3((side?gate.size.z:gate.size.x)*.78f,gate.size.y*.72f,1);
                var filter=chosen.GetComponent<MeshFilter>();filter.sharedMesh=Fracture(group.Key.stage);
                EditorUtility.SetDirty(filter);EditorUtility.SetDirty(chosen.transform);
            }
        }

        private static int Face(Vector3 p)=>Mathf.Abs(p.x)>Mathf.Abs(p.z) ? (p.x<0?2:3) : (p.z<0?0:1);

        private static Mesh Fracture(int stage)
        {
            string folder=Output+"/Damage";System.IO.Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            string path=folder+"/Branching fracture "+stage+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh!=null)return mesh;
            var vertices=new List<Vector3>();var triangles=new List<int>();
            if(stage==1)
            {
                Branch(vertices,triangles,new[]{new Vector2(-.28f,-.38f),new Vector2(-.10f,-.17f),new Vector2(-.17f,-.03f),new Vector2(.06f,.17f),new Vector2(-.04f,.38f)},.022f);
                Branch(vertices,triangles,new[]{new Vector2(.06f,.17f),new Vector2(.23f,.22f),new Vector2(.34f,.36f)},.014f);
                Branch(vertices,triangles,new[]{new Vector2(-.10f,-.17f),new Vector2(-.30f,-.13f),new Vector2(-.38f,-.23f)},.012f);
            }
            else
            {
                Branch(vertices,triangles,new[]{new Vector2(-.17f,-.03f),new Vector2(.06f,-.13f),new Vector2(.17f,-.09f),new Vector2(.36f,-.34f)},.026f);
                Branch(vertices,triangles,new[]{new Vector2(.17f,-.09f),new Vector2(.29f,.05f),new Vector2(.40f,.02f)},.017f);
                Branch(vertices,triangles,new[]{new Vector2(-.17f,-.03f),new Vector2(-.32f,.14f),new Vector2(-.24f,.28f),new Vector2(-.37f,.40f)},.019f);
                Branch(vertices,triangles,new[]{new Vector2(.06f,-.13f),new Vector2(-.02f,-.31f),new Vector2(.08f,-.43f)},.013f);
            }
            mesh=new Mesh{name="Branching fracture "+stage};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }

        private static void Branch(List<Vector3> vertices,List<int> triangles,Vector2[] points,float width)
        {
            for(int i=0;i<points.Length-1;i++)
            {
                var delta=(points[i+1]-points[i]).normalized;
                var normal=new Vector2(-delta.y,delta.x);
                float a=width*Mathf.Lerp(1,.18f,(float)i/(points.Length-1));
                float b=width*Mathf.Lerp(1,.18f,(float)(i+1)/(points.Length-1));
                // A thin closed ribbon reads from both sides without a floating solid bar.
                Vector2[] corners={points[i]-normal*a*.5f,points[i]+normal*a*.5f,points[i+1]+normal*b*.5f,points[i+1]-normal*b*.5f};
                int start=vertices.Count;
                foreach(float z in new[]{-.004f,.004f})foreach(var p in corners)vertices.Add(new Vector3(p.x,p.y,z));
                int[] indices={0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7};
                foreach(int index in indices)triangles.Add(start+index);
            }
        }
    }
}
