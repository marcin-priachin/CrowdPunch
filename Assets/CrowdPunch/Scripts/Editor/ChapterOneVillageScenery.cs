using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using static CrowdPunch.Editor.ChapterOneVillageAssets;
using static CrowdPunch.Editor.ChapterOneVillageLandscape;

namespace CrowdPunch.Editor
{
    internal static class ChapterOneVillageScenery
    {
        private static Material Stone => Surface("Limestone",new Color(.64f,.59f,.43f));
        private static Material Timber => Surface("Dark oak",new Color(.26f,.17f,.09f));
        private static Material Roof => Surface("Terracotta roof",new Color(.60f,.25f,.12f));
        private static Material Plaster => Surface("Cream plaster",new Color(.76f,.69f,.49f));
        private static Material Cloth
        {
            get
            {
                var material=Surface("Village blue cloth",new Color(.23f,.43f,.52f));
                material.SetFloat("_Cull",0); UnityEditor.EditorUtility.SetDirty(material);
                return material;
            }
        }

        internal static void Build(Transform root,Bounds floor,int stage)
        {
            float x=floor.extents.x,z=floor.extents.z,y=floor.max.y;
            var c=floor.center; c.y=y;
            // Paths are blended in the meadow shader: no overlapping road slabs.
            foreach(int side in new[]{-1,1})
            {
                for(float along=-z+3;along<z;along+=8)
                {
                    Model(root,"Stylized Nature","Bush_Common",c+new Vector3(side*(x+3),-.15f,along),new Vector3(2.5f,1.1f,2.2f),along*17);
                    Model(root,"Medieval Village","Prop_WoodenFence_Single",c+new Vector3(side*(x+8),-.18f,along),new Vector3(5,1.4f,.25f),90,Timber);
                }
                for(int i=0;i<3;i++)
                {
                    var p=c+new Vector3(side*(x+11),-.18f,-z+4+i*7);
                    Model(root,"Fantasy Props",i%2==0?"Barrel":"FarmCrate_Apple",p,new Vector3(1.7f,i%2==0?2:1.2f,1.5f),i*23);
                }
            }
            // Larger objects are set back for the orbit camera; low shrubs frame the near edge.
            int houses=stage==8?24:16;
            for(int i=0;i<houses;i++)
            {
                float angle=(i*360f/houses+17+stage*11)*Mathf.Deg2Rad;
                // Leave the north vista open for this stage's hero landmark.
                if(Mathf.Cos(angle)>.72f) continue;
                var p=Ring(angle,17+i%3*8,floor,stage);
                House(root,p,-angle*Mathf.Rad2Deg,i%3);
            }
            var north=c+new Vector3((stage%3-1)*8,-.18f,z+25);
            switch(stage)
            {
                case 1:
                    Orchard(root,c+new Vector3(-x-20,-.18f,0));
                    House(root,north,0,1);
                    Cart(root,c+new Vector3(x+12,-.18f,z-3));
                    break;
                case 2:
                    House(root,north,0,0);
                    for(int i=0;i<5;i++) Model(root,"Stylized Nature","Rock_Medium_"+(i%3+1),c+new Vector3(-x-15-i%2*5,-.18f,-10+i*7),new Vector3(5,2.5f+i%2,4),i*41,Stone);
                    Model(root,"Fantasy Props","Anvil_Log",c+new Vector3(x+13,-.18f,3),new Vector3(2.5f,3,2.2f));
                    break;
                case 3:
                    Market(root,c,floor,4);
                    Tower(root,north,12,false);
                    break;
                case 4:
                    Gate(root,north,14);
                    Cart(root,c+new Vector3(-x-15,-.18f,3));
                    break;
                case 5:
                    Market(root,c,floor,3);
                    Cart(root,north+Vector3.left*8);
                    Cart(root,north+Vector3.right*9);
                    House(root,north+Vector3.forward*9,0,2);
                    break;
                case 6:
                    Mill(root,north);
                    Orchard(root,c+new Vector3(x+22,-.18f,0));
                    break;
                case 7:
                    Tower(root,north,20,false);
                    Garden(root,c+new Vector3(-x-17,-.18f,0));
                    Garden(root,c+new Vector3(x+17,-.18f,0));
                    break;
                case 8:
                    Market(root,c,floor,7);
                    Mill(root,north+Vector3.right*17);
                    Orchard(root,c+new Vector3(-x-23,-.18f,0));
                    Tower(root,north+Vector3.left*19,18,false);
                    break;
                case 9:
                    Tower(root,north,22,true);
                    Gate(root,north+new Vector3(-20,0,10),11);
                    break;
                case 10:
                    Gate(root,north,23);
                    for(int side=-1;side<=1;side+=2)
                    {
                        House(root,north+new Vector3(side*29,0,5),0,2);
                        Model(root,"kenney_castle","wall",north+new Vector3(side*26,0,0),new Vector3(20,8,3),0,Stone);
                    }
                    break;
            }
            for(int side=-1;side<=1;side+=2)
            {
                var p=c+new Vector3(side*(x+4),-.15f,z-3);
                Block(root,"Waypost",p,new Vector3(.26f,3.8f,.26f),Timber);
                Model(root,"KayKit","banner_blue",p+new Vector3(0,2.2f,0),new Vector3(1.5f,1.8f,.16f),0,Cloth);
            }
            Motes(root,c+new Vector3(-x-4,1,0));
            Motes(root,c+new Vector3(x+4,1,0));
            ChapterOneVillageDetails.Build(root,floor,stage);
            ClearBuildingCanopies(root);
        }

