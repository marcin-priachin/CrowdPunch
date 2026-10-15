using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Editor
{
    public static partial class EnvironmentExamplesBuilder
    {
        private static void DressArena(Transform root, Bounds floor, int theme)
        {
            float x = floor.extents.x, z = floor.extents.z;
            var stone = Material(theme, "DressedStone", Stones[theme]);
            var dark = Material(theme, "Foundation", Floors[theme] * .68f);
            var accent = Material(theme, "Accent", Accents[theme], .18f);
            var paving = Material(theme, "EdgePaving", Color.Lerp(Floors[theme], Stones[theme], .38f));
            var fill = new GameObject("Soft sky fill").AddComponent<Light>();
            fill.transform.SetParent(root,false); fill.type=LightType.Directional;
            fill.transform.rotation=Quaternion.Euler(65,145,0); fill.intensity=.32f;
            fill.color=new Color(.73f,.83f,1); fill.shadows=LightShadows.None;
            // Broad, low-contrast slabs preserve threat readability at the gameplay camera distance.
            for(float fx=-x+2.3f;fx<x-2;fx+=4)
                for(float fz=-z+2.3f;fz<z-2;fz+=4)
                {
                    float variation=((Mathf.RoundToInt(fx+fz)*17)%3)*.012f;
                    var slab=Material(theme,"FloorSlab_"+Mathf.RoundToInt(variation*1000),Floors[theme]+new Color(variation,variation,variation,0));
                    Block(root,"Quiet floor paving",new Vector3(fx,-.996f,fz),new Vector3(3.96f,.006f,3.96f),slab);
                }
            // The continuous collider remains at -1m; only the quiet perimeter receives paving.
            Block(root, "Landscape shelf", new Vector3(0,-2,0), new Vector3(x*2+40,.7f,z*2+44), dark);
            for (int side = -1; side <= 1; side += 2)
            {
                for (float along = -z + 2; along < z; along += 4)
                {
                    Model(root,"KayKit","floor_tile_large",new Vector3(side*(x-1.2f),-.99f,along),new Vector3(2,.035f,3.8f),0,theme,paving);
                    Model(root,"KayKit",theme==2?"wall_broken":"wall",new Vector3(side*(x+.8f),-1,along),new Vector3(3.9f,1.55f,1.3f),90,theme,stone);
                }
                for (float along = -x + 2; along < x; along += 4)
                {
                    Model(root,"KayKit","wall",new Vector3(along,-1,side*(z+.8f)),new Vector3(3.9f,1.55f,1.3f),0,theme,stone);
                }
                for (float along = -z + 1; along <= z; along += 8)
                    Model(root,"KayKit","pillar_decorated",new Vector3(side*(x+1.4f),-1,along),new Vector3(1.7f,2.5f,1.7f),0,theme,stone);
                // Low foreground silhouettes leave the entry and near-side fighting space visible.
                for (int i = 0; i < 4; i++)
                    Model(root,"Stylized Nature","Rock_Medium_"+(i%3+1),new Vector3(side*(x+5+i%2*3),-1.3f,-z+4+i*9),new Vector3(3.8f,2+i%2,3.5f),i*53,theme,stone);
            }
            switch (theme)
            {
                case 0: Village(root,x,z,theme); break;
                case 1: Foundry(root,x,z,theme); break;
                case 2: Observatory(root,x,z,theme); break;
                case 3: Vault(root,x,z,theme); break;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Model(root,"KayKit","column",new Vector3(side*5,-1,z+4),new Vector3(2.4f,5,2.4f),0,theme,stone);
                Block(root,"Inset light",new Vector3(side*5,2.3f,z+2.75f),new Vector3(.7f,.65f,.08f),accent);
            }
            if(System.IO.Directory.Exists(Output+"Atmosphere")) FinishArena(root,floor,theme);
        }

        private static void Village(Transform root,float x,float z,int theme)
        {
            var plaster = Material(theme,"WarmPlaster",new Color(.77f,.69f,.48f));
            for (int side = -1; side <= 1; side += 2)
            {
                House(root,new Vector3(side*(x+10),-1.3f,3),theme,plaster,side*90);
                House(root,new Vector3(side*11,-1.3f,z+12),theme,plaster,0);
                Model(root,"Stylized Nature","CommonTree_1",new Vector3(side*(x+6),-1.3f,z-4),new Vector3(8,10,8),side*32,theme);
                Model(root,"Stylized Nature","CommonTree_3",new Vector3(side*(x+13),-1.3f,-z+9),new Vector3(7,8,7),0,theme);
                for(int i=0;i<4;i++) Model(root,"Stylized Nature","Bush_Common",new Vector3(side*(x+3.5f),-1.25f,-10+i*7),new Vector3(2.8f,1.3f,2.6f),i*36,theme);
                Model(root,"Fantasy Props","Barrel",new Vector3(side*(x+3),-1.2f,z-1),new Vector3(1.5f,2,1.5f),0,theme);
                Model(root,"KayKit","crates_stacked",new Vector3(side*(x+4),-1.2f,-2),new Vector3(3,2.6f,2.4f),side*10,theme);
                Model(root,"KayKit","banner_green",new Vector3(side*5,1,z+2.7f),new Vector3(1.4f,2.4f,.2f),0,theme);
                Atmosphere(root,theme,new Vector3(side*(x+4),2,4),new Color(1,.84f,.4f,.35f),false);
            }
        }

        private static void House(Transform root,Vector3 bottom,int theme,Material plaster,float yaw)
        {
            var group = new GameObject("Village building").transform;
            group.SetParent(root,false);
            Block(group,"Building foundation",new Vector3(0,-4,0),new Vector3(7,4,7),plaster);
            Block(group,"Plaster volume",new Vector3(0,0,0),new Vector3(7,5,7),plaster);
            Model(group,"Medieval Village","Wall_UnevenBrick_Window_Wide_Round",new Vector3(0,0,-3.55f),new Vector3(7,5,.35f),0,theme);
            Model(group,"Medieval Village","Window_Wide_Round1",new Vector3(0,1.3f,-3.77f),new Vector3(3,2.5f,.25f),0,theme);
            for(int face=1;face<4;face++)
            {
                var rotation=Quaternion.Euler(0,face*90,0);
                Model(group,"Medieval Village","Wall_UnevenBrick_Window_Wide_Round",rotation*new Vector3(0,0,-3.55f),new Vector3(7,5,.35f),face*90,theme);
                Model(group,"Medieval Village","Window_Wide_Round1",rotation*new Vector3(0,1.3f,-3.77f),new Vector3(3,2.5f,.25f),face*90,theme);
            }
            Model(group,"Medieval Village","Roof_RoundTiles_8x8",new Vector3(0,5,0),new Vector3(8.8f,3.4f,8.8f),0,theme);
            group.SetPositionAndRotation(bottom,Quaternion.Euler(0,yaw,0));
        }

        private static void Foundry(Transform root,float x,float z,int theme)
        {
            var metal = Material(theme,"BlueIron",new Color(.24f,.31f,.35f));
            var hot = Material(theme,"FurnaceGlow",new Color(1,.28f,.035f),1.4f);
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<3;i++)
                {
                    Model(root,"kenney_factory","machine-window",new Vector3(side*(x+6),-1.3f,-7+i*11),new Vector3(5.5f,6,5),side*90,theme,metal);
                    Model(root,"kenney_factory","pipe-large-long",new Vector3(side*(x+6),4.7f,-7+i*11),new Vector3(1.5f,7+i*1.5f,1.5f),0,theme,metal);
                    Block(root,"Contained furnace window",new Vector3(side*(x+3.18f),.5f,-7+i*11),new Vector3(.12f,1.6f,2),hot);
                }
                Model(root,"kenney_factory","pipe-large-bend",new Vector3(side*(x+4),-1.3f,-z+3),new Vector3(3,3,3),side*90,theme,metal);
                Model(root,"Fantasy Props","Anvil",new Vector3(side*(x+3.8f),-.8f,0),new Vector3(2.1f,1.4f,1.4f),0,theme);
                Model(root,"kenney_factory","cog-a",new Vector3(side*(x+9),-1.3f,z+5),new Vector3(5,5,1.4f),0,theme,metal);
                Atmosphere(root,theme,new Vector3(side*(x+6),11,4),new Color(.65f,.68f,.7f,.22f),false);
            }
            Model(root,"kenney_factory","structure-doorway-wide",new Vector3(0,-1,z+7),new Vector3(11,9,3),0,theme,metal);
        }

        private static void Observatory(Transform root,float x,float z,int theme)
        {
            var stone = Material(theme,"OldLimestone",new Color(.49f,.57f,.45f));
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<3;i++)
                {
                    Model(root,"KayKit","column",new Vector3(side*(x+5),-1.3f,-8+i*11),new Vector3(2.8f,6+i%2*2,2.8f),0,theme,stone);
                    Model(root,"Stylized Nature","CommonTree_"+(i+1),new Vector3(side*(x+11),-1.3f,-6+i*12),new Vector3(10,12+i,10),i*60,theme);
                    Model(root,"Stylized Nature","Fern_1",new Vector3(side*(x+3),-1.3f,-7+i*11),new Vector3(3.6f,2,3.6f),i*70,theme);
                }
                Model(root,"KayKit","wall_arched",new Vector3(side*9,-1.3f,z+7),new Vector3(7,7,2),0,theme,stone);
                Model(root,"Stylized Nature","Bush_Common",new Vector3(side*8,-1.3f,z+5),new Vector3(5,2.8f,4),0,theme);
                Atmosphere(root,theme,new Vector3(side*(x+3),2,0),new Color(.5f,1,.72f,.45f),false);
            }
            Model(root,"KayKit","wall_doorway",new Vector3(0,-1.3f,z+10),new Vector3(8,10,2.5f),0,theme,stone);
        }

        private static void Vault(Transform root,float x,float z,int theme)
        {
            var snow = Material(theme,"Snow",new Color(.8f,.88f,.91f));
            var ice = Material(theme,"GlacialStone",new Color(.34f,.49f,.6f));
            var pine = Material(theme,"FrostedPine",new Color(.35f,.56f,.57f));
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<4;i++)
                {
                    Model(root,"Stylized Nature","Pine_"+(i%3+1),new Vector3(side*(x+6+i%2*4),-1.3f,-12+i*10),new Vector3(5,9+i%2*4,5),i*47,theme,pine);
                    Model(root,"Stylized Nature","Rock_Medium_1",new Vector3(side*(x+4),-1.3f,-12+i*10),new Vector3(4,1.3f,4),i*27,theme,snow);
                }
                Model(root,"KayKit","column",new Vector3(side*9,-1.3f,z+6),new Vector3(4,10,4),0,theme,ice);
                Block(root,"Snow cornice",new Vector3(side*9,8.7f,z+6),new Vector3(4.5f,.4f,4.5f),snow);
                Model(root,"KayKit","banner_blue",new Vector3(side*9,4,z+3.9f),new Vector3(2.5f,4,.2f),0,theme);
                Atmosphere(root,theme,new Vector3(side*(x+4),6,0),new Color(.9f,.97f,1,.55f),true);
            }
            Model(root,"KayKit","wall_doorway",new Vector3(0,-1.3f,z+9),new Vector3(12,9,3),0,theme,ice);
            Block(root,"Vault lintel snow",new Vector3(0,7.7f,z+9),new Vector3(13,.4f,3.6f),snow);
        }

        private static void Atmosphere(Transform root,int theme,Vector3 position,Color color,bool snow)
        {
            string path = Output + "Materials/AmbientParticles.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Models/kenney_particle-pack/PNG (Transparent)/circle_01.png"));
                material.SetFloat("_Surface",1); material.SetFloat("_Blend",0); material.SetFloat("_ZWrite",0);
                material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue=(int)RenderQueue.Transparent;
                AssetDatabase.CreateAsset(material,path);
            }
            var effect = new GameObject(snow?"Perimeter snow flurries":theme==1?"Chimney haze":"Perimeter motes");
            effect.transform.SetParent(root,false); effect.transform.position=position;
            var ps=effect.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main; main.loop=true; main.duration=8; main.startLifetime=6; main.startSpeed=0;
            main.startSize=theme==1?1.4f:.08f; main.startColor=color; main.maxParticles=32;
            main.simulationSpace=ParticleSystemSimulationSpace.World; main.cullingMode=ParticleSystemCullingMode.Automatic;
            var emission=ps.emission; emission.rateOverTime=4;
            var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=new Vector3(2,2,12);
            var velocity=ps.velocityOverLifetime; velocity.enabled=true; velocity.space=ParticleSystemSimulationSpace.World;
            velocity.x=.12f; velocity.y=snow?-.55f:theme==1?.65f:.1f;
            var fade=ps.colorOverLifetime; fade.enabled=true;
            var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.2f),new GradientAlphaKey(0,1)}); fade.color=gradient;
            var renderer=ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=material; renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        }
    }
}
