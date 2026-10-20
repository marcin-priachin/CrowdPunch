using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static CrowdPunch.Editor.ChapterFiveNecropolisAssets;

namespace CrowdPunch.Editor
{
    // VISION-004..006: composed noncolliding scenery outside the complete playable envelope.
    internal static class ChapterFiveNecropolisScenery
    {
        private const string Graves = "kenney_graveyard-kit_5.0/Models/FBX format";

        internal static void Build(Transform root, Bounds[] floors, int stage)
        {
            var b=floors[0];foreach(var floor in floors.Skip(1))b.Encapsulate(floor);
            float y=floors.Min(f=>f.max.y)-.12f;
            Landscape(root,b,y,stage);
            Enclosure(root,b,y);
            // Low memorial gardens close the near layer; taller masses sit farther behind them.
            int rows=Mathf.Max(3,Mathf.CeilToInt(b.size.z/15));
            foreach(int side in new[]{-1,1})for(int i=0;i<rows;i++)
            {
                float z=Mathf.Lerp(b.min.z-3,b.max.z+3,(i+.5f)/rows);
                var p=new Vector3(b.center.x+side*(b.extents.x+8),y,z);
                MemorialGarden(root,p,i+stage,side*90);
                if(i%2==0)Mausoleum(root,p+new Vector3(side*13,0,4),5.5f,side*90,i%3==0);
                else Tree(root,p+new Vector3(side*12,0,2),8+i%3*2,stage+i*37);
            }
            int frontage=Mathf.Max(3,Mathf.CeilToInt(b.size.x/16));
            foreach(int side in new[]{-1,1})for(int i=0;i<frontage;i++)
            {
                float x=Mathf.Lerp(b.min.x-3,b.max.x+3,(i+.5f)/frontage);
                MemorialGarden(root,new Vector3(x,y,b.center.z+side*(b.extents.z+10)),stage+i,side<0?180:0);
                Tree(root,new Vector3(x+5,y,b.center.z+side*(b.extents.z+22)),9+(i%3)*2,i*71+stage);
            }
            Vector3 North(float x=0,float z=20)=>new Vector3(b.center.x+x,y,b.max.z+z);
            switch(stage)
            {
                case 41:
                    foreach(int side in new[]{-1,1})Monument(root,North(side*10,18),8,side*12);
                    Mausoleum(root,North(0,36),8,0,false);break;
                case 42:
                    // The four existing sectors get an individually recognizable scenic destination.
                    for(int sector=0;sector<4;sector++)
                    {
                        var p=new Vector3(b.min.x-22,y,Mathf.Lerp(b.min.z+14,b.max.z-14,sector/3f));
                        if(sector==0)foreach(int k in new[]{-1,1})Monument(root,p+Vector3.forward*k*5,5);
                        else if(sector==1)BellTower(root,p,11,90);
                        else Mausoleum(root,p,7+sector*1.5f,90,sector==3);
                    }
                    Mausoleum(root,North(0,26),11,0,true);break;
                case 43:
                    foreach(int side in new[]{-1,1})Mausoleum(root,North(side*15,25),10,side*10,true);
                    Monument(root,new Vector3(b.min.x-15,y,b.min.z-12),5);break;
                case 44:
                    BellTower(root,North(0,25),17);
                    foreach(int side in new[]{-1,1})Monument(root,North(side*15,22),6);break;
                case 45:
                    for(int i=0;i<5;i++)Monument(root,new Vector3(b.min.x-13,y,b.min.z+i*b.size.z/4),3.5f+i*.4f,90);
                    Mausoleum(root,North(8,25),9,-15,false);break;
                case 46:
                    Mausoleum(root,North(12,26),11,-18,true);
                    for(int i=0;i<4;i++)Grave(root,"crypt-a",new Vector3(b.min.x-11-i%2*6,y,b.min.z+6+i*9),new Vector3(4,3,5),"Limestone",90);
                    Monument(root,North(-12,16),7);break;
                case 47:
                    foreach(int side in new[]{-1,1})
                    {
                        BellTower(root,new Vector3(b.center.x+side*(b.extents.x+20),y,b.min.z+18),12,side*90);
                        Mausoleum(root,new Vector3(b.center.x+side*(b.extents.x+22),y,b.max.z-10),8,side*90,false);
                    }
                    Monument(root,new Vector3(b.center.x,y,b.min.z-24),9);break;
                case 48:
                    for(int i=0;i<5;i++)Mausoleum(root,North((i-2)*25,25+(i%2)*9),9+i%3*2,(i-2)*8,i%2==0);
                    foreach(int side in new[]{-1,1})BellTower(root,new Vector3(b.center.x+side*(b.extents.x+25),y,b.center.z),17,side*90);break;
                case 49:
                    Mausoleum(root,North(0,30),8,0,false);
                    foreach(int side in new[]{-1,1})for(int i=0;i<3;i++)Lantern(root,new Vector3(b.center.x+side*(b.extents.x+9),y,b.min.z+6+i*12),3.2f);
                    Monument(root,North(-17,18),4.5f);break;
                case 50:
                    Mausoleum(root,North(0,34),17,0,true);
                    foreach(int side in new[]{-1,1})
                    {
                        BellTower(root,North(side*21,29),18,side*8);
                        Mausoleum(root,new Vector3(b.center.x+side*(b.extents.x+22),y,b.center.z),10,side*90,true);
                    }
                    break;
            }
        }

