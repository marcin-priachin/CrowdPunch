using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Editor
{
    public static partial class EnvironmentExamplesBuilder
    {
        private static float LandscapeHeight(float x,float z,int theme)
        {
            float radius=new Vector2(x,z).magnitude;
            float rise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(40,110,radius));
            float broad=Mathf.PerlinNoise(x*.009f+21+theme*17,z*.009f+52);
            float ridges=Mathf.Abs(Mathf.PerlinNoise(x*.023f+16,z*.023f+theme*13)-.5f)*2;
            float height=theme==0?22:theme==2?30:theme==1?42:65;
            return -1.32f+rise*(4+broad*height+ridges*height*.6f);
        }

        private static void BuildLandscape(Transform root,int theme)
        {
            const int cells=120;
            const float step=5;
            var vertices=new Vector3[(cells+1)*(cells+1)];
            var uv=new Vector2[vertices.Length];
            var triangles=new List<int>[5];
            for(int i=0;i<5;i++) triangles[i]=new List<int>();
            for(int z=0;z<=cells;z++) for(int x=0;x<=cells;x++)
            {
                int index=z*(cells+1)+x;
                float wx=(x-cells/2)*step,wz=(z-cells/2)*step;
                vertices[index]=new Vector3(wx,LandscapeHeight(wx,wz,theme),wz);
                uv[index]=new Vector2(wx*.025f,wz*.025f);
            }
            for(int z=0;z<cells;z++) for(int x=0;x<cells;x++)
            {
                int a=z*(cells+1)+x,b=a+1,c=a+cells+1,d=c+1;
                float slope=Mathf.Max(Mathf.Abs(vertices[a].y-vertices[d].y),Mathf.Abs(vertices[b].y-vertices[c].y));
                int band=vertices[a].y>45 && slope>3.8f?3:1;
                triangles[band].AddRange(new[]{a,c,b,b,c,d});
            }
            var mesh=new Mesh { name=$"{theme}_Landscape" };
            mesh.vertices=vertices; mesh.uv=uv; mesh.subMeshCount=5;
            for(int i=0;i<5;i++) mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path=Output+$"Atmosphere/{theme}_Landscape.asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null) AssetDatabase.CreateAsset(mesh,path);
            else { EditorUtility.CopySerialized(mesh,existing); UnityEngine.Object.DestroyImmediate(mesh); mesh=existing; EditorUtility.SetDirty(mesh); }
            Color[] ground={new(.33f,.42f,.18f),new(.25f,.26f,.28f),new(.20f,.34f,.25f),new(.67f,.78f,.84f)};
            var palette=new Material[5];
            for(int i=0;i<5;i++)
            {
                Color color=i==3?Color.Lerp(ground[theme],Stones[theme]*.7f,.7f):i==4?Color.Lerp(ground[theme],Color.white,.42f):ground[theme]*(.91f+i*.075f);
                palette[i]=Material(theme,"Landscape_"+i,color);
            }
            var land=new GameObject("Surrounding valley - visual terrain only"); land.transform.SetParent(root,false); land.isStatic=true;
            land.AddComponent<MeshFilter>().sharedMesh=mesh; land.AddComponent<MeshRenderer>().sharedMaterials=palette;
        }

        private static Vector3 GroundPosition(float x,float z,int theme) => new(x,LandscapeHeight(x,z,theme)-.12f,z);

        private static void BuildDistantSetting(Transform root,int theme)
        {
            var random=new System.Random(2801+theme*771);
            float Next(float a,float b) => Mathf.Lerp(a,b,(float)random.NextDouble());
            var rock=Material(theme,"LandscapeRock",Color.Lerp(Stones[theme],Floors[theme],.5f));
            var foliage=theme==3?Material(theme,"DistantFrostPine",new Color(.22f,.38f,.40f)):null;
            // Full rings support the orbit camera. Irregular clusters break the arena's symmetry.
            for(int i=0;i<48;i++)
            {
                float angle=i*Mathf.PI*2/48+Next(-.06f,.06f),radius=Next(38,95);
                float x=Mathf.Sin(angle)*radius,z=Mathf.Cos(angle)*radius;
                if(theme==1)
                {
                    if(i%2==0) Model(root,"Stylized Nature","Rock_Medium_"+(i%3+1),GroundPosition(x,z,theme),new Vector3(Next(9,17),Next(7,17),Next(8,15)),Next(0,360),theme,rock);
                }
                else
                {
                    float h=Next(theme==2?13:9,theme==2?22:17);
                    Model(root,"Stylized Nature",theme==3?"Pine_"+(i%5+1):"CommonTree_"+(i%5+1),GroundPosition(x,z,theme),new Vector3(h*.7f,h,h*.7f),Next(0,360),theme,foliage);
                    if(i%3==0) Model(root,"Stylized Nature","Rock_Medium_"+(i%3+1),GroundPosition(x+4,z-3,theme),new Vector3(7,Next(3,7),6),Next(0,360),theme,rock);
                }
            }
            for(int i=0;i<22;i++)
            {
                float a=i*Mathf.PI*2/22,r=Next(105,175),x=Mathf.Sin(a)*r,z=Mathf.Cos(a)*r;
                if(theme==0)
                    Model(root,"Stylized Nature","CommonTree_"+(i%5+1),GroundPosition(x,z,theme),new Vector3(14,19,14),Next(0,360),theme);
                else
                    Model(root,"Stylized Nature","Rock_Medium_"+(i%3+1),GroundPosition(x,z,theme)-Vector3.up*5,new Vector3(Next(22,38),Next(theme==3?30:15,theme==3?65:35),Next(22,36)),Next(0,360),theme,rock);
            }
            switch(theme)
            {
                case 0: VillageSkyline(root,theme); break;
                case 1: IndustrialSkyline(root,theme); break;
                case 2: RuinedSkyline(root,theme); break;
                case 3: MountainFortress(root,theme); break;
            }
        }

        private static void VillageSkyline(Transform root,int theme)
        {
            var plaster=Material(theme,"SettlementPlaster",new Color(.72f,.64f,.48f));
            var stone=Material(theme,"VillageTowerStone",new Color(.57f,.55f,.42f));
            for(int i=0;i<12;i++)
            {
                float angle=(i*30+12)*Mathf.Deg2Rad,r=42+i%3*8;
                var p=GroundPosition(Mathf.Sin(angle)*r,Mathf.Cos(angle)*r,theme);
                House(root,p,theme,plaster,-i*30);
            }
            var tower=GroundPosition(-22,48,theme);
            Block(root,"Village bell tower",tower,new Vector3(7,17,7),stone);
            for(int side=-1;side<=1;side+=2)
                Model(root,"KayKit","wall_arched",tower+new Vector3(0,14,side*3.5f),new Vector3(7,5,.7f),0,theme,stone);
            Model(root,"Medieval Village","Roof_RoundTiles_8x8",tower+Vector3.up*19,new Vector3(9,5,9),0,theme);
            // A road leads out of the court into the village, rather than ending in a void.
            var road=Material(theme,"VillageRoad",new Color(.47f,.42f,.29f));
            for(int i=0;i<5;i++) Block(root,"Village approach paving",new Vector3(0,-1.29f,22+i*4),new Vector3(8,.05f,3.95f),road);
        }

        private static void IndustrialSkyline(Transform root,int theme)
        {
            var metal=Material(theme,"DistrictSteel",new Color(.22f,.29f,.34f));
            var rust=Material(theme,"DistrictRust",new Color(.42f,.28f,.19f));
            var hot=Material(theme,"DistrictWindows",new Color(1,.32f,.045f),2.4f);
            for(int i=0;i<8;i++)
            {
                float a=(i*45+15)*Mathf.Deg2Rad,r=48+i%2*10;
                var p=GroundPosition(Mathf.Sin(a)*r,Mathf.Cos(a)*r,theme);
                Block(root,"Industrial foundation",p-Vector3.up*9,new Vector3(11,9,10),metal).transform.rotation=Quaternion.Euler(0,-i*45,0);
                Model(root,"kenney_factory","machine-window",p,new Vector3(11,13+i%3*4,10),-i*45,theme,metal);
                Model(root,"kenney_factory","pipe-large-long",p+Vector3.up*(13+i%3*4),new Vector3(3,12+i%3*3,3),0,theme,rust);
                Block(root,"Factory amber glazing",p+new Vector3(0,6,-5.05f),new Vector3(8,2,.15f),hot);
                Atmosphere(root,theme,p+Vector3.up*(25+i%3*7),new Color(.25f,.28f,.32f,.35f),false);
                var smoke=root.GetChild(root.childCount-1).GetComponent<ParticleSystem>();
                var main=smoke.main; main.startSize=5; main.startLifetime=12; main.maxParticles=30;
                var emission=smoke.emission; emission.rateOverTime=2;
                var shape=smoke.shape; shape.scale=Vector3.one;
                var velocity=smoke.velocityOverLifetime; velocity.x=.9f; velocity.y=1.8f;
            }
            for(int side=-1;side<=1;side+=2)
            {
                var p=new Vector3(side*25,-1.3f,34);
                Block(root,"Gantry support",p,new Vector3(2,20,2),rust);
                Block(root,"Gantry crossbeam",p+new Vector3(-side*7,18,0),new Vector3(16,2,2),rust);
                Block(root,"Suspended cargo cable",p+new Vector3(-side*10,10,0),new Vector3(.12f,8,.12f),metal);
                Model(root,"KayKit","crates_stacked",p+new Vector3(-side*10,7,0),new Vector3(4,3,3),0,theme);
            }
        }

        private static void RuinedSkyline(Transform root,int theme)
        {
            var stone=Material(theme,"AncientPaleStone",new Color(.49f,.57f,.48f));
            var dark=Material(theme,"AncientBronze",new Color(.19f,.34f,.30f));
            for(int side=-1;side<=1;side+=2)
                for(int i=0;i<4;i++)
                    Model(root,"KayKit",i==2?"wall_broken":"wall_arched",GroundPosition(side*(25+i*3),26+i*7,theme),new Vector3(9,12-i,3),side*35,theme,stone);
            var p=GroundPosition(12,54,theme);
            Model(root,"KayKit","pillar_decorated",p,new Vector3(15,25,15),0,theme,stone);
            var dome=GameObject.CreatePrimitive(PrimitiveType.Sphere); dome.name="Weathered observatory dome";
            UnityEngine.Object.DestroyImmediate(dome.GetComponent<Collider>()); dome.transform.SetParent(root,false);
            dome.transform.position=p+Vector3.up*25; dome.transform.localScale=new Vector3(18,12,18);
            dome.GetComponent<Renderer>().sharedMaterial=dark; dome.isStatic=true;
            Model(root,"KayKit","column",p+Vector3.up*30,new Vector3(2,9,2),0,theme,stone);
            for(int i=0;i<8;i++) Model(root,"Stylized Nature","Bush_Common",GroundPosition(-27+i*7,31,theme),new Vector3(6,3,5),i*37,theme);
        }

        private static void MountainFortress(Transform root,int theme)
        {
            var stone=Material(theme,"FortressStone",new Color(.34f,.46f,.56f));
            var snow=Material(theme,"FortressSnow",new Color(.84f,.91f,.95f));
            var warm=Material(theme,"FortressWindows",new Color(1,.59f,.18f),2.2f);
            for(int i=-2;i<=2;i++)
            {
                var p=GroundPosition(i*14,49+Mathf.Abs(i)*4,theme);
                float h=i==0?25:18;
                Block(root,"Fortress buried foundation",p-Vector3.up*7,new Vector3(9,7,9),stone);
                Model(root,"KayKit","pillar_decorated",p,new Vector3(9,h,9),0,theme,stone);
                Block(root,"Fortress snow cap",p+Vector3.up*h,new Vector3(10,.7f,10),snow);
                Block(root,"Distant occupied window",p+new Vector3(0,h-6,-4.55f),new Vector3(1,2.4f,.12f),warm);
                if(i<2) Model(root,"KayKit","wall_arched",p+new Vector3(7,0,0),new Vector3(9,12,3),0,theme,stone);
            }
        }
    }
}
