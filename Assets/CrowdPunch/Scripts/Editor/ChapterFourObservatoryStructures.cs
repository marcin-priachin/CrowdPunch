using System.Collections.Generic;
using UnityEngine;
using static CrowdPunch.Editor.ChapterFourObservatoryAssets;

namespace CrowdPunch.Editor
{
    // VISION-004..006: fixed, noncolliding assemblies outside the gameplay envelope.
    internal static class ChapterFourObservatoryStructures
    {
        internal const string Ruins = "Ultimate Modular Ruins Pack - Aug 2021-20261016T162013Z-1-001/Ultimate Modular Ruins Pack - Aug 2021/FBX";

        internal static Transform Assembly(Transform parent,string name,Vector3 p,float yaw=0)
        {
            var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=p;t.localRotation=Quaternion.Euler(0,yaw,0);return t;
        }

        internal static void Arcade(Transform parent,Vector3 p,int bays,float yaw,int variant)
        {
            var t=Assembly(parent,"Root-framed survey cloister",p,yaw);
            Shape(t,"Joined cloister foundation",Vector3.zero,new Vector3(bays*5.7f+1,.35f,4.4f),Surface("Limestone",true));
            for(int i=0;i<bays;i++)
            {
                float x=(i-(bays-1)*.5f)*5.7f;
                Model(t,Ruins,(variant+i)%3==0?"Wall_ArchRound_Overgrown_Broken":"Wall_ArchRound",new Vector3(x,.35f,0),new Vector3(5.65f,6.5f,1.4f),Surface("Limestone",true));
                Shape(t,"Recessed intact gallery back",new Vector3(x,.35f,1.8f),new Vector3(5.68f,6.7f,.55f),Surface("Slate",true));
                Shape(t,"Gallery capping course",new Vector3(x,7.05f,.8f),new Vector3(5.68f,.32f,3.1f),Surface("Sage",true));
                if((i+variant)%2==0)Root(t,new Vector3(x-2.4f,.35f,-.9f),5.8f);
            }
            // Returning wings and rear faces make the cloister a complete building.
            foreach(int side in new[]{-1,1})Shape(t,"Cloister return wall",new Vector3(side*(bays*2.85f+.15f),.35f,.7f),new Vector3(.55f,6.7f,3.4f),Surface("Limestone",true));
        }

        internal static void Root(Transform parent,Vector3 p,float height)
        {
            var t=Assembly(parent,"Trailing exterior roots",p);
            for(int i=0;i<4;i++)
            {
                var root=Shape(t,"Attached woody root",new Vector3(Mathf.Sin(i*1.8f)*.24f,i*height*.23f,-i*.09f),new Vector3(.28f-i*.045f,height*.27f,.22f),Surface("Timber",true));
                root.transform.localRotation=Quaternion.Euler(0,0,i%2==0?12:-16);
            }
        }

        internal static void Instrument(Transform parent,Vector3 p,float radius,bool broken,float yaw=0)
        {
            var t=Assembly(parent,broken?"Broken armillary landmark":"Survey armillary landmark",p,yaw);
            Shape(t,"Instrument stone pedestal",Vector3.zero,new Vector3(radius*1.5f,1.0f,radius*1.5f),Surface("Limestone",true),true);
            Shape(t,"Instrument brass pivot",Vector3.up,new Vector3(.55f,radius*.9f,.55f),Surface("Ochre",true),true);
            for(int i=0;i<3;i++)
            {
                var ring=MeshObject(t,"Brass meridian hoop",Ring(radius,.14f,broken&&i==1?230:360),Surface("Ochre",true));
                ring.transform.localPosition=new Vector3(0,radius+1.2f,0);ring.transform.localRotation=Quaternion.Euler(i==0?0:65,i*60,22+i*25);
            }
            Shape(t,"Quiet jade celestial sphere",new Vector3(0,radius+.6f,0),Vector3.one*radius*.65f,Surface("Sage",true),true);
        }