        private static void ClearBuildingCanopies(Transform root)
        {
            Bounds BoundsOf(Transform item)
            {
                var renderers=item.GetComponentsInChildren<MeshRenderer>();
                var bounds=renderers[0].bounds;
                foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                return bounds;
            }
            var buildings=root.Cast<Transform>().Where(t=>t.name=="Terracotta village house" || t.name.StartsWith("tower-square-") || t.name=="gate")
                .Select(t=> {var bounds=BoundsOf(t);bounds.Expand(2);return bounds;}).ToArray();
            foreach(var tree in root.Cast<Transform>().Where(t=>t.name.StartsWith("CommonTree_")).ToArray())
                if(buildings.Any(b=>b.Intersects(BoundsOf(tree)))) Object.DestroyImmediate(tree.gameObject);
        }

        private static void House(Transform root,Vector3 bottom,float yaw,int variant)
        {
            var group=new GameObject("Terracotta village house").transform; group.SetParent(root,false);
            float height=variant==2?7:5;
            // The footing must protrude beyond the walls, not share their vertical faces.
            Block(group,"Stone footing",new Vector3(0,-.55f,0),new Vector3(7.4f,.8f,6.4f),Stone);
            ChapterOneVillageHouseBody.Create(group,height,Plaster);
            Model(group,"Medieval Village","Door_2_Round",new Vector3(-1.8f,0,-3.16f),new Vector3(1.4f,2.7f,.2f),0,Timber);
            for(int face=0;face<4;face++)
            {
                var rotation=Quaternion.Euler(0,face*90,0);
                float depth=face%2==0?3.04f:3.54f;
                Model(group,"Medieval Village","Window_Wide_Round1",rotation*new Vector3(0,1.7f,-depth),new Vector3(2.2f,2.4f,.22f),face*90,Timber);
            }
            Block(group,"Timber cornice",new Vector3(0,height-.3f,0),new Vector3(7.15f,.25f,6.15f),Timber);
            Model(group,"Medieval Village","Roof_RoundTiles_8x8",new Vector3(0,height,0),new Vector3(8.5f,3.2f,7.5f),0,Roof);
            Model(group,"Medieval Village","Prop_Chimney",new Vector3(2,height+1.6f,1),new Vector3(1,2.4f,1),0,Stone);
            Block(group,"Oak corner",new Vector3(-3.5f,0,-3.05f),new Vector3(.2f,height,.2f),Timber);
            Block(group,"Oak corner",new Vector3(3.5f,0,-3.05f),new Vector3(.2f,height,.2f),Timber);
            group.SetPositionAndRotation(bottom,Quaternion.Euler(0,yaw,0));
        }

        private static void Tower(Transform root,Vector3 p,float height,bool fortified)
        {
            Model(root,"kenney_castle","tower-square-base",p,new Vector3(7,height*.45f,7),0,Stone);
            Model(root,"kenney_castle","tower-square-mid",p+Vector3.up*(height*.45f),new Vector3(7,height*.55f,7),0,Stone);
            Model(root,"kenney_castle",fortified?"tower-square-top":"tower-square-top-roof-high-windows",p+Vector3.up*height,new Vector3(8,5,8),0,fortified?Stone:Roof);
            for(int face=0;face<4;face++)
            {
                var rotation=Quaternion.Euler(0,face*90,0);
                Model(root,"Medieval Village","Window_Wide_Round1",p+rotation*new Vector3(0,height*.72f,-3.57f),new Vector3(2.3f,3.2f,.2f),face*90,Timber);
            }
            Model(root,"KayKit","banner_blue",p+new Vector3(0,height*.6f,-3.6f),new Vector3(2.2f,4,.2f),0,Cloth);
        }

