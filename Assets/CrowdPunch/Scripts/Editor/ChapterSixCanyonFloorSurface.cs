using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    // VISION-004/006: repair the visible concave surface without changing its physics mesh.
    public static class ChapterSixCanyonFloorSurface
    {
        internal const string MeshPath=ChapterSixCanyonAssets.Output+"/Geometry/Level 52 concave floor surface.asset";

        [MenuItem("Crowd Punch/Art Direction/Fix Level 52 Floor Triangulation")]
        public static void Refresh()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning ||
                Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save scenes and stop Play/baking before correcting the floor.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene=EditorSceneManager.OpenScene(ChapterSixCanyonBuilder.SubPath(52));Apply(scene);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            }
            finally {EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }

        internal static void Apply(Scene scene)
        {
            var filter=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>()).Single(f=>f.name=="Continuous Convex Floor");
            // The collider remains the immutable source of footprint vertices, never the destination.
            var source=filter.GetComponent<MeshCollider>().sharedMesh;
            var points=source.vertices;var uv=source.uv;int n=points.Length/2;
            if(n<3 || points.Length!=n*2 || Enumerable.Range(0,n).Any(i=>
                points[i].y!=points[0].y || points[i+n].y!=points[n].y ||
                points[i].x!=points[i+n].x || points[i].z!=points[i+n].z))
                throw new InvalidOperationException("Expected the original extruded level-52 floor outline.");
            var outline=points.Take(n).Select(p=>new Vector2(p.x,p.z)).ToArray();
            var surface=GauntletProgressionBuilder.TriangulateFloorOutline(outline);
            var vertices=new List<Vector3>();var texcoords=new List<Vector2>();var indices=new List<int>();
            void Triangle(int a,int b,int c)
            {
                foreach(int i in new[]{a,b,c}){indices.Add(vertices.Count);vertices.Add(points[i]);texcoords.Add(uv[i]);}
            }
            // Separate cap/side vertices keep the horizontal floor normals uniformly upward.
            var original=source.triangles;
            for(int i=0;i<original.Length;i+=3)
            {
                int a=original[i],b=original[i+1],c=original[i+2];
                if(points[a].y==points[b].y && points[b].y==points[c].y)continue;
                Triangle(a,b,c);
            }
            for(int i=0;i<surface.Count;i+=3)
            {
                int a=surface[i],b=surface[i+1],c=surface[i+2];
                Triangle(a,c,b);Triangle(n+a,n+b,n+c);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(MeshPath));AssetDatabase.Refresh();
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);bool created=mesh==null;
            if(created)mesh=new Mesh{name=Path.GetFileNameWithoutExtension(MeshPath)};else mesh.Clear();
            mesh.SetVertices(vertices);mesh.SetUVs(0,texcoords);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            if(created)AssetDatabase.CreateAsset(mesh,MeshPath);else EditorUtility.SetDirty(mesh);
            filter.sharedMesh=mesh;EditorUtility.SetDirty(filter);
            if(filter.GetComponent<MeshCollider>().sharedMesh!=source)throw new InvalidOperationException("Floor collider changed.");
        }
    }
}
