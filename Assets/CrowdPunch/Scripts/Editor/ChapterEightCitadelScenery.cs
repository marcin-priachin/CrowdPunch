using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static CrowdPunch.Editor.ChapterEightCitadelAssets;

namespace CrowdPunch.Editor
{
    // VISION-004..006: complete fortress assemblies outside the unchanged combat envelope.
    internal static class ChapterEightCitadelScenery
    {
        internal static void Build(Transform root, Bounds[] floors, int stage)
        {
            var b=floors[0];foreach(var f in floors.Skip(1))b.Encapsulate(f);
            float y=floors.Min(f=>f.max.y)-.12f;
            Landscape(root,b,y,stage);
            int bays=Mathf.Max(3,Mathf.CeilToInt(b.size.z/23));
            foreach(int side in new[]{-1,1})
            {
                float x=b.center.x+side*(b.extents.x+14);
                Shape(root,"Continuous fortress curtain",new Vector3(x,y,b.center.z),new Vector3(4,5.4f,b.size.z+28),Surface("Slate",true));
                Shape(root,"Fortress parapet coping",new Vector3(x,y+5.4f,b.center.z),new Vector3(4,.3f,b.size.z+28),Surface("Limestone",true));
                for(int i=0;i<bays;i++)
                {
                    float z=Mathf.Lerp(b.min.z-4,b.max.z+4,(i+.5f)/bays);
                    Pier(root,new Vector3(x-side*2.7f,y,z),6.4f,-side*90,i+stage);
                    Banner(root,new Vector3(x-side*2.06f,y+1.4f,z+3.8f),-side*90,3.3f);
                    if(i%2==0)Tower(root,new Vector3(x+side*6,y,z+5),11+i%3*2,-side*90);
                }
            }
            foreach(int side in new[]{-1,1})
            {
                float z=b.center.z+side*(b.extents.z+15);
                Shape(root,"Closed fortress return",new Vector3(b.center.x,y,z),new Vector3(b.size.x+24,side<0?3.2f:5.4f,2),Surface("Slate",true));
                float height=side<0?3.2f:5.4f;
                Shape(root,"Return parapet coping",new Vector3(b.center.x,y+height,z),new Vector3(b.size.x+24,.3f,2),Surface("Limestone",true));
                int count=Mathf.Max(3,Mathf.CeilToInt(b.size.x/20));
                for(int i=0;i<count;i++)
                {
                    float x=Mathf.Lerp(b.min.x-6,b.max.x+6,(i+.5f)/count);
                    Pier(root,new Vector3(x,y,z-side*1.7f),height+1.2f,side<0?180:0,i);
                    if(side>0)Banner(root,new Vector3(x+3,y+1.2f,z-1.06f),0,3.4f);
                }
            }
            Vector3 North(float x=0,float z=32)=>new Vector3(b.center.x+x,y,b.max.z+z);
            switch(stage)
            {
                case 71:
                    Gatehouse(root,North(),15,0);foreach(int s in new[]{-1,1})Tower(root,North(s*19,35),17,0);break;
                case 72:
                    Gatehouse(root,North(),19,0);Crown(root,North(0,31)+Vector3.up*14,5);
                    foreach(int s in new[]{-1,1})Banner(root,North(s*14,23),0,7);break;
                case 73:
                    foreach(int s in new[]{-1,1})Gatehouse(root,new Vector3(b.center.x+s*(b.extents.x+29),y,b.center.z),12,-s*90);
                    Tower(root,North(),22,0);break;
                case 74:
                    var rooms=floors.OrderBy(f=>f.center.z).ToArray();
                    for(int i=0;i<rooms.Length;i++)
                    {
                        var p=new Vector3(b.center.x+(i%2==0?-1:1)*(b.extents.x+29),y,rooms[i].center.z);
                        if(i%3==0)Gatehouse(root,p,11+i, i%2==0?90:-90);
                        else if(i%3==1)Tower(root,p,15+i,0);
                        else {Pier(root,p,10+i,90,i);Crown(root,p+Vector3.up*(10+i),3.5f);}
                    }
                    Gatehouse(root,North(0,36),24,0);Crown(root,North(0,36)+Vector3.up*18,7);break;
                case 75:
                    foreach(int s in new[]{-1,1})for(int i=0;i<3;i++)
                    {
                        var p=new Vector3(b.center.x+s*(b.extents.x+27),y,b.min.z+10+i*b.size.z/3);
                        Gatehouse(root,p,11,-s*90);Stores(root,p+new Vector3(-s*7,0,5),s);
                    }
                    Gatehouse(root,North(),17,0);break;
                case 76:
                    Gatehouse(root,North(),22,0);Crown(root,North()+Vector3.up*17,7);
                    foreach(int s in new[]{-1,1})Tower(root,North(s*22,40),23,0);break;
                case 77:
                    foreach(int s in new[]{-1,1})for(int i=0;i<3;i++)Tower(root,new Vector3(b.center.x+s*(b.extents.x+26),y,b.min.z+12+i*b.size.z/3),17+i*3,0);
                    Gatehouse(root,North(),26,0);break;
                case 78:
                    Gatehouse(root,North(0,39),30,0);Crown(root,North(0,39)+Vector3.up*23,9);
                    foreach(int s in new[]{-1,1}) {Tower(root,North(s*38,37),30,0);Gatehouse(root,new Vector3(b.center.x+s*(b.extents.x+32),y,b.center.z),22,-s*90);}break;
                case 79:
                    Throne(root,North(0,34));foreach(int s in new[]{-1,1})Tower(root,North(s*22,38),14,0);break;
                case 80:
                    Throne(root,North(0,43));foreach(int s in new[]{-1,1})
                    {BrokenArch(root,North(s*22,29),16,s*16);Tower(root,new Vector3(b.center.x+s*(b.extents.x+30),y,b.center.z),23,0);}break;
            }
        }

