using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static CrowdPunch.Editor.ChapterOneVillageAssets;

namespace CrowdPunch.Editor
{
    internal static class ChapterOneVillageLandscape
    {
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
            var triangles=new List<int>();
            for(int z=0;z<=cells;z++) for(int x=0;x<=cells;x++)
            {
                float wx=floor.center.x+(x-cells/2)*step,wz=floor.center.z+(z-cells/2)*step;
                vertices[z*(cells+1)+x]=Ground(wx,wz,floor,stage);
            }
            for(int z=0;z<cells;z++) for(int x=0;x<cells;x++)
            {
                int a=z*(cells+1)+x,b=a+1,c=a+cells+1,d=c+1;
                triangles.AddRange(new[]{a,c,b,b,c,d});
            }
            var mesh=new Mesh {name=$"Village {stage:00} rolling landscape"};
            mesh.vertices=vertices;
            mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path=Output+$"/Landscape/Stage_{stage:00}.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null) AssetDatabase.CreateAsset(mesh,path);
            else {EditorUtility.CopySerialized(mesh,saved); Object.DestroyImmediate(mesh); mesh=saved; EditorUtility.SetDirty(saved);}
            var land=new GameObject("Rolling meadows - visual only",typeof(MeshFilter),typeof(MeshRenderer));
            land.transform.SetParent(root,false); land.isStatic=true;
            land.GetComponent<MeshFilter>().sharedMesh=mesh;
            land.GetComponent<MeshRenderer>().sharedMaterial=ChapterOneVillageSurfaces.Meadow(floor,stage);
            land.GetComponent<MeshRenderer>().forceMeshLod=0;
            land.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            var random=new System.Random(1703+stage*991);
            float Next(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
            int count=stage==8?190:140;
            for(int i=0;i<count;i++)
            {
                float angle=i*Mathf.PI*2/count+Next(-.06f,.06f);
                var p=Ring(angle,i%3==0?Next(30,52):Next(55,120),floor,stage);
                float height=Next(7,14);
                Model(root,"Stylized Nature","CommonTree_"+(i%5+1),p,new Vector3(height*.76f,height,height*.76f),Next(0,360));
                if(i%6==0) Model(root,"Stylized Nature","Rock_Medium_"+(i%3+1),p+new Vector3(4,0,2),new Vector3(5,2.8f,4),Next(0,360),Surface("Limestone",new Color(.64f,.59f,.43f)));
            }
        }
    }
}