        private static void Gate(Transform root,Vector3 p,float height)
        {
            Tower(root,p+Vector3.left*12,height,true);
            Tower(root,p+Vector3.right*12,height,true);
            Model(root,"kenney_castle","gate",p,new Vector3(17,height*.7f,4),0,Stone);
            Model(root,"Medieval Village","Door_4_Round",p+new Vector3(0,0,-2.12f),new Vector3(7,height*.5f,.3f),0,Timber);
            Model(root,"Medieval Village","Roof_RoundTiles_8x8",p+Vector3.up*(height*.7f),new Vector3(18,3,6),0,Roof);
        }

        private static void Mill(Transform root,Vector3 p)
        {
            // The annex stands beside the tower: neither roof nor wall faces intersect.
            // A recessed low passage joins the buildings behind their main facades.
            House(root,p+Vector3.right*8,0,2);
            Tower(root,p,14,false);
            Block(root,"Mill annex passage",p+new Vector3(4,0,1),new Vector3(1.2f,3,2.8f),Plaster);
            Block(root,"Mill passage timber roof",p+new Vector3(4,3,1),new Vector3(1.4f,.25f,3.2f),Timber);
            var pivot=p+new Vector3(0,14,-4.4f);
            Block(root,"Windmill axle",pivot+new Vector3(0,-.4f,.25f),new Vector3(.8f,.8f,1.3f),Timber);
            for(int i=0;i<4;i++)
            {
                var rotation=Quaternion.Euler(0,0,i*90+25);
                var arm=Block(root,"Windmill timber sail",pivot+rotation*new Vector3(0,3.5f,0)-Vector3.up*3.5f,new Vector3(.36f,7,.3f),Timber);
                arm.transform.position=pivot+rotation*new Vector3(0,3.5f,0); arm.transform.rotation=rotation;
                var sail=Block(root,"Cream windmill sail",Vector3.zero,new Vector3(1.7f,4.5f,.2f),Plaster);
                sail.transform.position=pivot+rotation*new Vector3(.8f,4.2f,0); sail.transform.rotation=rotation;
            }
        }

        private static void Orchard(Transform root,Vector3 p)
        {
            for(int row=0;row<3;row++) for(int col=0;col<2;col++)
            {
                var position=p+new Vector3(col*9,0,(row-1)*10);
                Model(root,"Stylized Nature","CommonTree_"+(row%3+1),position,new Vector3(6,7,6),row*57);
                Model(root,"Fantasy Props","FarmCrate_Apple",position+new Vector3(2,0,1),new Vector3(1.2f,.7f,1));
            }
        }

        private static void Market(Transform root,Vector3 c,Bounds floor,int count)
        {
            for(int side=-1;side<=1;side+=2) for(int i=0;i<count;i++)
            {
                var p=c+new Vector3(side*(floor.extents.x+15),-.18f,(i-(count-1)*.5f)*8);
                Model(root,"Fantasy Props","Stall_Cart_Empty",p,new Vector3(4,3.5f,2.8f),side*90);
                Block(root,"Blue market awning",p+Vector3.up*3.6f,new Vector3(4.5f,.18f,3.3f),i%2==0?Cloth:Plaster);
                Model(root,"Fantasy Props","FarmCrate_Carrot",p+new Vector3(0,1.2f,0),new Vector3(1.5f,.9f,1.2f));
            }
        }

        private static void Cart(Transform root,Vector3 p) =>
            Model(root,"Medieval Village","Prop_Wagon",p,new Vector3(5,3.5f,3.3f),25);

        private static void Garden(Transform root,Vector3 p)
        {
            for(int i=0;i<4;i++)
            {
                var point=p+new Vector3(0,0,(i-1.5f)*7);
                Block(root,"Raised garden stone bed",point,new Vector3(5,.45f,4),Stone);
                Model(root,"Stylized Nature","Bush_Common",point+Vector3.up*.4f,new Vector3(4.5f,1.2f,3.5f),i*32);
                Model(root,"Fantasy Props","Bench",point+new Vector3(-4,0,0),new Vector3(2.6f,1.4f,1),90);
            }
        }

        private static void Motes(Transform root,Vector3 p)
        {
            var material=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CrowdPunch/Data/EnvironmentExamples/Materials/AmbientParticles.mat");
            if(material==null) return;
            var go=new GameObject("Quiet orchard motes"); go.transform.SetParent(root,false); go.transform.position=p;
            var ps=go.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main; main.loop=true; main.startLifetime=6; main.startSpeed=.08f;
            main.startSize=.055f; main.startColor=new Color(1,.85f,.5f,.25f); main.maxParticles=18;
            main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission; emission.rateOverTime=2;
            var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=new Vector3(2,2,15);
            var renderer=ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        }
    }
}
