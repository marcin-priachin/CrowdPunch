using UnityEngine;
using static CrowdPunch.Editor.ChapterTwoFoundryAssets;
using static CrowdPunch.Editor.ChapterTwoFoundryInfrastructure;
using static CrowdPunch.Editor.ChapterTwoFoundryBlocks;

namespace CrowdPunch.Editor
{
    // A working factory compound: the court is an open handling yard within connected buildings.
    internal static class ChapterTwoFoundryScenery
    {
        internal static void Build(Transform root, Bounds court, int stage)
        {
            float y=court.max.y-.12f;
            float left=court.min.x,right=court.max.x,front=court.min.z,back=court.max.z;
            GroundPlane(root,new Vector3(court.center.x,y,court.center.z),new Vector3(court.size.x+484,1,court.size.z+484));
            ServiceAprons(root,court,y);

            // Touching structural bays form one factory frontage, not separate exhibit pieces.
            int bays=Mathf.Max(3,Mathf.CeilToInt((court.size.x+16)/15));
            if(stage==13 && bays%2==0) bays++;
            float bayWidth=(court.size.x+16)/bays;
            for(int i=0;i<bays;i++)
            {
                float x=court.center.x+(i-(bays-1)*.5f)*bayWidth;
                if(stage==13 && i==bays/2)continue;
                float h=6+((stage+i)%3)*1.1f;
                bool furnace=stage==11 && i==bays/2 || stage==20 || stage==18 && i%3==1;
                bool dock=i%2==0 && stage!=13;
                float workshopWidth=bayWidth-.10f;
                Workshop(root,new Vector3(x,y,back+11),new Vector3(workshopWidth,h,14),furnace,joined:true,
                    entranceBase:dock ? .45f : .20f,loadingDock:dock);
                if(dock) WorkshopDock(root,new Vector3(x,y,back+4),stage==12);
                Shape(root,"Header cantilever bracket",new Vector3(x-workshopWidth*.43f,y+3.81f,back+3.5f),
                    new Vector3(.28f,.24f,1),Steel);
            }

            // One returning wing makes an L-shaped service yard. Foreground heights stay low.
            int wingSide=stage%2==0?-1:1;
            float wingLength=Mathf.Clamp(court.size.z*.36f,9,30);
            float wingX=wingSide<0?left-13.5f:right+13.5f;
            Workshop(root,new Vector3(wingX,y,back+3.9f-wingLength*.5f),new Vector3(wingLength,4.4f,8),false,
                yaw:wingSide*90);

            // A continuous pipe header links the process bays. Curved knees and round flanges
            // keep diameter independent of run length. Return into the end bays below the windows.
            Pipe(root,.52f,new Vector3(left-6.5f,y+3.48f,back+4.02f),new Vector3(left-6.5f,y+4.3f,back+3),
                new Vector3(right+6.5f,y+4.3f,back+3),new Vector3(right+6.5f,y+3.48f,back+4.02f));

            switch(stage)
            {
                case 11:
                    Fuel(root,new Vector3(left-5.5f,y,front+5),2);
                    LoadingBay(root,new Vector3(right+6,y,front+6),5,false);
                    Pipe(root,.65f,new Vector3(left-6,y+1.1f,front+10),new Vector3(left-6,y+1.1f,back+2),new Vector3(left-6,y+4.3f,back+3));
                    break;
                case 12:
                    Tank(root,new Vector3(left-6,y,court.center.z-4),6,4.2f);
                    Tank(root,new Vector3(right+6,y,court.center.z-1),8,5);
                    BoilerConnections(root,court,y,court.center.z-4,court.center.z-1);
                    LoadingBay(root,new Vector3(right+7,y,front+4),4,true);
                    break;
                case 13:
                    Workshop(root,new Vector3(left-14,y,back+4),new Vector3(10,10,15),true);
                    Shape(root,"Exit service bridge",new Vector3(court.center.x,y+5.8f,back+11),new Vector3(10,.7f,3),Steel);
                    LoadingBay(root,new Vector3(left-6,y,front+5),4,false);
                    break;
                case 14:
                    for(int side=-1;side<=1;side+=2)
                    {
                        float x=side<0?left-4.5f:right+4.5f;
                        for(int line=0;line<2;line++)
                        {
                            float px=x+side*line*1.25f;
                            Pipe(root,.65f,new Vector3(px,y+.9f,front+3),new Vector3(px,y+.9f,back+1),new Vector3(px,y+3,back+3),new Vector3(px,y+3,back+4.02f));
                        }
                        for(float z=front+4;z<back;z+=6)Shape(root,"Pump pipe saddle",new Vector3(x+side*.65f,y,z),new Vector3(2.7f,.58f,.6f),Steel);
                        Model(root,"machine-fortified",new Vector3(x+side*.6f,y,front+1),new Vector3(3,3,3),Teal);
                    }
                    break;
                case 15:
                    Tank(root,new Vector3(left-6,y,court.center.z-4),10,5.5f);
                    Tank(root,new Vector3(right+6,y,front+6),5.8f,4);
                    BoilerConnections(root,court,y,court.center.z-4,front+6);
                    Gantry(root,new Vector3(court.center.x,y,back+1),court.size.x+17.6f,6);
                    break;
                case 16:
                {
                    LoadingBay(root,new Vector3(left-6,y,front+5),6,false);
                    Model(root,"crane",new Vector3(right+13,y,court.center.z),new Vector3(9,11,10),Brass);
                    float cargoEnd=Cargo(root,new Vector3(right+5,y,front+4),6);
                    Quay(root,new Vector3(right+6,y,court.center.z),court.size.z*.7f,cargoEnd);
                    break;
                }
                case 17:
                    Tank(root,new Vector3(left-6,y,court.center.z-3),6.5f,4.4f);
                    Tank(root,new Vector3(right+6,y,court.center.z-3),6.5f,4.4f);
                    BoilerConnections(root,court,y,court.center.z-3,court.center.z-3);
                    Model(root,"cog-e",new Vector3(court.center.x,y+8,back+3.8f),new Vector3(3.5f,3.5f,1),Brass);
                    break;
                case 18:
                    Tank(root,new Vector3(right+6,y,court.center.z+10),9,6);
                    LoadingBay(root,new Vector3(left-6,y,court.center.z),6,true);
                    LoadingBay(root,new Vector3(right+6,y,front+8),6,false);
                    Fuel(root,new Vector3(left-5.5f,y,front+7),2);
                    Pipe(root,.7f,new Vector3(right+6,y+3,court.center.z+10),new Vector3(right+6,y+3,back+3),
                        new Vector3(right+6,y+4.3f,back+3));
                    break;
                case 19:
                {
                    LoadingBay(root,new Vector3(left-6,y,front+6),6,false);
                    float cargoEnd=Cargo(root,new Vector3(right+5,y,front+5),8);
                    Gantry(root,new Vector3(court.center.x,y,back+1),court.size.x+17.6f,6);
                    Model(root,"crane-lift",new Vector3(right+13,y,court.center.z),new Vector3(6,8,6),Teal);
                    Quay(root,new Vector3(right+6,y,court.center.z),court.size.z*.65f,cargoEnd);
                    break;
                }
                case 20:
                    Tank(root,new Vector3(left-6,y,court.center.z-3),7,5);
                    Tank(root,new Vector3(right+6,y,court.center.z-3),7,5);
                    BoilerConnections(root,court,y,court.center.z-3,court.center.z-3);
                    Fuel(root,new Vector3(left-6,y,front+4),2);
                    break;
            }

            Surround(root,court,y,stage);
            SeparateDrainEnds(root);
        }

