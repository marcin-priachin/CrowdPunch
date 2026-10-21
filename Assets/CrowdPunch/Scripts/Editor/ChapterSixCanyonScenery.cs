using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static CrowdPunch.Editor.ChapterSixCanyonAssets;

namespace CrowdPunch.Editor
{
    // VISION-005/006: complete canyon layers stay outside the authored combat envelope.
    internal static class ChapterSixCanyonScenery
    {
        internal static void Build(Transform root,Bounds[] floors,int stage)
        {
            var b=floors[0];foreach(var floor in floors.Skip(1))b.Encapsulate(floor);
            float y=floors.Min(f=>f.max.y)-.12f;
            Landscape(root,b,y,stage);
            int rows=Mathf.Max(3,Mathf.CeilToInt(b.size.z/21));
            foreach(int side in new[]{-1,1})for(int i=0;i<rows;i++)
            {
                float z=Mathf.Lerp(b.min.z-4,b.max.z+4,(i+.5f)/rows);
                var p=new Vector3(b.center.x+side*(b.extents.x+14),y,z);
                Store(root,p,5.5f,side*90,(i+stage)%3==0);
                Supplies(root,p+new Vector3(-side*5.8f,0,2),i+stage);
                if(stage!=59 || i%2==0)Rock(root,p+new Vector3(side*15,0,7),10+i%3*3,i*31+stage);
            }
            foreach(int side in new[]{-1,1})
            {
                // Returning walls connect the service yards without crossing the entry/exit lane.
                Shape(root,"Low sandstone yard return",new Vector3(b.center.x+side*(b.extents.x+5),y,b.center.z),new Vector3(.65f,.85f,b.size.z+10),Surface("Limestone",true));
                int count=Mathf.Max(3,Mathf.CeilToInt(b.size.x/23));
                for(int i=0;i<count;i++)
                {
                    var p=new Vector3(Mathf.Lerp(b.min.x-6,b.max.x+6,(i+.5f)/count),y,b.center.z+side*(b.extents.z+19));
                    if(i%2==0)Store(root,p,5.5f,side<0?180:0,false);else Cistern(root,p,5);
                    Rock(root,p+new Vector3(6,0,side*21),13,i*29+stage);
                }
            }
            Vector3 North(float x=0,float z=32)=>new Vector3(b.center.x+x,y,b.max.z+z);
            switch(stage)
            {
                case 51:
                    Cistern(root,North(),10);
                    foreach(int side in new[]{-1,1})Store(root,North(side*13,23),7,side*12,false);break;
                case 52:
                    Tower(root,North(-17),14,12);Store(root,North(13,25),8,-15,true);break;
                case 53:
                    Windwheel(root,North(),14);
                    foreach(int side in new[]{-1,1})Store(root,North(side*14,24),6,side*15,false);break;
                case 54:
                    Cistern(root,North(-12),8);Tower(root,North(15,38),13,-15);
                    for(int i=0;i<4;i++)Supplies(root,new Vector3(b.min.x-10,y,b.min.z+8+i*12),i);break;
                case 55:
                    Tower(root,North(-19,28),17,-18);Store(root,North(16,33),9,16,true);Rock(root,North(6,48),22,21);break;
                case 56:
                    // Five scenic destinations follow the five existing sectors.
                    for(int sector=0;sector<5;sector++)
                    {
                        var f=floors.OrderBy(f=>f.center.z).ElementAt(Mathf.Min(sector,floors.Length-1));
                        var p=new Vector3(b.min.x-22,y,f.center.z);
                        if(sector==0){Store(root,p,7,90,false);Supplies(root,p+Vector3.forward*8,sector);}
                        else if(sector==1)Cistern(root,p,9);
                        else if(sector==2)Rock(root,p,19,56);
                        else if(sector==3){Store(root,p,8,90,true);Windwheel(root,p+Vector3.forward*13,10);}
                        else Tower(root,p,17,90);
                    }
                    Tower(root,North(0,30),19,0);break;
                case 57:
                    foreach(int side in new[]{-1,1})
                    {
                        Cistern(root,new Vector3(b.center.x+side*(b.extents.x+19),y,b.max.z-7),8);
                        for(int i=0;i<3;i++)Model(root,"kenney_nature-kit","tree_palmDetailedTall",new Vector3(b.center.x+side*(b.extents.x+28),y,b.max.z-12+i*9),new Vector3(8,11+i,8),Surface("Sage",true),side*35+i*27);
                    }
                    Store(root,North(),9,0,false);break;
                case 58:
                    foreach(int side in new[]{-1,1}){Rock(root,North(side*35,48),30,side*23);Tower(root,North(side*16,28),14,side*15);}break;
                case 59:
                    Rock(root,North(-22,40),28,15);Rock(root,North(22,54),34,-20);Store(root,North(0,25),5,0,true);break;
                case 60:
                    foreach(int side in new[]{-1,1}){Tower(root,North(side*24,33),21,side*12);Store(root,North(side*14,23),8,side*10,false);}
                    Windwheel(root,North(0,49),18);break;
            }
        }

