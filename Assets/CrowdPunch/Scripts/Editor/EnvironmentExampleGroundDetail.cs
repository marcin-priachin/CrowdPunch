using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    public static partial class EnvironmentExamplesBuilder
    {
        private static void BuildGroundDetail(Transform root,int theme)
        {
            var random=new System.Random(391+theme);
            float Next(float a,float b) => Mathf.Lerp(a,b,(float)random.NextDouble());
            var groundCover=Material(theme,"GroundCover",new[]{new Color(.30f,.43f,.17f),new Color(.33f,.31f,.27f),new Color(.19f,.39f,.23f),new Color(.79f,.88f,.93f)}[theme]);
            for(int i=0;i<100;i++)
            {
                float angle=Next(0,Mathf.PI*2),r=Next(30,58);
                var p=GroundPosition(Mathf.Sin(angle)*r,Mathf.Cos(angle)*r,theme);
                if(i%4==0 || theme==1 || theme==3)
                    Model(root,"Stylized Nature","Rock_Medium_"+(i%3+1),p,new Vector3(Next(1.2f,3),Next(.3f,1),Next(1,2.5f)),Next(0,360),theme,groundCover);
                else
                    Model(root,"kenney_nature","grass_large",p,new Vector3(Next(1.3f,3),Next(.4f,1.1f),Next(1.3f,2.7f)),Next(0,360),theme,groundCover);
            }
            // A continuous service path connects the arena to the surrounding settlement.
            const int segments=128;
            var vertices=new Vector3[(segments+1)*2];
            var triangles=new List<int>();
            for(int i=0;i<=segments;i++)
            {
                float a=i*Mathf.PI*2/segments;
                for(int edge=0;edge<2;edge++)
                {
                    float radius=28+Mathf.Sin(a*3)*1.2f+edge*3;
                    float x=Mathf.Sin(a)*radius,z=Mathf.Cos(a)*radius;
                    vertices[i*2+edge]=new Vector3(x,LandscapeHeight(x,z,theme)+.035f,z);
                }
                if(i<segments) triangles.AddRange(new[]{i*2,i*2+1,i*2+2,i*2+1,i*2+3,i*2+2});
            }
            var mesh=new Mesh {name=$"{theme}_Path"}; mesh.vertices=vertices; mesh.SetTriangles(triangles,0); mesh.RecalculateNormals();
            string path=Output+$"Atmosphere/{theme}_Path.asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null) AssetDatabase.CreateAsset(mesh,path);
            else { EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); mesh=existing; EditorUtility.SetDirty(mesh); }
            var road=new GameObject("Winding perimeter service path"); road.transform.SetParent(root,false); road.isStatic=true;
            road.AddComponent<MeshFilter>().sharedMesh=mesh;
            road.AddComponent<MeshRenderer>().sharedMaterial=Material(theme,"ServicePath",new[]{new Color(.48f,.43f,.29f),new Color(.18f,.22f,.25f),new Color(.36f,.43f,.32f),new Color(.48f,.62f,.72f)}[theme]);
            if(theme==0)
            {
                for(int side=-1;side<=1;side+=2)
                    for(int i=0;i<5;i++)
                        Model(root,"kenney_nature","fence_simple",GroundPosition(side*34,-12+i*6,theme),new Vector3(5.8f,1.6f,.25f),90,theme);
            }
        }
    }
}