        private static void Enclosure(Transform root,Bounds b,float y)
        {
            var walls=new GameObject("Connected memorial enclosure").transform;walls.SetParent(root,false);
            foreach(int side in new[]{-1,1})
            {
                float x=b.center.x+side*(b.extents.x+5.5f);
                Shape(walls,"Continuous side garden wall",new Vector3(x,y,b.center.z),new Vector3(.55f,1.05f,b.size.z+11),Surface("Limestone",true));
                Shape(walls,"Side wall capping",new Vector3(x,y+1.05f,b.center.z),new Vector3(.75f,.18f,b.size.z+11.2f),Surface("Slate",true));
                foreach(int end in new[]{-1,1})
                {
                    float length=(b.size.x+11-7)*.5f;
                    var p=new Vector3(b.center.x+end*(3.5f+length*.5f),y,b.center.z+side*(b.extents.z+5.5f));
                    Shape(walls,"Returning cemetery wall",p,new Vector3(length,1.05f,.55f),Surface("Limestone",true));
                    Shape(walls,"Returning wall capping",p+Vector3.up*1.05f,new Vector3(length,.18f,.75f),Surface("Slate",true));
                    Grave(walls,"pillar-square",new Vector3(b.center.x+end*3.8f,y,p.z),new Vector3(1.1f,2.2f,1.1f),"Limestone");
                }
            }
        }

        private static void MemorialGarden(Transform root,Vector3 bottom,int seed,float yaw)
        {
            var garden=new GameObject("Memorial garden "+seed).transform;garden.SetParent(root,false);garden.localPosition=bottom;garden.localRotation=Quaternion.Euler(0,yaw,0);
            // A low border with deliberate overlap has no shared coplanar faces or raised floor overlay.
            Shape(garden,"Memorial retaining plinth",Vector3.zero,new Vector3(7,.25f,4.5f),Surface("Slate",true));
            for(int i=0;i<3;i++)
                Grave(garden,i%2==0?"gravestone-round":"gravestone-decorative",new Vector3((i-1)*2.1f,.25f,.7f),new Vector3(1.7f,1.8f+i%2*.6f,1.2f),"Limestone",(seed%3-1)*5);
            foreach(int side in new[]{-1,1})Grave(garden,"urn-round",new Vector3(side*2.7f,.25f,-1.4f),new Vector3(.75f,.9f,.75f),"Chalk");
            if(seed%3==0)Lantern(garden,new Vector3(-4,0,-1),2.4f);
        }