        internal static Mesh Ring(float radius,float width,int degrees=360)
        {
            var v=new List<Vector3>();var t=new List<int>();int count=Mathf.CeilToInt(degrees/10f);
            Vector3 P(int i,float r,float z){float a=i/(float)count*degrees*Mathf.Deg2Rad;return new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,z);}
            for(int i=0;i<count;i++)
            {
                Quad(v,t,P(i,radius,-width),P(i+1,radius,-width),P(i+1,radius-width,-width),P(i,radius-width,-width));
                Quad(v,t,P(i,radius-width,width),P(i+1,radius-width,width),P(i+1,radius,width),P(i,radius,width));
                Quad(v,t,P(i,radius,width),P(i+1,radius,width),P(i+1,radius,-width),P(i,radius,-width));
                Quad(v,t,P(i,radius-width,-width),P(i+1,radius-width,-width),P(i+1,radius-width,width),P(i,radius-width,width));
            }
            if(degrees<360)foreach(int i in new[]{0,count})Quad(v,t,P(i,radius,-width),P(i,radius,width),P(i,radius-width,width),P(i,radius-width,-width));
            return SaveMesh($"Meridian {radius} {width} {degrees}",v,t);
        }

        internal static void Tower(Transform parent,Vector3 p,float height,float yaw=0,bool broken=false)
        {
            var t=Assembly(parent,broken?"Ruined planetarium":"Watchkeeper observatory tower",p,yaw);
            Shape(t,"Octagonal tower foundation",Vector3.zero,new Vector3(11,.5f,11),Surface("Slate",true),true);
            Shape(t,"Observatory drum",Vector3.up*.5f,new Vector3(8,height,8),Surface("Limestone",true),true);
            foreach(int side in new[]{-1,1})
            {
                Shape(t,"Tower brass band",new Vector3(0,height*(side<0?.3f:.8f),0),new Vector3(8.15f,.18f,8.15f),Surface("Ochre",true),true);
                Model(t,Ruins,"Window_Open",new Vector3(side*4.03f,height*.45f,0),new Vector3(1.8f,2.6f,.5f),Surface("Slate",true),side*90);
            }
            // Closed thick dome strips retain proper undersides at broken edges.
            var v=new List<Vector3>();var indices=new List<int>();int sectors=broken?15:24;
            Vector3 P(int a,int b,float radius){float az=a*Mathf.PI/12,el=b*Mathf.PI/12;return new Vector3(Mathf.Cos(az)*Mathf.Cos(el)*radius,Mathf.Sin(el)*radius,Mathf.Sin(az)*Mathf.Cos(el)*radius);}
            for(int a=0;a<sectors;a++)for(int b=0;b<6;b++)
            {
                Quad(v,indices,P(a,b,4.7f),P(a,b+1,4.7f),P(a+1,b+1,4.7f),P(a+1,b,4.7f));
                Quad(v,indices,P(a+1,b,4.45f),P(a+1,b+1,4.45f),P(a,b+1,4.45f),P(a,b,4.45f));
                if(b==0)Quad(v,indices,P(a,b,4.45f),P(a,b,4.7f),P(a+1,b,4.7f),P(a+1,b,4.45f));
                if(a==0||a==sectors-1)foreach(int edge in new[]{a,a+1})Quad(v,indices,P(edge,b,4.45f),P(edge,b+1,4.45f),P(edge,b+1,4.7f),P(edge,b,4.7f));
            }
            var dome=MeshObject(t,"Patinated closed observatory dome",SaveMesh("Observatory dome "+broken,v,indices),Surface("Sage",true));dome.transform.localPosition=Vector3.up*(height+.5f);
            if(!broken)Instrument(t,new Vector3(0,height+5.1f,0),1.4f,false);
            Root(t,new Vector3(-3,0,-3),height*.8f);
        }
    }
}