        private static void Store(Transform parent,Vector3 bottom,float size,float yaw,bool ruined)
        {
            var root=new GameObject(ruined?"Wind-eroded outpost":"Caravan service store").transform;root.SetParent(parent,false);root.localPosition=bottom;root.localRotation=Quaternion.Euler(0,yaw,0);
            float height=size*.65f;
            Shape(root,"Sandstone foundation",Vector3.zero,new Vector3(size+.35f,.4f,size*.8f+.35f),Surface("Limestone",true));
            Shape(root,"Recessed dark doorway",new Vector3(0,.4f,-size*.4f+.13f),new Vector3(1.45f,2.3f,.18f),Surface("Slate",true));
            foreach(int side in new[]{-1,1})
            {
                Shape(root,"Door frontage pier",new Vector3(side*(size+1.5f)*.25f,.4f,-size*.4f),new Vector3((size-1.5f)*.5f,height,.35f),Surface("Limestone",true));
                Shape(root,"Returning sandstone wing",new Vector3(side*(size*.5f-.18f),.4f,0),new Vector3(.36f,height,size*.8f-.38f),Surface("Limestone",true));
                Shape(root,"Awning timber post",new Vector3(side*(size*.48f),0,-size*.4f-2),new Vector3(.18f,3.1f,.18f),Surface("Timber",true));
            }
            Shape(root,"Closed rear facade",new Vector3(0,.4f,size*.4f),new Vector3(size,height,.36f),Surface("Limestone",true));
            Shape(root,"Doorway lintel",new Vector3(0,2.7f,-size*.4f),new Vector3(1.5f,height-2.3f,.35f),Surface("Limestone",true));
            if(!ruined)Shape(root,"Continuous fitted flat roof",new Vector3(0,height+.4f,0),new Vector3(size+.3f,.25f,size*.8f+.3f),Surface("Limestone",true));
            else foreach(int side in new[]{-1,1})Shape(root,"Broken parapet",new Vector3(side*size*.35f,height+.4f,0),new Vector3(size*.2f,.38f,size*.8f),Surface("Rock",true));
            // Faded cloth is inert decoration, below the distant roof silhouette.
            var awning=Shape(root,"Faded blue canvas awning",new Vector3(0,3.05f,-size*.4f-1),new Vector3(size+.12f,.075f,2.3f),Surface("Fabric",true));
            awning.transform.localRotation=Quaternion.Euler(-6,0,0);
            for(int i=0;i<4;i++)
            {
                var seam=Shape(root,"Drawn canvas seam",Vector3.zero,new Vector3(.025f,.015f,2.15f),Surface("Slate",true)).transform;
                ChapterSixCanyonAwningSeams.Align(awning.transform,seam,(i-1.5f)*size/4);
            }
            Supplies(root,new Vector3(size*.35f,0,-size*.4f-1),Mathf.RoundToInt(size));
            CloseStoreWallJoints(root);
        }

        internal static void CloseStoreWallJoints(Transform store)
        {
            var front=store.GetComponentsInChildren<Transform>().First(t=>t.name=="Door frontage pier");
            var rear=store.Find("Closed rear facade");
            float start=front.localPosition.z+front.localScale.z*.5f;
            float end=rear.localPosition.z-rear.localScale.z*.5f;
            foreach(var wing in store.GetComponentsInChildren<Transform>().Where(t=>t.name=="Returning sandstone wing"))
            {
                var position=wing.localPosition;position.z=(start+end)*.5f;wing.localPosition=position;
                var size=wing.localScale;size.z=end-start;wing.localScale=size;
            }
        }

        private static void Cistern(Transform root,Vector3 p,float size)
        {
            var group=new GameObject("Sandstone water cistern").transform;group.SetParent(root,false);group.localPosition=p;
            Shape(group,"Cistern stone basin",Vector3.zero,new Vector3(size,size*.62f,size),Surface("Limestone",true),true);
            Shape(group,"Cistern covered cap",Vector3.up*size*.62f,new Vector3(size*1.07f,.3f,size*1.07f),Surface("Slate",true),true);
            foreach(int side in new[]{-1,1})Shape(group,"Cistern brace",new Vector3(side*size*.48f,0,0),new Vector3(.45f,size*.68f,1.1f),Surface("Ochre",true));
            Shape(group,"Quiet blue cistern lid",Vector3.up*(size*.62f+.31f),new Vector3(size*.46f,.12f,size*.46f),Surface("Fabric",true),true);
        }