        private static void Mausoleum(Transform root,Vector3 bottom,float width,float yaw,bool grand)
        {
            var t=new GameObject(grand?"Grand mausoleum":"Memorial mausoleum").transform;t.SetParent(root,false);t.localPosition=bottom;t.localRotation=Quaternion.Euler(0,yaw,0);
            float height=width*.64f,depth=width*.78f;
            Shape(t,"Mausoleum foundation",Vector3.zero,new Vector3(width+1,.5f,depth+1),Surface("Slate",true));
            Shape(t,"Weathered stone chamber",Vector3.up*.5f,new Vector3(width,height,depth),Surface("Limestone",true));
            Shape(t,"Recessed iron door",new Vector3(0,.52f,-depth*.5f-.035f),new Vector3(width*.25f,height*.76f,.08f),Surface("Slate",true));
            foreach(int side in new[]{-1,1})
            {
                Shape(t,"Door jamb",new Vector3(side*width*.19f,.5f,-depth*.5f-.12f),new Vector3(width*.09f,height*.85f,.36f),Surface("Chalk",true));
                Grave(t,"pillar-square",new Vector3(side*width*.39f,.5f,-depth*.5f-.3f),new Vector3(width*.14f,height*.91f,width*.14f),"Limestone");
                Shape(t,"Quiet side memorial panel",new Vector3(side*(width*.5f+.02f),height*.38f,0),new Vector3(.05f,height*.36f,depth*.45f),Surface("Slate",true));
            }
            Shape(t,"Door lintel",new Vector3(0,height*.85f+.5f,-depth*.5f-.12f),new Vector3(width*.49f,.35f,.4f),Surface("Chalk",true));
            float eave=height+.5f;
            Shape(t,"Continuous cornice",Vector3.up*eave,new Vector3(width+.6f,.35f,depth+.6f),Surface("Limestone",true));
            // Closed pitched roof, with a continuous overhang on every side.
            var v=new List<Vector3>();var indices=new List<int>();float x=width*.5f+.45f,z=depth*.5f+.45f,top=eave+.35f+width*.28f,low=eave+.35f;
            Quad(v,indices,new Vector3(-x,low,-z),new Vector3(0,top,-z),new Vector3(0,top,z),new Vector3(-x,low,z));
            Quad(v,indices,new Vector3(0,top,-z),new Vector3(x,low,-z),new Vector3(x,low,z),new Vector3(0,top,z));
            Quad(v,indices,new Vector3(-x,low,z),new Vector3(x,low,z),new Vector3(x,low,-z),new Vector3(-x,low,-z));
            for(int i=0;i<indices.Count;i+=3)(indices[i+1],indices[i+2])=(indices[i+2],indices[i+1]);
            int n=v.Count;v.AddRange(new[]{new Vector3(-x,low,-z),new Vector3(x,low,-z),new Vector3(0,top,-z),new Vector3(x,low,z),new Vector3(-x,low,z),new Vector3(0,top,z)});indices.AddRange(new[]{n,n+2,n+1,n+3,n+5,n+4});
            MeshObject(t,"Closed slate pediment",SaveMesh("Mausoleum roof "+width.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture),v,indices),Surface("Slate",true));
            Grave(t,"detail-plate",new Vector3(0,height*.57f,-depth*.5f-.1f),new Vector3(width*.15f,.7f,.2f),"Ochre");
            if(grand)Grave(t,"pillar-obelisk",new Vector3(0,top,0),new Vector3(width*.16f,width*.42f,width*.16f),"Limestone");
            foreach(int side in new[]{-1,1})Lantern(t,new Vector3(side*(width*.5f+1.5f),0,-depth*.45f),2.8f);
        }

        private static void Monument(Transform root,Vector3 bottom,float height,float yaw=0)
        {
            var t=new GameObject("Ivory memorial monument").transform;t.SetParent(root,false);t.localPosition=bottom;
            Shape(t,"Memorial stone footing",Vector3.zero,new Vector3(height*.56f,.4f,height*.56f),Surface("Slate",true));
            Grave(t,"pillar-obelisk",Vector3.up*.4f,new Vector3(height*.4f,height,height*.4f),"Limestone",yaw);
        }

