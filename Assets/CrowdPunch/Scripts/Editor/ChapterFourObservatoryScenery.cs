using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static CrowdPunch.Editor.ChapterFourObservatoryAssets;
using static CrowdPunch.Editor.ChapterFourObservatoryStructures;

namespace CrowdPunch.Editor
{
    internal static class ChapterFourObservatoryScenery
    {
        internal static void Build(Transform root,Bounds[] floors,int stage)
        {
            var court=floors[0];foreach(var b in floors.Skip(1))court.Encapsulate(b);
            float y=floors.Min(b=>b.max.y)-.12f;
            Exterior(root,court,y);GardenHorizon(root,court,y,stage);
            // The entire combined floor envelope is protected, including elevated sectors.
            foreach(int side in new[]{-1,1})
            {
                float x=court.center.x+side*(court.extents.x+11);
                Arcade(root,new Vector3(x,y,court.center.z),Mathf.Clamp(Mathf.CeilToInt(court.size.z/12),3,10),side*90,stage);
                for(int i=0;i<5;i++)
                {
                    float z=Mathf.Lerp(court.min.z-10,court.max.z+10,i/4f);
                    Model(root,"kenney_nature-kit",i%2==0?"tree_oak":"tree_small",new Vector3(x+side*13,y,z),new Vector3(8,13+i%2*4,8),Surface("Sage",true),i*71+stage*13);
                    Model(root,Ruins,"Tree_2",new Vector3(x+side*5,y,z+3),new Vector3(4,7,4),Surface("Timber",true),i*33);
                }
            }
            // Joined north frontage plus a lower southern entrance retains full orbit coverage.
            Arcade(root,new Vector3(court.center.x,y,court.max.z+14),Mathf.Clamp(Mathf.CeilToInt(court.size.x/6),4,18),0,stage);
            Arcade(root,new Vector3(court.center.x,y,court.min.z-17),4,180,stage+1);
            switch(stage)
            {
                case 31:
                    Instrument(root,new Vector3(court.max.x+13,y,court.max.z+9),4.5f,true,25);
                    Tower(root,new Vector3(court.min.x-19,y,court.max.z+22),10,20,true);break;
                case 32:
                    Tower(root,new Vector3(court.center.x+9,y,court.max.z+25),13);
                    Instrument(root,new Vector3(court.min.x-12,y,court.min.z+3),2.5f,false);break;
                case 33:
                    for(int i=0;i<4;i++)Root(root,new Vector3(court.min.x-8,y,court.min.z+i*10),5+i);
                    Tower(root,new Vector3(court.max.x+21,y,court.center.z),9,90,true);
                    Instrument(root,new Vector3(court.center.x-7,y,court.max.z+23),3.4f,true);break;
                case 34:
                    for(int i=0;i<floors.Length;i++)
                    {
                        var b=floors.OrderBy(b=>b.center.z).ElementAt(i);
                        var p=new Vector3(court.min.x-17,y,b.center.z);
                        if(i==0)Instrument(root,p,3.5f,true);
                        else if(i==1)Arcade(root,p,3,90,i);
                        else Tower(root,p,10+i*2,90,true);
                    }break;
                case 35:
                    Instrument(root,new Vector3(court.center.x,y,court.max.z+24),6,false,35);
                    Tower(root,new Vector3(court.min.x-21,y,court.min.z+4),9,80,true);break;
                case 36:
                    Tower(root,new Vector3(court.center.x,y,court.max.z+28),20);
                    Instrument(root,new Vector3(court.max.x+15,y,court.max.z-12),3.5f,false);break;
                case 37:
                    Tower(root,new Vector3(court.center.x-7,y,court.max.z+26),11,0,true);
                    Instrument(root,new Vector3(court.max.x+15,y,court.center.z-8),4,false,70);break;
                case 38:
                    for(int i=0;i<4;i++)Tower(root,new Vector3(court.center.x+(i-1.5f)*24,y,court.max.z+24+(i%2)*9),10+i*3,i*35,i%2==0);
                    Instrument(root,new Vector3(court.min.x-19,y,court.center.z),6,true);break;
                case 39:
                    Instrument(root,new Vector3(court.center.x,y,court.max.z+25),5,false,80);
                    Tower(root,new Vector3(court.max.x+24,y,court.min.z+7),16,45,true);break;
                case 40:
                    Tower(root,new Vector3(court.center.x,y,court.max.z+26),9,130,true);
                    foreach(int side in new[]{-1,1})Instrument(root,new Vector3(court.center.x+side*(court.extents.x+16),y,court.center.z+4),5,side<0,side*30);
                    break;
            }
        }

        private static void Exterior(Transform root,Bounds court,float y)
        {
            const int cells=72;var v=new List<Vector3>();var t=new List<int>();
            for(int z=0;z<=cells;z++)for(int x=0;x<=cells;x++)v.Add(new Vector3(x/(float)cells-.5f,0,z/(float)cells-.5f));
            for(int z=0;z<cells;z++)for(int x=0;x<cells;x++){int i=z*(cells+1)+x;t.AddRange(new[]{i,i+cells+1,i+1,i+1,i+cells+1,i+cells+2});}
            var go=MeshObject(root,"Continuous observatory earth",SaveMesh("Subdivided garden earth",v,t),Surface("Earth"));
            go.transform.localPosition=new Vector3(court.center.x,y,court.center.z);go.transform.localScale=new Vector3(court.size.x+500,1,court.size.z+500);
        }

        private static void GardenHorizon(Transform root,Bounds b,float y,int stage)
        {
            var v=new List<Vector3>();var t=new List<int>();const int segments=80;
            // A continuous, closed garden escarpment backs all four architectural frontages.
            Vector3 P(int i,float distance,bool top)
            {
                float a=i*Mathf.PI*2/segments,s=Mathf.Sin(a),c=Mathf.Cos(a);
                return new Vector3(b.center.x+Mathf.Sign(s)*Mathf.Pow(Mathf.Abs(s),.5f)*(b.extents.x+distance),y+(top?15+Mathf.Sin(i*.73f+stage)*2:0),b.center.z+Mathf.Sign(c)*Mathf.Pow(Mathf.Abs(c),.5f)*(b.extents.z+distance));
            }
            for(int i=0;i<segments;i++)
            {
                Quad(v,t,P(i,43,false),P(i,43,true),P(i+1,43,true),P(i+1,43,false));
                Quad(v,t,P(i,43,true),P(i,65,true),P(i+1,65,true),P(i+1,43,true));
                Quad(v,t,P(i+1,65,false),P(i+1,65,true),P(i,65,true),P(i,65,false));
            }
            MeshObject(root,"Continuous wooded garden horizon",SaveMesh("Garden horizon "+stage,v,t),Surface("Sage",true,true));
            for(int i=0;i<20;i++)
            {
                var p=P(i*4,48,false);
                Model(root,"kenney_nature-kit",i%2==0?"tree_oak":"tree_detailed",p,new Vector3(12,21+i%3*3,12),Surface("Sage",true,true),i*43);
            }
        }
    }
}