        private static void Tower(Transform root,Vector3 p,float height,float yaw)
        {
            var group=new GameObject("Clifftop signal tower").transform;group.SetParent(root,false);group.localPosition=p;group.localRotation=Quaternion.Euler(0,yaw,0);
            float width=height*.28f;
            Shape(group,"Signal tower footing",Vector3.zero,new Vector3(width+1,.7f,width+1),Surface("Rock",true));
            Shape(group,"Signal tower masonry",Vector3.up*.7f,new Vector3(width,height-.7f,width),Surface("Limestone",true));
            Shape(group,"Signal tower cornice",Vector3.up*height,new Vector3(width+.7f,.45f,width+.7f),Surface("Ochre",true));
            foreach(int side in new[]{-1,1})
            {
                Shape(group,"Signal window",new Vector3(side*(width*.5f+.015f),height*.75f,0),new Vector3(.025f,1.2f,.55f),Surface("Slate",true));
                Shape(group,"Signal blue banner",new Vector3(side*(width*.5f+.045f),height*.35f,0),new Vector3(.04f,height*.25f,1.15f),Surface("Fabric",true));
                Shape(group,"Tower parapet",new Vector3(side*(width*.5f),height+.45f,0),new Vector3(.35f,.7f,width+.7f),Surface("Limestone",true));
            }
        }

        private static void Windwheel(Transform root,Vector3 p,float height)
        {
            Tower(root,p,height*.7f,0);
            var hub=p+new Vector3(0,height*.73f,-height*.16f-.25f);
            Shape(root,"Stationary windwheel hub",hub,new Vector3(.65f,.65f,.6f),Surface("Ochre",true),true);
            // Stationary machinery has no swept volume intersecting architecture or objectives.
            for(int i=0;i<6;i++)
            {
                var rotation=Quaternion.Euler(0,0,i*60);
                var blade=Shape(root,"Windwheel blue sail",hub+rotation*Vector3.up*(height*.19f)-Vector3.up*(height*.28f)*.5f,new Vector3(.65f,height*.28f,.12f),Surface("Fabric",true));
                blade.transform.localRotation=rotation;
            }
        }

        private static void Supplies(Transform root,Vector3 p,int seed)
        {
            for(int i=0;i<3;i++)
            {
                var q=p+new Vector3((i-1)*1.2f,0,i%2*.6f);
                Shape(root,"Caravan supply chest",q,new Vector3(1.05f,.85f,1),Surface("Timber",true));
                foreach(int side in new[]{-1,1})Shape(root,"Chest iron straps",q+new Vector3(side*.32f,.86f,0),new Vector3(.08f,.05f,1.02f),Surface("Slate",true));
            }
            Model(root,"kenney_nature-kit",seed%2==0?"cactus_short":"cactus_tall",p+new Vector3(2.8f,0,1),new Vector3(2,2.6f,2),Surface("Sage",true),seed*17);
        }

        private static void Rock(Transform root,Vector3 p,float size,float yaw)
            =>Model(root,"kenney_nature-kit","rock_largeC",p,new Vector3(size*1.5f,size,size),Surface("Cliff",true),yaw);

        private static void Landscape(Transform root,Bounds b,float y,int stage)
        {
            var v=new List<Vector3>();var t=new List<int>();const int cells=64;
            for(int z=0;z<=cells;z++)for(int x=0;x<=cells;x++)v.Add(new Vector3(x/(float)cells-.5f,0,z/(float)cells-.5f));
            for(int z=0;z<cells;z++)for(int x=0;x<cells;x++){int i=z*(cells+1)+x;t.AddRange(new[]{i,i+cells+1,i+1,i+1,i+cells+1,i+cells+2});}
            var earth=MeshObject(root,"Continuous canyon earth",SaveMesh("Subdivided canyon earth",v,t),Surface("Earth"));
            earth.transform.localPosition=new Vector3(b.center.x,y,b.center.z);earth.transform.localScale=new Vector3(b.size.x+500,1,b.size.z+500);
            v=new List<Vector3>();t=new List<int>();const int segments=64;
            Vector3 P(int i,float distance,float h)
            {
                float a=i*Mathf.PI*2/segments,s=Mathf.Sin(a),c=Mathf.Cos(a);
                return new Vector3(b.center.x+Mathf.Sign(s)*Mathf.Pow(Mathf.Abs(s),.5f)*(b.extents.x+distance),y+h,b.center.z+Mathf.Sign(c)*Mathf.Pow(Mathf.Abs(c),.5f)*(b.extents.z+distance));
            }
            for(int i=0;i<segments;i++)
            {
                float h=18+Mathf.Floor((Mathf.Sin(i*.45f+stage)+1)*3)*2,h2=18+Mathf.Floor((Mathf.Sin((i+1)*.45f+stage)+1)*3)*2;
                Quad(v,t,P(i,44,0),P(i,57,h),P(i+1,57,h2),P(i+1,44,0));
                Quad(v,t,P(i,57,h),P(i,77,h),P(i+1,77,h2),P(i+1,57,h2));
                Quad(v,t,P(i,77,h),P(i,120,0),P(i+1,120,0),P(i+1,77,h2));
            }
            MeshObject(root,"Continuous canyon mesa rim",SaveMesh("Canyon mesa rim "+stage,v,t),Surface("Cliff",true,true));
        }
    }
}
