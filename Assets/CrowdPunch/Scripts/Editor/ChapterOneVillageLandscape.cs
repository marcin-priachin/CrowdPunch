using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static CrowdPunch.Editor.ChapterOneVillageAssets;

namespace CrowdPunch.Editor
{
    internal static class ChapterOneVillageLandscape
    {
        internal static void Paving(Transform root,Bounds floor,Vector2[] outline,int stage)
        {
            // Only complete slabs inside the convex authored polygon; collision remains in ECS.
            bool Inside(Vector2 p)
            {
                for(int i=0;i<outline.Length;i++)
                {
                    var a=outline[i]; var edge=outline[(i+1)%outline.Length]-a;
                    if(edge.x*(p.y-a.y)-edge.y*(p.x-a.x)<.4f) return false;
                }
                return outline.Length>=3;
            }
            var vertices=new List<Vector3>();
            var triangles=new[]{new List<int>(),new List<int>()};
            int row=0;
            for(float z=floor.min.z+1.4f;z<floor.max.z-1.4f;z+=3.6f,row++)
            for(float x=floor.min.x+1.4f+(row%2)*1.8f;x<floor.max.x-1.4f;x+=3.6f)
            {
                const float half=1.78f;
                var corners=new[]{new Vector2(x-half,z-half),new Vector2(x-half,z+half),new Vector2(x+half,z+half),new Vector2(x+half,z-half)};
                bool valid=true; foreach(var corner in corners) valid &= Inside(corner);
                if(!valid) continue;
                int a=vertices.Count;
                foreach(var corner in corners) vertices.Add(new Vector3(corner.x,floor.max.y+.008f,corner.y));
                triangles[(row+Mathf.RoundToInt(x))&1].AddRange(new[]{a,a+1,a+2,a,a+2,a+3});
            }
            var mesh=new Mesh {name=$"Village {stage:00} quiet paving"}; mesh.SetVertices(vertices); mesh.subMeshCount=2;
            for(int i=0;i<2;i++) mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path=Output+$"/Landscape/Paving_{stage:00}.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null) AssetDatabase.CreateAsset(mesh,path);
            else {EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);mesh=saved;EditorUtility.SetDirty(saved);}
            var go=new GameObject("Quiet courtyard paving",typeof(MeshFilter),typeof(MeshRenderer)); go.transform.SetParent(root,false); go.isStatic=true;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials=new[]{Surface("Paving A",new Color(.535f,.485f,.345f)),Surface("Paving B",new Color(.528f,.478f,.338f))};
            go.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        internal static float Height(float x,float z,Bounds floor,int stage)
        {
            float dx=Mathf.Max(0,Mathf.Abs(x-floor.center.x)-floor.extents.x);
            float dz=Mathf.Max(0,Mathf.Abs(z-floor.center.z)-floor.extents.z);
            float distance=new Vector2(dx,dz).magnitude;
            float rise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(32,125,distance));
            return floor.max.y-.18f+rise*(8+Mathf.PerlinNoise(x*.011f+stage*7,z*.011f+37)*27);
        }

        internal static Vector3 Ground(float x,float z,Bounds floor,int stage) => new(x,Height(x,z,floor,stage),z);

        internal static Vector3 Ring(float angle,float setback,Bounds floor,int stage)
        {
            float sx=Mathf.Sin(angle),sz=Mathf.Cos(angle);
            float edge=Mathf.Min(floor.extents.x/Mathf.Max(.001f,Mathf.Abs(sx)),floor.extents.z/Mathf.Max(.001f,Mathf.Abs(sz)));
            return Ground(floor.center.x+sx*(edge+setback),floor.center.z+sz*(edge+setback),floor,stage);
        }

        internal static void Build(Transform root,Bounds floor,int stage)
        {
            const int cells=104;
            const float step=6;
            var vertices=new Vector3[(cells+1)*(cells+1)];
            var triangles=new[]{new List<int>(),new List<int>(),new List<int>()};
            for(int z=0;z<=cells;z++) for(int x=0;x<=cells;x++)
            {
                float wx=floor.center.x+(x-cells/2)*step,wz=floor.center.z+(z-cells/2)*step;
                vertices[z*(cells+1)+x]=Ground(wx,wz,floor,stage);
            }
            for(int z=0;z<cells;z++) for(int x=0;x<cells;x++)
            {
                int a=z*(cells+1)+x,b=a+1,c=a+cells+1,d=c+1;
                float noise=Mathf.PerlinNoise(x*.07f+stage*3,z*.07f);
                int band=noise<.38f?0:noise>.63f?2:1;
                triangles[band].AddRange(new[]{a,c,b,b,c,d});
            }
            var mesh=new Mesh {name=$"Village {stage:00} rolling landscape"};
            mesh.vertices=vertices; mesh.subMeshCount=3;
            for(int i=0;i<3;i++) mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path=Output+$"/Landscape/Stage_{stage:00}.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null) AssetDatabase.CreateAsset(mesh,path);
            else {EditorUtility.CopySerialized(mesh,saved); Object.DestroyImmediate(mesh); mesh=saved; EditorUtility.SetDirty(saved);}
            var land=new GameObject("Rolling meadows - visual only",typeof(MeshFilter),typeof(MeshRenderer));
            land.transform.SetParent(root,false); land.isStatic=true;
            land.GetComponent<MeshFilter>().sharedMesh=mesh;
            land.GetComponent<MeshRenderer>().sharedMaterials=new[]{
                Surface("Meadow shade",new Color(.29f,.38f,.16f)),
                Surface("Meadow",new Color(.34f,.43f,.19f)),
                Surface("Meadow sun",new Color(.38f,.46f,.22f))};
            land.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            var random=new System.Random(1703+stage*991);
            float Next(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
            int count=stage==8?100:64;
            for(int i=0;i<count;i++)
            {
                float angle=i*Mathf.PI*2/count+Next(-.06f,.06f);
                var p=Ring(angle,Next(26,120),floor,stage);
                float height=Next(7,14);
                Model(root,"Stylized Nature","CommonTree_"+(i%5+1),p,new Vector3(height*.76f,height,height*.76f),Next(0,360));
                if(i%6==0) Model(root,"Stylized Nature","Rock_Medium_"+(i%3+1),p+new Vector3(4,0,2),new Vector3(5,2.8f,4),Next(0,360),Surface("Limestone",new Color(.64f,.59f,.43f)));
            }
        }
    }
}