        private static void BoilerConnections(Transform root,Bounds court,float y,float leftTankZ,float rightTankZ)
        {
            foreach(int side in new[]{-1,1})
            {
                float x=side<0?court.min.x-6:court.max.x+6;
                float tankZ=side<0?leftTankZ:rightTankZ;
                Pipe(root,.6f,new Vector3(x,y+2.5f,tankZ),new Vector3(x,y+2.5f,court.max.z+3),new Vector3(x,y+4.3f,court.max.z+3));
            }
        }

        private static void Quay(Transform root,Vector3 p,float length,float cargoEnd)
        {
            float start=Mathf.Max(p.z-length*.5f,cargoEnd+.25f);
            float end=p.z+length*.5f;
            foreach(int side in new[]{-1,1})
                Shape(root,"Freight siding rail",new Vector3(p.x+side*1.5f,p.y+.28f,(start+end)*.5f),
                    new Vector3(.12f,.12f,end-start),Steel);
            for(float z=start+.15f;z<=end-.15f;z+=2)
                Shape(root,"Freight siding sleeper",new Vector3(p.x,p.y+.2f,z),new Vector3(3.8f,.08f,.3f),Coal);
        }

        private static void Tank(Transform root,Vector3 p,float height,float width)
        {
            Shape(root,"Tank plinth",p,new Vector3(width+.7f,.4f,width+.7f),Steel);
            Shape(root,"Riveted boiler",p+Vector3.up*.4f,new Vector3(width,height,width),Teal,true);
            foreach(float t in new[]{.1f,.5f,.9f})Shape(root,"Copper tank band",p+Vector3.up*(height*t),new Vector3(width+.12f,.18f,width+.12f),Copper,true);
            Model(root,"pipe-large-valve",p+new Vector3(0,1,-width*.5f),new Vector3(1.2f,1.2f,1.2f),Brass);
            Shape(root,"Tank cap",p+Vector3.up*height,new Vector3(width+.3f,.25f,width+.3f),Steel,true);
        }

