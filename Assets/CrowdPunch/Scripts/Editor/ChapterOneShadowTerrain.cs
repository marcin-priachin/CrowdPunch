using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    // Higher shadow texel density near play, without a huge atlas for the distant hills.
    internal static class ChapterOneShadowTerrain
    {
        internal static void Split(Scene scene,int stage)
        {
            var root=scene.GetRootGameObjects().Single(r=>r.name==ChapterOneVillageBuilder.RootName).transform;
            var terrain=root.Find("Rolling meadows - visual only");
            var filter=terrain.GetComponent<MeshFilter>();
            if(filter==null) return;
            var source=filter.sharedMesh; var renderer=terrain.GetComponent<MeshRenderer>();
            var vertices=source.vertices; var normals=source.normals; var triangles=source.triangles;
            var buckets=Enumerable.Range(0,16).Select(_=>new List<int>()).ToArray();
            var bounds=source.bounds; float tileWidth=bounds.size.x/4;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var center=(vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3;
                int x=Mathf.Clamp((int)((center.x-bounds.min.x)/tileWidth),0,3);
                int z=Mathf.Clamp((int)((center.z-bounds.min.z)/tileWidth),0,3);
                buckets[z*4+x].AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});
            }
            for(int i=0;i<16;i++)
            {
                string path=ChapterOneVillageAssets.Output+$"/BakedShadows/Meshes/Terrain_{stage:00}_{i:00}.asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Mesh mesh;
                {
                    var used=buckets[i].Distinct().ToArray(); var remap=new Dictionary<int,int>();
                    for(int j=0;j<used.Length;j++) remap.Add(used[j],j);
                    mesh=new Mesh {name=$"Meadow shadow tile {stage:00}-{i:00}"};
                    mesh.vertices=used.Select(v=>vertices[v]).ToArray(); mesh.normals=used.Select(v=>normals[v]).ToArray();
                    mesh.triangles=buckets[i].Select(v=>remap[v]).ToArray(); mesh.RecalculateBounds();
                    Unwrapping.GenerateSecondaryUVSet(mesh);
                    if(saved==null) AssetDatabase.CreateAsset(mesh,path);
                    else
                    {
                        EditorUtility.CopySerialized(mesh,saved); Object.DestroyImmediate(mesh);
                        mesh=saved; EditorUtility.SetDirty(saved);
                    }
                }
                int x=i%4,z=i/4; bool near=x>=1 && x<=2 && z>=1 && z<=2;
                var go=new GameObject((near?"Near":"Distant")+$" meadow tile {z}-{x}",typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(terrain,false); go.isStatic=true;
                go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var tile=go.GetComponent<MeshRenderer>(); tile.sharedMaterial=renderer.sharedMaterial;
                tile.forceMeshLod=0; tile.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Object.DestroyImmediate(renderer); Object.DestroyImmediate(filter);
        }
    }
}