        private static void BellTower(Transform root,Vector3 bottom,float height,float yaw=0)
        {
            var t=new GameObject("Necropolis bell tower").transform;t.SetParent(root,false);t.localPosition=bottom;t.localRotation=Quaternion.Euler(0,yaw,0);
            float width=height*.31f;
            Shape(t,"Bell tower footing",Vector3.zero,new Vector3(width+1,.5f,width+1),Surface("Slate",true));
            Shape(t,"Bell tower masonry",Vector3.up*.5f,new Vector3(width,height*.66f,width),Surface("Limestone",true));
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Shape(t,"Open belfry pier",new Vector3(x*width*.4f,height*.66f,z*width*.4f),new Vector3(width*.2f,height*.22f,width*.2f),Surface("Limestone",true));
            Shape(t,"Belfry cornice",Vector3.up*(height*.88f),new Vector3(width+1,.4f,width+1),Surface("Slate",true));
            Grave(t,"detail-chalice",new Vector3(0,height*.70f,0),new Vector3(width*.5f,height*.12f,width*.5f),"Ochre");
            Grave(t,"pillar-obelisk",Vector3.up*(height*.88f+.4f),new Vector3(width*.7f,height*.25f,width*.7f),"Limestone");
        }

        private static void Lantern(Transform root,Vector3 bottom,float height)
        {
            Grave(root,"lightpost-single",bottom,new Vector3(height*.55f,height,height*.55f),"Slate");
            // Painted ivory glass gives a quiet warm cue without lights, flames or emission.
            Shape(root,"Lantern painted glass",bottom+Vector3.up*(height*.74f),new Vector3(.24f,.32f,.24f),Surface("Chalk",true));
        }

        private static void Tree(Transform root,Vector3 bottom,float height,float yaw,bool distant=false)
            => Model(root,Graves,"pine-crooked",bottom,new Vector3(height*.6f,height,height*.6f),Surface("Sage",true,distant),yaw);
        private static GameObject Grave(Transform root,string name,Vector3 bottom,Vector3 envelope,string kind,float yaw=0)
            =>Model(root,Graves,name,bottom,envelope,Surface(kind,true),yaw);

        private static void Landscape(Transform root,Bounds b,float y,int stage)
        {
            var v=new List<Vector3>();var t=new List<int>();const int cells=64;
            for(int z=0;z<=cells;z++)for(int x=0;x<=cells;x++)v.Add(new Vector3(x/(float)cells-.5f,0,z/(float)cells-.5f));
            for(int z=0;z<cells;z++)for(int x=0;x<cells;x++){int i=z*(cells+1)+x;t.AddRange(new[]{i,i+cells+1,i+1,i+1,i+cells+1,i+cells+2});}
            var ground=MeshObject(root,"Continuous necropolis earth",SaveMesh("Subdivided necropolis earth",v,t),Surface("Earth"));
            ground.transform.localPosition=new Vector3(b.center.x,y,b.center.z);ground.transform.localScale=new Vector3(b.size.x+500,1,b.size.z+500);
            v=new List<Vector3>();t=new List<int>();const int segments=72;
            Vector3 P(int i,float distance,float h)
            {
                float a=i*Mathf.PI*2/segments,s=Mathf.Sin(a),c=Mathf.Cos(a);
                return new Vector3(b.center.x+Mathf.Sign(s)*Mathf.Pow(Mathf.Abs(s),.5f)*(b.extents.x+distance),y+h,b.center.z+Mathf.Sign(c)*Mathf.Pow(Mathf.Abs(c),.5f)*(b.extents.z+distance));
            }
            for(int i=0;i<segments;i++)
            {
                float h=8+Mathf.Sin(i*.41f+stage)*2,h2=8+Mathf.Sin((i+1)*.41f+stage)*2;
                Quad(v,t,P(i,45,0),P(i,62,h),P(i+1,62,h2),P(i+1,45,0));
                Quad(v,t,P(i,62,h),P(i,100,0),P(i+1,100,0),P(i+1,62,h2));
            }
            MeshObject(root,"Continuous necropolis ridge",SaveMesh("Necropolis ridge "+stage,v,t),Surface("Sage",true,true));
            for(int i=0;i<24;i++)Tree(root,P(i*3,55,0),13+i%4*2,i*47+stage,true);
        }
    }
}
