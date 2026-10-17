using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    // A closed shell avoids seams between unrelated wall and gable kits.
    internal static class ChapterOneVillageHouseBody
    {
        internal static void Create(Transform parent, float wallHeight, Material plaster)
        {
            string path=ChapterOneVillageAssets.Output+$"/Landscape/HouseShell_{wallHeight:0}.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null)
            {
                mesh=Build(wallHeight);
                AssetDatabase.CreateAsset(mesh,path);
            }
            var body=new GameObject("Continuous plaster walls and gables",typeof(MeshFilter),typeof(MeshRenderer));
            body.transform.SetParent(parent,false);
            body.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=body.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=plaster; renderer.forceMeshLod=0;
            body.isStatic=true;
        }

        private static Mesh Build(float height)
        {
            // These points sit inside the tile thickness, following the imported roof's
            // curved underside rather than its decorative ridge/outer bounding box.
            var outline=new[]{new Vector2(-3.5f,0),new Vector2(3.5f,0),
                new Vector2(3.5f,height+.42f),new Vector2(2,height+1.39f),
                new Vector2(0,height+2.90f),new Vector2(-2,height+1.39f),new Vector2(-3.5f,height+.42f)};
            var vertices=new List<Vector3>(); var indices=new List<int>();
            for(int side=0;side<2;side++)
            {
                float z=side==0?-3:3; int start=vertices.Count;
                vertices.Add(new Vector3(0,height*.5f,z));
                foreach(var p in outline) vertices.Add(new Vector3(p.x,p.y,z));
                for(int i=0;i<outline.Length;i++)
                {
                    int a=start+1+i,b=start+1+(i+1)%outline.Length;
                    indices.Add(start); indices.Add(side==0?b:a); indices.Add(side==0?a:b);
                }
            }
            for(int i=0;i<outline.Length;i++)
            {
                var a=outline[i]; var b=outline[(i+1)%outline.Length]; int start=vertices.Count;
                vertices.Add(new Vector3(a.x,a.y,-3)); vertices.Add(new Vector3(b.x,b.y,-3));
                vertices.Add(new Vector3(b.x,b.y,3)); vertices.Add(new Vector3(a.x,a.y,3));
                indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            var mesh=new Mesh {name=$"Continuous village house shell {height:0}"};
            mesh.SetVertices(vertices); mesh.SetTriangles(indices,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
