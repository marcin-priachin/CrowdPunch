using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Editor
{
    internal static class CombatVfxParticles
    {
        // INFO-004 / VISION-005: cream cartoon puffs and short white speed accents.
        internal static ParticleSystem Dasher(Material smoke, Material stroke)
        {
            var root = new GameObject("DasherTelegraph");
            try
            {
                var puffs = Particle(root, smoke, new Color(1f, .94f, .8f, .8f), 0, .32f, .42f, .7f);
                var shape = puffs.shape;
                shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .42f;
                shape.rotation = new Vector3(90, 0, 0); shape.radiusThickness = .25f;
                var main = puffs.main; main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                var size = puffs.sizeOverLifetime;
                size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(
                    new Keyframe(0, .35f), new Keyframe(.25f, 1f), new Keyframe(1, 1.25f)));
                var accents = Child(root, "White speed accents", stroke, new Color(1f, .98f, .9f, .9f), 0, .18f, .16f, 2f);
                accents.transform.localPosition = new Vector3(0, .35f, -.25f);
                accents.transform.localRotation = Quaternion.Euler(0, 180, 0);
                shape = accents.shape; shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 15; shape.radius = .28f;
                var renderer = accents.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 1.8f; renderer.velocityScale = .08f;
                foreach (var ps in new[] { puffs, accents })
                {
                    main = ps.main; main.loop = true; main.duration = 1f; main.maxParticles = 24;
                    main.useUnscaledTime = false;
                    var emission = ps.emission; emission.rateOverTime = ps == puffs ? 16f : 12f;
                }
                return Save(root);
            }
            finally { Object.DestroyImmediate(root); }
        }

        internal static ParticleSystem Splash(Material flash, Material ring, Material droplet)
        {
            var root = new GameObject("ArcaneImpact");
            try
            {
                var core = Particle(root, flash, new Color(.65f, .98f, 1), 1, .16f, 1.35f, 0);
                var shape = core.shape; shape.enabled = false;
                var rim = Child(root, "Expanding splash ring", ring, new Color(.18f, .8f, 1, .85f), 1, .32f, 1.8f, 0);
                shape = rim.shape; shape.enabled = false;
                rim.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local;
                var size = rim.sizeOverLifetime;
                size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .15f, 1, 1.4f));
                var drops = Child(root, "Chunky cyan droplets", droplet, new Color(.22f, .88f, 1), 10, .38f, .24f, 3.8f);
                shape = drops.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 72; shape.radius = .08f;
                var main = drops.main; main.gravityModifier = .8f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(.24f, .42f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                size = drops.sizeOverLifetime;
                size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .5f),
                    new Keyframe(.18f, 1.2f), new Keyframe(1, 0)));
                Child(root, "White splash flecks", droplet, new Color(.9f, 1, 1), 5, .22f, .12f, 4.5f);
                return Save(root);
            }
            finally { Object.DestroyImmediate(root); }
        }
        internal static ParticleSystem Charge(Material glow, Material ring)
        {
            var root = new GameObject("ArcaneCharge");
            try
            {
                var core = Particle(root, glow, new Color(.7f, .98f, 1, .95f), 1, .12f, 1, 0);
                var halo = Child(root, "Gathering halo", ring, new Color(.2f, .85f, 1, .8f), 1, .3f, 2.4f, 0);
                var sparks = Child(root, "Inward sparks", glow, new Color(.4f, .95f, 1, .9f), 0, .3f, .22f, 0);
                foreach (var ps in new[] { core, halo, sparks })
                {
                    var main = ps.main;
                    main.loop = true; main.duration = 1; main.useUnscaledTime = false;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    var emission = ps.emission;
                    emission.rateOverTime = ps == core ? 24 : ps == halo ? 5 : 18;
                    var shape = ps.shape; shape.enabled = ps == sparks;
                    if (ps == sparks)
                    {
                        shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 1.3f; shape.radiusThickness = 0;
                        main.startSpeed = -3.5f;
                    }
                    var size = ps.sizeOverLifetime;
                    size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, ps == halo ? .35f : .8f));
                }
                return Save(root);
            }
            finally { Object.DestroyImmediate(root); }
        }
        internal static ParticleSystem Impact(string name,Material flash,Material splinter,Material smoke,Color color,float scale)
        {
            var root=new GameObject(name);
            try
            {
                var core=Particle(root,flash,color,1,.14f,1.4f*scale,0);
                var coreShape=core.shape;coreShape.enabled=false;
                var size=core.sizeOverLifetime;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.6f),new Keyframe(.2f,1),new Keyframe(1,0)));
                var flecks=Child(root,"Directional splinters",splinter,color,6,.23f,.22f*scale,4.5f*scale);
                var shape=flecks.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=28;shape.radius=.04f;
                var renderer=flecks.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Stretch;
                renderer.lengthScale=1.4f;renderer.velocityScale=.12f;
                if(smoke!=null) Child(root,"Contact puff",smoke,new Color(.78f,.68f,.48f,.25f),3,.28f,.45f*scale,.6f);
                return Save(root);
            }
            finally { Object.DestroyImmediate(root); }
        }
        internal static ParticleSystem Smoke(string name,Material material,Color color,int count,float lifetime,float size)
        {
            var root=new GameObject(name);
            try
            {
                var ps=Particle(root,material,color,count,lifetime,size,.6f);
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=65;shape.radius=.25f;
                var scale=ps.sizeOverLifetime;scale.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,.45f,1,1.2f));
                var rotation=ps.main;rotation.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
                return Save(root);
            }
            finally { Object.DestroyImmediate(root); }
        }
        internal static ParticleSystem Flight(string name,Material glow,Material ring,bool boss,bool snake)
        {
            var root=new GameObject(name);
            try
            {
                Color color=snake?new Color(1,.77f,.26f,.65f):boss?new Color(.72f,.3f,1,.7f):new Color(.3f,.85f,1,.7f);
                var ps=Particle(root,glow,color,0,.22f,snake?.075f:.16f,0);
                var main=ps.main;main.loop=true;main.duration=1;main.useUnscaledTime=false;
                main.simulationSpace=ParticleSystemSimulationSpace.World;
                var emission=ps.emission;emission.rateOverTime=snake?12:18;
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=snake?.2f:.12f;
                if(!snake)
                {
                    var halo=Child(root,"Arcane halo",ring,color,0,.45f,boss?1.8f:1.6f,0);
                    var haloSize=halo.sizeOverLifetime;haloSize.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,1.25f));
                    var m=halo.main;m.loop=true;m.duration=1;m.simulationSpace=ParticleSystemSimulationSpace.Local;m.useUnscaledTime=false;
                    m.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
                    var e=halo.emission;e.rateOverTime=boss?6:4;e.SetBursts(new[]{new ParticleSystem.Burst(0,1)});
                    var sh=halo.shape;sh.enabled=false;
                    var rotation=halo.rotationOverLifetime;rotation.enabled=true;rotation.z=1.7f;
                    if(boss)
                    {
                        var orbit=Child(root,"Gold orbit",ring,new Color(1,.7f,.25f,.7f),0,.55f,1.95f,0);
                        var orbitSize=orbit.sizeOverLifetime;orbitSize.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,1.15f));
                        m=orbit.main;m.loop=true;m.duration=1;m.simulationSpace=ParticleSystemSimulationSpace.Local;m.useUnscaledTime=false;
                        e=orbit.emission;e.rateOverTime=3;e.SetBursts(new[]{new ParticleSystem.Burst(0,1)});
                        sh=orbit.shape;sh.enabled=false;
                        var r=orbit.GetComponent<ParticleSystemRenderer>();r.alignment=ParticleSystemRenderSpace.Local;
                        orbit.transform.localRotation=Quaternion.Euler(65,0,0);
                        rotation=orbit.rotationOverLifetime;rotation.enabled=true;rotation.z=-2;
                    }
                }
                return Save(root);
            }
            finally { Object.DestroyImmediate(root); }
        }
        internal static ParticleSystem Environment(Material smoke,Material spark)
        {
            var root=new GameObject("ArtGroundImpact");
            try
            {
                Particle(root,smoke,new Color(.67f,.59f,.44f,.45f),6,.35f,.55f,.9f);
                var chips=Child(root,"Noncolliding stone chips",spark,new Color(.62f,.54f,.42f),4,.32f,.085f,2.7f);
                var main=chips.main;main.gravityModifier=.9f;
                var model=CombatVfxAuthoring.Load<GameObject>("Assets/CrowdPunch/Models/kenney_nature-kit/rock_smallH.fbx");
                var renderer=chips.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode=ParticleSystemRenderMode.Mesh;
                var source=model.GetComponentInChildren<MeshFilter>().sharedMesh;
                string meshPath=CombatVfxAuthoring.Materials+"ImpactStoneMesh.asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(mesh==null){mesh=new Mesh { name="Impact stone single submesh" };AssetDatabase.CreateAsset(mesh,meshPath);}
                // Particle mesh rendering requires exactly one submesh. Keep both source surfaces.
                var parts=new CombineInstance[source.subMeshCount];
                for(int i=0;i<parts.Length;i++)parts[i]=new CombineInstance { mesh=source,subMeshIndex=i,transform=Matrix4x4.identity };
                mesh.Clear();mesh.CombineMeshes(parts,true,false);EditorUtility.SetDirty(mesh);
                renderer.mesh=mesh;
                // Use a game-owned cel material, with no colliders, shadows or imported runtime scripts.
                string path=CombatVfxAuthoring.Materials+"ImpactStone.mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=new Material(Shader.Find("CrowdPunch/Cel/Environment"));AssetDatabase.CreateAsset(mat,path);}
                mat.SetColor("_BaseColor",new Color(.62f,.54f,.42f));renderer.sharedMaterial=mat;
                return Save(root);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static ParticleSystem Child(GameObject parent,string name,Material material,Color color,int count,float lifetime,float size,float speed)
        {
            var child=new GameObject(name);child.transform.SetParent(parent.transform,false);
            return Particle(child,material,color,count,lifetime,size,speed);
        }
        private static ParticleSystem Particle(GameObject go,Material material,Color color,int count,float lifetime,float size,float speed)
        {
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=false;main.duration=.4f;
            main.startLifetime=lifetime;main.startSpeed=new ParticleSystem.MinMaxCurve(speed*.6f,speed);
            main.startSize=new ParticleSystem.MinMaxCurve(size*.8f,size);main.startColor=color;
            main.maxParticles=32;main.stopAction=ParticleSystemStopAction.None;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
            main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;main.useUnscaledTime=true;
            var emission=ps.emission;emission.rateOverTime=0;
            if(count>0)emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)count)});
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=45;shape.radius=.1f;
            var fade=ps.colorOverLifetime;fade.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.06f),new GradientAlphaKey(.7f,.45f),new GradientAlphaKey(0,1)});
            fade.color=gradient;
            var scale=ps.sizeOverLifetime;scale.enabled=true;scale.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,.2f));
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return ps;
        }
        private static ParticleSystem Save(GameObject root)
            => PrefabUtility.SaveAsPrefabAsset(root,CombatVfxAuthoring.Prefabs+root.name+".prefab").GetComponent<ParticleSystem>();
    }
}