        private static void Pier(Transform parent,Vector3 p,float height,float yaw,int seed)
        {
            var root=Group(parent,"Citadel buttress",p,yaw);
            Shape(root,"Buttress footing",Vector3.zero,new Vector3(2.8f,.55f,3.8f),Surface("Rock",true));
            Shape(root,"Ash masonry pier",Vector3.up*.55f,new Vector3(1.7f,height-.55f,2.4f),Surface("Limestone",true));
            Shape(root,"Brass pier cornice",Vector3.up*height,new Vector3(2.1f,.25f,2.8f),Surface("Ochre",true));
            Shape(root,"Inset royal seal",new Vector3(0,height*.64f,-1.24f),new Vector3(.5f,.7f,.06f),Surface(seed%2==0?"Fabric":"Ochre",true));
        }
        private static Transform Group(Transform parent,string name,Vector3 p,float yaw=0)
        {var root=new GameObject(name).transform;root.SetParent(parent,false);root.localPosition=p;root.localRotation=Quaternion.Euler(0,yaw,0);return root;}
        private static void Banner(Transform parent,Vector3 p,float yaw,float height)
        {
            var root=Group(parent,"Desaturated red royal banner",p,yaw);
            Shape(root,"Banner field",Vector3.zero,new Vector3(height*.45f,height,.09f),Surface("Fabric",true));
            Shape(root,"Banner brass hanger",new Vector3(0,height,-.02f),new Vector3(height*.53f,.12f,.16f),Surface("Ochre",true));
            Shape(root,"Painted banner seal",new Vector3(0,height*.49f,-.065f),new Vector3(height*.16f,height*.24f,.025f),Surface("Ochre",true));
        }
        private static void Gatehouse(Transform parent,Vector3 p,float size,float yaw)
        {
            var root=Group(parent,"Complete ceremonial gatehouse",p,yaw);
            float depth=size*.5f,height=size*.76f,radius=size*.27f,spring=size*.32f;
            Shape(root,"Gatehouse rear",new Vector3(0,0,depth*.5f),new Vector3(size,height,.65f),Surface("Slate",true));
            foreach(int s in new[]{-1,1})
            {
                Shape(root,"Gatehouse returning wing",new Vector3(s*(size*.5f-.35f),0,.0875f),new Vector3(.7f,height,depth-.825f),Surface("Limestone",true));
                Shape(root,"Ceremonial doorway pier",new Vector3(s*(radius+size*.115f),0,-depth*.5f),new Vector3(size*.23f,spring,1),Surface("Limestone",true));
                Banner(root,new Vector3(s*size*.39f,height*.45f,-depth*.5f-.56f),0,height*.32f);
            }
            Shape(root,"Closed gatehouse roof",new Vector3(0,height,.15f),new Vector3(size+.4f,.4f,depth+.7f),Surface("Slate",true));
            Shape(root,"Recessed iron doors",new Vector3(0,0,-depth*.5f+.57f),new Vector3(radius*2,spring+radius,.18f),Surface("Slate",true));
            Arch(root,"Royal arch",size,radius,spring,-depth*.5f-.05f,9);
            for(int i=0;i<7;i++)Shape(root,"Gatehouse crown merlon",new Vector3((i-3)*size/7,height+.4f,.15f),new Vector3(size/10,1.3f,depth+.5f),Surface("Limestone",true));
        }
        private static void Arch(Transform root,string name,float size,float radius,float spring,float z,int segments)
        {
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI/9+.018f,c=(i+1)*Mathf.PI/9-.018f;
                var v=new List<Vector3>();var t=new List<int>();
                Vector3 P(float angle,float r,float d)=>new Vector3(Mathf.Cos(angle)*r,spring+Mathf.Sin(angle)*r,d);
                float outer=radius+size*.20f;
                var aa=P(a,radius,-.5f);var ab=P(c,radius,-.5f);var ba=P(a,outer,-.5f);var bb=P(c,outer,-.5f);
                Quad(v,t,aa,ab,bb,ba);Quad(v,t,aa+Vector3.forward,ba+Vector3.forward,bb+Vector3.forward,ab+Vector3.forward);
                Quad(v,t,aa,aa+Vector3.forward,ab+Vector3.forward,ab);Quad(v,t,ba,bb,bb+Vector3.forward,ba+Vector3.forward);
                Quad(v,t,aa,ba,ba+Vector3.forward,aa+Vector3.forward);Quad(v,t,ab,ab+Vector3.forward,bb+Vector3.forward,bb);
                var wedge=MeshObject(root,name+" voussoir",SaveMesh($"Royal arch {size} stone {i}",v,t),Surface(i==4?"Ochre":"Limestone",true));wedge.transform.localPosition=Vector3.forward*z;
            }
        }
        private static void BrokenArch(Transform parent,Vector3 p,float size,float yaw)
        {
            var root=Group(parent,"Broken ceremonial arch",p,yaw);
            foreach(int s in new[]{-1,1})Shape(root,"Broken arch pier",new Vector3(s*size*.36f,0,0),new Vector3(size*.2f,size*.4f,1.3f),Surface("Limestone",true));
            Arch(root,"Broken crown arch",size,size*.26f,size*.4f,0,5);
            for(int i=0;i<3;i++)Shape(root,"Grounded arch rubble",new Vector3((i-1)*2,0,-3),new Vector3(1.4f,.6f,1.6f),Surface("Rock",true)).transform.localRotation=Quaternion.Euler(0,i*37,0);
        }
        private static void Tower(Transform root,Vector3 p,float height,float yaw)
        {Model(root,"kenney_castle-kit/Models/FBX format","tower-square",p,new Vector3(height*.43f,height,height*.43f),Surface("Slate",true),yaw);}
        private static void Crown(Transform parent,Vector3 p,float size)
        {
            var root=Group(parent,"Ceremonial brass crown",p);
            Shape(root,"Crown band",Vector3.zero,new Vector3(size,.55f,size*.3f),Surface("Ochre",true));
            for(int i=0;i<5;i++)Shape(root,"Crown prong",new Vector3((i-2)*size*.19f,.55f,0),new Vector3(size*.10f,size*(i==2?.50f:i%2==0?.36f:.26f),size*.23f),Surface("Ochre",true));
        }
        private static void Throne(Transform parent,Vector3 p)
        {
            var root=Group(parent,"Distant crown throne",p);
            Shape(root,"Throne dais",Vector3.zero,new Vector3(17,1.1f,12),Surface("Limestone",true));
            Shape(root,"Throne seat",new Vector3(0,1.1f,0),new Vector3(10,4,7),Surface("Slate",true));
            Shape(root,"Throne tall back",new Vector3(0,5.1f,3),new Vector3(10,12,1),Surface("Slate",true));
            foreach(int s in new[]{-1,1})Shape(root,"Throne arm",new Vector3(s*4.6f,5.1f,0),new Vector3(.8f,2.6f,7),Surface("Ochre",true));
            Banner(root,new Vector3(0,7.1f,2.42f),0,7);Crown(root,new Vector3(0,17.1f,3),10);
        }
        private static void Stores(Transform parent,Vector3 p,int side)
        {
            var root=Group(parent,"Inert armory stores",p);
            for(int i=0;i<3;i++)
            {Shape(root,"Closed supply chest",new Vector3((i-1)*2.2f,0,0),new Vector3(1.7f,1.4f,1.4f),Surface("Timber",true));
             Shape(root,"Supply chest lid",new Vector3((i-1)*2.2f,1.4f,0),new Vector3(1.8f,.16f,1.5f),Surface("Ochre",true));}
        }
        private static void Landscape(Transform root,Bounds b,float y,int stage)
        {
            var v=new List<Vector3>();var t=new List<int>();const int cells=64;
            for(int z=0;z<=cells;z++)for(int x=0;x<=cells;x++)v.Add(new Vector3(x/(float)cells-.5f,0,z/(float)cells-.5f));
            for(int z=0;z<cells;z++)for(int x=0;x<cells;x++){int i=z*(cells+1)+x;t.AddRange(new[]{i,i+cells+1,i+1,i+1,i+cells+1,i+cells+2});}
            var earth=MeshObject(root,"Continuous citadel ash earth",SaveMesh("Subdivided ash earth",v,t),Surface("Earth"));earth.transform.localPosition=new Vector3(b.center.x,y,b.center.z);float hazeEnd=Mathf.Max(52,b.extents.magnitude+30)+103;
            float padding=Mathf.Max(250,hazeEnd+20);
            earth.transform.localScale=new Vector3(b.size.x+padding*2,1,b.size.z+padding*2);
            for(int layer=0;layer<2;layer++)
            {
                v=new List<Vector3>();t=new List<int>();const int count=64;
                Vector3 P(int i,float d,float h){float a=i*Mathf.PI*2/count,s=Mathf.Sin(a),c=Mathf.Cos(a);return new Vector3(b.center.x+Mathf.Sign(s)*Mathf.Pow(Mathf.Abs(s),.5f)*(b.extents.x+d),y+h,b.center.z+Mathf.Sign(c)*Mathf.Pow(Mathf.Abs(c),.5f)*(b.extents.z+d));}
                float offset=layer*52;
                for(int i=0;i<count;i++)
                {
                    float h=22+layer*15+(Mathf.Sin(i*1.7f+stage)+1)*9,next=22+layer*15+(Mathf.Sin((i+1)*1.7f+stage)+1)*9;
                    Quad(v,t,P(i,49+offset,0),P(i,73+offset,h),P(i+1,73+offset,next),P(i+1,49+offset,0));
                    Quad(v,t,P(i,73+offset,h),P(i,94+offset,h*.65f),P(i+1,94+offset,next*.65f),P(i+1,73+offset,next));
                    Quad(v,t,P(i,94+offset,h*.65f),P(i,135+offset,0),P(i+1,135+offset,0),P(i+1,94+offset,next*.65f));
                }
                MeshObject(root,"Closed volcanic skyline "+layer,SaveMesh($"Volcanic skyline {stage} layer {layer}",v,t),Surface("Cliff",true,layer==1));
            }
        }
    }
}
