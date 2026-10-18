using UnityEngine;
using static CrowdPunch.Editor.ChapterTwoFoundryAssets;

namespace CrowdPunch.Editor
{
    // Chapter-owned compositions. All heights are grounded on the flat exterior terrace.
    internal static class ChapterTwoFoundryScenery
    {
        internal static void Build(Transform root, Bounds court, int stage)
        {
            float y=court.max.y-.12f;
            // Exterior ground continues below the existing court, with 12 cm separation.
            // The original collision-aligned floor remains the only visible combat surface.
            float reach=240, x0=court.min.x-2, x1=court.max.x+2, z0=court.min.z-2,z1=court.max.z+2;
            GroundPlane(root,new Vector3(court.center.x,y,court.center.z),new Vector3(x1-x0+reach*2,1,z1-z0+reach*2));

            // Near service clusters are low. Taller masses remain beyond the playable bounds.
            for(int side=-1;side<=1;side+=2)
            for(int row=0;row<3;row++)
            {
                float x=side<0?x0-4:x1+4;
                float z=Mathf.Lerp(z0+4,z1-4,(row+.5f)/3);
                Model(root,(stage+row)%2==0?"box-large":"machine-fortified",new Vector3(x,y,z),new Vector3(2.8f,1.3f,2.4f),row%2==0?Teal:Steel,side*90);
                Model(root,"pipe-large-long",new Vector3(x+side*3,y,z),new Vector3(1.4f,1.2f,6),Copper);
            }

            float back=z1+17;
            switch(stage)
            {
                case 11:
                    Workshop(root,new Vector3(court.center.x,y,back),new Vector3(18,8,11),true);
                    Fuel(root,new Vector3(x0-7,y,z1-1),3); Fuel(root,new Vector3(x1+8,y,z0+6),2);
                    break;
                case 12:
                    Tank(root,new Vector3(x0-10,y,z1-3),8,5); Tank(root,new Vector3(x1+11,y,z1+3),11,6);
                    Workshop(root,new Vector3(court.center.x+5,y,back+5),new Vector3(14,5,9),false);
                    Model(root,"robot-arm-b",new Vector3(x0-6,y,court.center.z),new Vector3(4,4,4),Brass);
                    break;
                case 13:
                    Workshop(root,new Vector3(x0-10,y,z1-6),new Vector3(12,9,13),true);
                    Workshop(root,new Vector3(x1+10,y,z1-6),new Vector3(12,6,13),false);
                    Tank(root,new Vector3(x1+9,y,z0+8),6,5);
                    break;
                case 14:
                    for(int side=-1;side<=1;side+=2)
                    {
                        float x=side<0?x0-10:x1+10;
                        for(int i=0;i<3;i++) Model(root,"pipe-large-long",new Vector3(x+i*1.6f,y+1,court.center.z),new Vector3(1.3f,1.3f,court.size.z+12),Copper);
                        foreach(int end in new[]{-1,1})
                            Shape(root,"Pump pipe support",new Vector3(x+1.6f,y,court.center.z+end*court.size.z*.35f),new Vector3(5.8f,1,1.1f),Steel);
                        Workshop(root,new Vector3(x,y,z1+9),new Vector3(9,4,8),false);
                    }
                    Tank(root,new Vector3(court.center.x,y,back+8),12,8);
                    break;
                case 15:
                    Tank(root,new Vector3(x0-12,y,z1-4),13,8); Tank(root,new Vector3(x1+9,y,z0+7),7,5);
                    Gantry(root,new Vector3(court.center.x,y,back),court.size.x+22,9);
                    break;
                case 16:
                    Workshop(root,new Vector3(x0-13,y,court.center.z),new Vector3(14,6,22),false);
                    Model(root,"crane",new Vector3(x1+12,y,z1-3),new Vector3(11,14,10),Brass);
                    Cargo(root,new Vector3(x1+8,y,z0+5),4);
                    break;
                case 17:
                    Tank(root,new Vector3(x0-11,y,z1),9,5); Tank(root,new Vector3(x1+11,y,z1),9,5);
                    Workshop(root,new Vector3(court.center.x,y,back),new Vector3(15,7,10),false);
                    Model(root,"cog-e",new Vector3(court.center.x,y+8,back),new Vector3(5,5,1),Brass,0);
                    break;
                case 18:
                    for(int i=0;i<4;i++) Workshop(root,new Vector3(Mathf.Lerp(x0,x1,i/3f),y,back+((i%2)*8)),new Vector3(18,7+i*2,14),i%2==0);
                    Workshop(root,new Vector3(x0-20,y,court.center.z),new Vector3(15,10,26),true);
                    Tank(root,new Vector3(x1+18,y,court.center.z+10),15,10);
                    Fuel(root,new Vector3(x0-10,y,z0+12),4);
                    break;
                case 19:
                    Cargo(root,new Vector3(x0-8,y,z0+8),3); Cargo(root,new Vector3(x1+8,y,z1-5),5);
                    Gantry(root,new Vector3(court.center.x,y,back),court.size.x+15,8);
                    Model(root,"crane-lift",new Vector3(x1+12,y,court.center.z),new Vector3(8,10,7),Teal);
                    break;
                case 20:
                    for(int i=0;i<3;i++) Workshop(root,new Vector3(court.center.x+(i-1)*19,y,back+Mathf.Abs(i-1)*6),new Vector3(14,7,10),true);
                    Tank(root,new Vector3(x0-12,y,court.center.z),8,7); Tank(root,new Vector3(x1+12,y,court.center.z),8,7);
                    break;
            }

            // Distant skyline on every side; height and spacing change with the composition.
            for(int side=0;side<4;side++)
            for(int i=0;i<4;i++)
            {
                float t=(i+.5f)/4, h=7+(stage*7+i*3+side)%8;
                Vector3 p=side<2?new Vector3(side==0?x0-42:x1+42,y,Mathf.Lerp(z0-25,z1+25,t)):
                    new Vector3(Mathf.Lerp(x0-35,x1+35,t),y,side==2?z0-44:z1+45);
                Workshop(root,p,new Vector3(12,h,13),false);
                Chimney(root,p+new Vector3(3,0,3),h+7+(stage+i)%5,2.4f);
            }
        }

