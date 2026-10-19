using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    internal static class FoundryShellMeshes
    {
        internal static Mesh Plate()
        {
            string path = FoundryShellVisuals.Output + "/Bevelled metal plate.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            var outline = new[] {new Vector2(-.42f,-.5f),new Vector2(.42f,-.5f),new Vector2(.5f,-.42f),new Vector2(.5f,.42f),
                new Vector2(.42f,.5f),new Vector2(-.42f,.5f),new Vector2(-.5f,.42f),new Vector2(-.5f,-.42f)};
            var vertices = new List<Vector3>();
            void Triangle(Vector3 a, Vector3 b, Vector3 c) { vertices.Add(a); vertices.Add(b); vertices.Add(c); }
            for (int i = 0; i < outline.Length; i++)
            {
                var a = outline[i]; var b = outline[(i+1)%outline.Length];
                var backA = new Vector3(a.x,a.y,-.5f); var backB = new Vector3(b.x,b.y,-.5f);
                var frontA = new Vector3(a.x,a.y,.5f); var frontB = new Vector3(b.x,b.y,.5f);
                Triangle(new Vector3(0,0,-.5f),backB,backA);
                Triangle(new Vector3(0,0,.5f),frontA,frontB);
                Triangle(backA,backB,frontB); Triangle(backA,frontB,frontA);
            }
            return Save(path, vertices);
        }

        internal static Mesh Ring()
        {
            string path = FoundryShellVisuals.Output + "/Machined core flange.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            var vertices = new List<Vector3>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) {vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(a);vertices.Add(c);vertices.Add(d);}
            const int sides = 20;
            for (int i = 0; i < sides; i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                Vector3 Point(float angle,float radius,float z)=>new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,z);
                Quad(Point(a,.5f,.5f),Point(b,.5f,.5f),Point(b,.36f,.5f),Point(a,.36f,.5f));
                Quad(Point(a,.5f,-.5f),Point(a,.36f,-.5f),Point(b,.36f,-.5f),Point(b,.5f,-.5f));
                Quad(Point(a,.5f,-.5f),Point(b,.5f,-.5f),Point(b,.5f,.5f),Point(a,.5f,.5f));
                Quad(Point(a,.36f,-.5f),Point(a,.36f,.5f),Point(b,.36f,.5f),Point(b,.36f,-.5f));
            }
            return Save(path,vertices);
        }

        internal static Mesh Accent()
        {
            string path=FoundryShellVisuals.Output+"/Painted impact chip.asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null)return existing;
            var vertices=new List<Vector3>();
            var sides=new[]{Vector3.right*.5f,Vector3.forward*.5f,Vector3.left*.5f,Vector3.back*.5f};
            for(int i=0;i<4;i++){vertices.Add(Vector3.up*.5f);vertices.Add(sides[(i+1)%4]);vertices.Add(sides[i]);vertices.Add(Vector3.down*.5f);vertices.Add(sides[i]);vertices.Add(sides[(i+1)%4]);}
            return Save(path,vertices);
        }

        private static Mesh Save(string path,List<Vector3> vertices)
        {
            var mesh=new Mesh{name=System.IO.Path.GetFileNameWithoutExtension(path)};
            mesh.SetVertices(vertices);var indices=new int[vertices.Count];for(int i=0;i<indices.Length;i++)indices[i]=i;
            mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
    }
}
