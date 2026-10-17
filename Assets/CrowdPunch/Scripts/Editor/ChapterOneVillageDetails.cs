using System.Linq;
using UnityEngine;
using static CrowdPunch.Editor.ChapterOneVillageAssets;
using static CrowdPunch.Editor.ChapterOneVillageLandscape;

namespace CrowdPunch.Editor
{
    // VISION-005: concentrate small silhouettes outside combat and entrance lanes.
    internal static class ChapterOneVillageDetails
    {
        internal static void Build(Transform root,Bounds floor,int stage)
        {
            var details=new GameObject("Planted verges and village life").transform;
            details.SetParent(root,false);
            var random=new System.Random(stage*7919+103);
            float Next(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
            int clusters=stage==8?88:48;
            for(int i=0;i<clusters;i++)
            {
                float angle=(i+.5f)*Mathf.PI*2/clusters;
                var anchor=Ring(angle,Next(2.8f,9),floor,stage);
                // Keep both broad approach paths unobstructed.
                if(Mathf.Abs(anchor.x-floor.center.x)<6) continue;
                for(int j=0;j<3;j++)
                {
                    var p=Ground(anchor.x+Next(-1.2f,1.2f),anchor.z+Next(-1.2f,1.2f),floor,stage);
                    Model(details,"Stylized Nature","Grass_Common_Short",p-Vector3.up*.06f,
                        new Vector3(Next(.7f,1.3f),Next(.25f,.5f),Next(.7f,1.3f)),Next(0,360));
                }
                if(i%3==0)
                    Model(details,"Stylized Nature","Flower_4_Group",anchor,
                        new Vector3(1.4f,.6f,1.2f),Next(0,360));
                if(i%4==0)
                    Model(details,"Stylized Nature","Bush_Common_Flowers",anchor+Vector3.right*1.5f,
                        new Vector3(2.4f,1.15f,1.8f),Next(0,360));
            }

            var stone=Surface("Limestone",new Color(.64f,.59f,.43f));
            var wood=Surface("Dark oak",new Color(.26f,.17f,.09f));
            var climbing=Surface("Climbing foliage",new Color(.24f,.36f,.12f));
            foreach(var house in root.Cast<Transform>().Where(t=>t.name=="Terracotta village house").ToArray())
            {
                var front=house.TransformPoint(new Vector3(0,0,-4.5f));
                // A broad doorstep and garden edge visually anchor each building.
                var step=Block(details,"Limestone doorstep",front-Vector3.up*.1f,new Vector3(3,.25f,1.3f),stone);
                step.transform.rotation=house.rotation;
                var bench=house.TransformPoint(new Vector3(4.5f,0,0));
                Model(details,"Fantasy Props","Bench",bench,new Vector3(2.3f,1.1f,1),house.eulerAngles.y+90);
                Model(details,"Fantasy Props","Barrel",house.TransformPoint(new Vector3(-4.3f,0,-2)),
                    new Vector3(1.1f,1.5f,1.1f),Next(0,360));
                Model(details,"Fantasy Props",stage%2==0?"FarmCrate_Carrot":"FarmCrate_Apple",
                    house.TransformPoint(new Vector3(-4.6f,0,-3.5f)),new Vector3(1.2f,.8f,1),house.eulerAngles.y);
                Model(details,"Stylized Nature","Bush_Common_Flowers",house.TransformPoint(new Vector3(3.5f,0,-3.9f)),
                    new Vector3(2.2f,1.1f,1.6f),Next(0,360));
                for(int i=0;i<3;i++)
                {
                    var p=house.TransformPoint(new Vector3(-2.5f+i*2.5f,.25f,3.4f));
                    var post=Block(details,"Garden trellis",p,new Vector3(.16f,2.7f,.16f),wood);
                    post.transform.rotation=house.rotation;
                    Model(details,"Medieval Village","Prop_Vine2",p,new Vector3(1.5f,2.2f,.25f),house.eulerAngles.y,climbing);
                }
            }
            // Timber stacks and produce corners give the side lanes a lived-in purpose.
            for(int side=-1;side<=1;side+=2)
            {
                var p=Ground(floor.center.x+side*(floor.extents.x+10),floor.center.z-3,floor,stage);
                for(int i=0;i<3;i++)
                    Block(details,"Stored oak beams",p+new Vector3(0,i*.24f,i%2*.25f),new Vector3(3,.2f,.7f),wood);
                Model(details,"Medieval Village","Prop_Crate",p+new Vector3(side*2,0,2),new Vector3(1.3f,1.3f,1.3f),Next(-20,20));
            }
        }
    }
}