        private static void Workshop(Transform root,Vector3 p,Vector3 size,bool furnace)
        {
            Shape(root,"Foundry brick workshop",p,size,Brick);
            Shape(root,"Standing seam roof",p+Vector3.up*size.y,new Vector3(size.x+1,.5f,size.z+1),Teal);
            Roof(root,p+Vector3.up*(size.y+.45f),new Vector3(size.x+1,1.4f,size.z+1));
            for(int i=-1;i<=1;i++)
            {
                Shape(root,"Steel facade pier",p+new Vector3(i*size.x*.37f,0,-size.z*.5f-.12f),new Vector3(.45f,size.y,.5f),Steel);
                Shape(root,"High clerestory window",p+new Vector3(i*size.x*.27f,size.y*.66f,-size.z*.5f-.08f),new Vector3(size.x*.18f,size.y*.16f,.12f),Ivory);
            }
            Model(root,"door-wide-closed",p+new Vector3(0,0,-size.z*.5f-.2f),new Vector3(4,3.8f,.4f),Steel);
            foreach(int side in new[]{-1,1})
            {
                Shape(root,"Copper drainpipe",p+new Vector3(side*(size.x*.5f-.35f),0,-size.z*.5f-.2f),new Vector3(.15f,size.y,.15f),Copper,true);
                Shape(root,"Workshop stone footing",p+new Vector3(side*size.x*.35f,0,-size.z*.5f-.2f),new Vector3(size.x*.28f,.38f,.5f),Steel);
            }
            if(furnace)
            {
                Chimney(root,p+new Vector3(size.x*.28f,0,size.z*.15f),size.y+10,3);
                Model(root,"hopper-high-round",p+new Vector3(-size.x*.26f,size.y,0),new Vector3(4,4.5f,4),Copper);
            }
        }

        private static void Chimney(Transform root,Vector3 p,float height,float width)
        {
            Shape(root,"Tapered chimney footing",p,new Vector3(width+1,2,width+1),Steel);
            Shape(root,"Smelter chimney",p+Vector3.up*1.8f,new Vector3(width,height-1.8f,width),Brick,true);
            foreach(float fraction in new[]{.35f,.70f,1f}) Shape(root,"Chimney iron band",p+Vector3.up*(height*fraction-.3f),new Vector3(width+.15f,.3f,width+.15f),Steel,true);
        }

        private static void Tank(Transform root,Vector3 p,float height,float width)
        {
            Shape(root,"Tank plinth",p,new Vector3(width+1,.5f,width+1),Steel);
            Shape(root,"Riveted boiler",p+Vector3.up*.4f,new Vector3(width,height,width),Teal,true);
            foreach(float t in new[]{.1f,.5f,.9f}) Shape(root,"Copper tank band",p+Vector3.up*(height*t),new Vector3(width+.12f,.25f,width+.12f),Copper,true);
            Model(root,"pipe-large-valve",p+new Vector3(0,1,-width*.5f),new Vector3(2.5f,2.5f,2),Brass);
            Shape(root,"Tank cap",p+Vector3.up*height,new Vector3(width+.3f,.35f,width+.3f),Steel,true);
        }

        private static void Fuel(Transform root,Vector3 p,int count)
        {
            for(int i=0;i<count;i++)
            {
                Shape(root,"Coal bunker",p+Vector3.right*(i-count*.5f)*3.6f,new Vector3(3.3f,.7f,4),Steel);
                Model(root,"hopper-square",p+Vector3.right*(i-count*.5f)*3.6f+Vector3.up*.65f,new Vector3(3,1.3f,3.5f),Coal);
            }
        }
        private static void Cargo(Transform root,Vector3 p,int count)
        {
            for(int i=0;i<count;i++) Model(root,"box-large",p+new Vector3((i%2)*3.3f,0,(i/2)*3.4f),new Vector3(3,2+(i%2),3),i%2==0?Copper:Teal);
        }
        private static void Gantry(Transform root,Vector3 p,float width,float height)
        {
            foreach(int side in new[]{-1,1}) Shape(root,"Gantry steel leg",p+new Vector3(side*width*.5f,0,0),new Vector3(1.1f,height,1.5f),Steel);
            Shape(root,"Background service gantry",p+Vector3.up*(height-.6f),new Vector3(width+2,.8f,2.5f),Brass);
            Model(root,"catwalk-straight",p+Vector3.up*height,new Vector3(width,1.1f,2.4f),Steel);
        }
    }
}
