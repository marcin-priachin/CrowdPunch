using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static CrowdPunch.Editor.ChapterTwoFoundryAssets;

namespace CrowdPunch.Editor
{
    // Noncolliding construction around the court: routes join real service destinations.
    internal static class ChapterTwoFoundryInfrastructure
    {
        internal static void Pipe(Transform root,float diameter,params Vector3[] corners)
        {
            var points=new List<Vector3>{corners[0]};
            for(int i=1;i<corners.Length-1;i++)
            {
                float radius=Mathf.Min(diameter*1.5f,Vector3.Distance(corners[i-1],corners[i])*.3f,Vector3.Distance(corners[i+1],corners[i])*.3f);
                var start=corners[i]+(corners[i-1]-corners[i]).normalized*radius;
                var end=corners[i]+(corners[i+1]-corners[i]).normalized*radius;
                points.Add(start);
                for(int j=1;j<=6;j++){float t=j/6f;points.Add((1-t)*(1-t)*start+2*(1-t)*t*corners[i]+t*t*end);}
            }
            points.Add(corners[corners.Length-1]);
            string key=diameter.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+string.Join(";",points.Select(p=>p.ToString("R")));
            string path=Output+"/Meshes/Pipe-"+Hash128.Compute(key)+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null)
            {
                const int sides=12;var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
                Vector3 normal=Vector3.up;float distance=0;
                for(int i=0;i<points.Count;i++)
                {
                    var tangent=(points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(i-1,0)]).normalized;
                    normal=Vector3.ProjectOnPlane(normal,tangent).normalized;
                    if(normal.sqrMagnitude<.1f)normal=Vector3.Cross(tangent,Vector3.forward).normalized;
                    var side=Vector3.Cross(tangent,normal).normalized;
                    if(i>0)distance+=Vector3.Distance(points[i-1],points[i]);
                    for(int j=0;j<sides;j++)
                    {
                        float angle=j*Mathf.PI*2/sides;var n=normal*Mathf.Cos(angle)+side*Mathf.Sin(angle);
                        vertices.Add(points[i]+n*diameter*.5f);normals.Add(n);uv.Add(new Vector2(j/(float)sides,distance/diameter));
                        if(i==0)continue;int a=(i-1)*sides+j,b=(i-1)*sides+(j+1)%sides,c=i*sides+j,d=i*sides+(j+1)%sides;
                        triangles.AddRange(new[]{a,b,c,b,d,c});
                    }
                }
                // Closed ends are hidden in ports or flange plates; no open silhouettes.
                for(int end=0;end<2;end++)
                {
                    int ring=end==0?0:(points.Count-1)*sides,center=vertices.Count;
                    vertices.Add(points[end==0?0:points.Count-1]);normals.Add(end==0?(points[0]-points[1]).normalized:(points.Last()-points[points.Count-2]).normalized);uv.Add(Vector2.zero);
                    for(int j=0;j<sides;j++)triangles.AddRange(end==0?new[]{center,ring+(j+1)%sides,ring+j}:new[]{center,ring+j,ring+(j+1)%sides});
                }
                mesh=new Mesh{name="Constant diameter rounded foundry pipe"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
            }
            var go=new GameObject("Connected process pipe",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=Copper;go.GetComponent<MeshRenderer>().forceMeshLod=0;
            for(int i=0;i<corners.Length-1;i++)
            {
                var direction=(corners[i+1]-corners[i]).normalized;float length=Vector3.Distance(corners[i],corners[i+1]);
                for(float d=diameter*2;d<length-diameter;d+=4)
                    Cylinder(root,"Pipe flange",corners[i]+direction*d,direction,diameter*1.3f,.16f,Steel);
            }
        }

        private static void Cylinder(Transform root,string name,Vector3 center,Vector3 direction,float diameter,float length,Material material)
        {
            var go=Shape(root,name,Vector3.zero,new Vector3(diameter,length,diameter),material,true);
            go.transform.localPosition=center;go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,direction);
        }

        internal static void ServiceAprons(Transform root,Bounds court,float y)
        {
            foreach(int side in new[]{-1,1})
            {
                float x=side<0?court.min.x-5.5f:court.max.x+5.5f;
                Shape(root,"Side service paving",new Vector3(x,y,court.center.z),new Vector3(7,.20f,court.size.z+13),Ground);
                for(float z=court.min.z;z<court.max.z+5;z+=5)
                    Shape(root,"Drainage channel",new Vector3(x+side*2.8f,y+.21f,z),new Vector3(.25f,.05f,4.4f),Coal);
            }
            Shape(root,"Rear loading concourse",new Vector3(court.center.x,y,court.max.z+5.5f),new Vector3(court.size.x+18,.20f,7),Ground);
        }

        internal static void LoadingBay(Transform root,Vector3 p,int count,bool machine)
        {
            Shape(root,"Raised loading dock",p,new Vector3(8,.45f,6),Steel);
            foreach(int side in new[]{-1,1})
            {
                Shape(root,"Dock canopy upright",p+new Vector3(side*3.6f,.45f,2),new Vector3(.18f,3,.18f),Steel);
                Shape(root,"Dock corner marking",p+new Vector3(side*3.4f,.46f,-2.7f),new Vector3(.15f,.025f,1.2f),Brass);
            }
            Shape(root,"Loading shelter roof",p+new Vector3(0,3.4f,1),new Vector3(8.3f,.22f,3.5f),Teal);
            for(int i=0;i<count;i++)
                Model(root,machine && i==0?"machine-fortified":"box-large",p+new Vector3(-2.5f+(i%3)*2.3f,.45f,(i/3)*2-1),new Vector3(2,2.2f,1.8f),i%2==0?Teal:Steel,(i%2)*90);
            // Grounded steps make the loading platform part of the building's service edge.
            for(int i=0;i<3;i++)Shape(root,"Dock access step",p+new Vector3(2.6f,0,-3.7f+i*.3f),new Vector3(1.6f,.15f*(i+1),.35f),Steel);
        }
    }
}