        private static void Fuel(Transform root,Vector3 p,int count)
        {
            for(int i=0;i<count;i++)
            {
                var bay=p+Vector3.forward*(i-(count-1)*.5f)*3.8f;
                Shape(root,"Coal bunker slab",bay,new Vector3(5,.35f,3.6f),Steel);
                Shape(root,"Coal bunker back",bay+new Vector3(-2.3f,.35f,0),new Vector3(.25f,1.4f,3.6f),Brick);
                foreach(int side in new[]{-1,1})Shape(root,"Coal bunker divider",bay+new Vector3(.0375f,.35f,side*1.7f),new Vector3(4.425f,1.2f,.2f),Brick);
                for(int j=0;j<3;j++)Shape(root,"Coal pile",bay+new Vector3(j-1,.35f,0),new Vector3(1.7f,.55f+j*.1f,2.7f),Coal,true);
            }
        }
        private static float Cargo(Transform root,Vector3 p,int count)
        {
            const float rowSpacing=2.4f;
            const float crateWidth=2.1f;
            for(int i=0;i<count;i++)
                Model(root,"box-large",p+new Vector3((i%2)*2.3f,0,(i/2)*rowSpacing),new Vector3(crateWidth,2,crateWidth),
                    i%2==0?Copper:Teal,(i%2)*90);
            return p.z+((count-1)/2)*rowSpacing+crateWidth*.5f;
        }
        private static void Gantry(Transform root,Vector3 p,float width,float height)
        {
            foreach(int side in new[]{-1,1})Shape(root,"Gantry steel leg",p+new Vector3(side*width*.5f,0,0),new Vector3(.55f,height,.75f),Steel);
            Shape(root,"Background service gantry",p+Vector3.up*(height-.3f),new Vector3(width+.5f,.35f,1.8f),Steel);
            foreach(int side in new[]{-1,1})
            {
                Shape(root,"Service bridge handrail",p+new Vector3(0,height+.9f,side*.8f),new Vector3(width,.09f,.09f),Brass);
                for(float x=-width*.5f;x<=width*.5f;x+=2)Shape(root,"Service bridge baluster",p+new Vector3(x,height,side*.8f),new Vector3(.08f,1,.08f),Steel);
            }
        }
    }
}
