using UnityEngine;
using static CrowdPunch.Editor.ChapterTwoFoundryAssets;

namespace CrowdPunch.Editor
{
    // VISION-005..006: a closed industrial district supports every camera orbit.
    internal static class ChapterTwoFoundryBlocks
    {
        internal static void Surround(Transform root,Bounds court,float y,int stage)
        {
            // Setbacks leave the orbiting camera in the service street. Opposite rows
            // close the view through the small expansion joints between buildings.
            float width=court.size.x+76;
            Row(root,"North factory block",new Vector3(court.center.x,y,court.max.z+33),width,18,8.5f,0,stage);
            Row(root,"South delivery block",new Vector3(court.center.x,y,court.min.z-29),width,18,5.5f,180,stage+1);
            float length=court.size.z+43;
            Row(root,"West factory block",new Vector3(court.min.x-29,y,court.center.z+2),length,18,7,-90,stage+2);
            Row(root,"East factory block",new Vector3(court.max.x+29,y,court.center.z+2),length,18,7,90,stage+3);

            // Low, close delivery architecture gives the previously bare south edge
            // a purpose without placing a tall roof between the camera and player.
            foreach(int side in new[]{-1,1})
            {
                float x=court.center.x+side*(court.extents.x+9);
                Workshop(root,new Vector3(x,y,court.min.z-8),new Vector3(12,2.8f,9),false,false,180);
                Shape(root,"Delivery loading apron",new Vector3(x,y,court.min.z-10),new Vector3(13.5f,.16f,14),Ground);
            }
            Shape(root,"Delivery street paving",new Vector3(court.center.x,y-.04f,court.min.z-13),new Vector3(court.size.x+4,.16f,10),Ground);

            // A second, solid perimeter hides the ground beyond the first street from
            // elevated and diagonal views, including the 100 m great yard.
            float outerWidth=court.size.x+132,outerLength=court.size.z+116;
            Row(root,"North skyline block",new Vector3(court.center.x,y,court.max.z+59),outerWidth,22,12,0,stage+4,true);
            Row(root,"South skyline block",new Vector3(court.center.x,y,court.min.z-55),outerWidth,22,10,180,stage+5,true);
            Row(root,"West skyline block",new Vector3(court.min.x-55,y,court.center.z+2),outerLength-24,22,11,-90,stage+6,true);
            Row(root,"East skyline block",new Vector3(court.max.x+55,y,court.center.z+2),outerLength-24,22,11,90,stage+7,true);
        }

        private static void Row(Transform root,string name,Vector3 center,float length,float depth,float height,float yaw,int seed,bool skyline=false)
        {
            var group=new GameObject(name).transform;group.SetParent(root,false);
            int count=Mathf.CeilToInt(length/18);float step=length/count;
            var rotation=Quaternion.Euler(0,yaw,0);
            for(int i=0;i<count;i++)
            {
                var p=center+rotation*new Vector3((i-(count-1)*.5f)*step,0,0);
                Workshop(group,p,new Vector3(step-.10f,height+(seed+i)%3*.65f,depth),(seed+i)%4==0,true,yaw,skyline);
            }
            // Expansion joints must not become aligned sightlines through both rows.
            // Recess the continuous backing inside the bays, clear of their facade plane.
            var backing=Shape(group,"Recessed continuous party wall",center+rotation*new Vector3(0,0,-depth*.5f+.5f),new Vector3(length,height-.2f,.3f),Brick);
            backing.transform.localRotation=rotation;
        }

        internal static void Workshop(Transform root,Vector3 p,Vector3 size,bool furnace,bool joined=false,float yaw=0,bool skyline=false)
        {
            // Fit imported doors in their local axes before rotating the finished bay.
            var bay=new GameObject("Factory structural bay").transform;bay.SetParent(root,false);
            Shape(bay,"Foundry brick workshop",Vector3.zero,size,Brick);
            float roofWidth=size.x+(joined?0:.4f);
            Shape(bay,"Standing seam roof",Vector3.up*size.y,new Vector3(roofWidth,.25f,size.z+.4f),Teal);
            // The pitched roof bottom clears the flat roof top rather than intersecting it.
            Roof(bay,Vector3.up*(size.y+.27f),new Vector3(roofWidth,1.3f,size.z+.4f));
            float windowHeight=Mathf.Min(1,size.y*.23f);
            for(int i=-1;!skyline && i<=1;i++)
            {
                Shape(bay,"Steel facade pier",new Vector3(i*size.x*.43f,0,-size.z*.5f-.15f),new Vector3(.3f,size.y,.3f),Steel);
                Shape(bay,"High clerestory window",new Vector3(i*size.x*.27f,size.y*.68f,-size.z*.5f-.08f),new Vector3(size.x*.20f,windowHeight,.12f),Ivory);
                Shape(bay,"Clerestory mullion",new Vector3(i*size.x*.27f,size.y*.68f,-size.z*.5f-.17f),new Vector3(.1f,windowHeight,.06f),Steel);
            }
            // Door and footing occupied the same exposed plane. Terminate the footing
            // at the jambs so their different baked receivers cannot alternate on screen.
            if(!skyline)Model(bay,"door-wide-closed",new Vector3(0,0,-size.z*.5f-.25f),new Vector3(4,3.8f,.4f),Steel);
            float footingWidth=(size.x-4.2f)*.5f;
            foreach(int side in skyline?System.Array.Empty<int>():new[]{-1,1})
            {
                Shape(bay,"Brick footing beside door",new Vector3(side*(2.1f+footingWidth*.5f),0,-size.z*.5f-.225f),new Vector3(footingWidth,.35f,.45f),Steel);
                Shape(bay,"Copper drainpipe",new Vector3(side*(size.x*.5f-.35f),0,-size.z*.5f-.2f),new Vector3(.14f,size.y,.14f),Copper,true);
            }
            if(furnace)
            {
                Chimney(bay,new Vector3(size.x*.28f,0,size.z*.15f),size.y+9,2.5f);
                if(!skyline)Model(bay,"hopper-high-round",new Vector3(-size.x*.26f,size.y+.27f,0),new Vector3(3,4,3),Copper);
            }
            bay.localPosition=p;bay.localRotation=Quaternion.Euler(0,yaw,0);
        }

        private static void Chimney(Transform root,Vector3 p,float height,float width)
        {
            Shape(root,"Chimney footing",p,new Vector3(width+.8f,2,width+.8f),Steel);
            Shape(root,"Smelter chimney",p+Vector3.up*1.8f,new Vector3(width,height-1.8f,width),Brick,true);
            foreach(float fraction in new[]{.35f,.70f,1f})Shape(root,"Chimney iron band",p+Vector3.up*(height*fraction-.3f),new Vector3(width+.15f,.3f,width+.15f),Steel,true);
        }
    }
}
