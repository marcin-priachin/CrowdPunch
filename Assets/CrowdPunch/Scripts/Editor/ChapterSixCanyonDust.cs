using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace CrowdPunch.Editor
{
    // VISION-005/006: sparse, low cartoon sand puffs outside the combat floor.
    public static class ChapterSixCanyonDust
    {
        public const string RootName="Chapter Six - Cartoon Desert Dust";
        private const string MaterialPath=ChapterSixCanyonAssets.Output+"/Dust/Windblown cartoon sand.mat";

        [MenuItem("Crowd Punch/Art Direction/Add Chapter Six Cartoon Desert Dust")]
        public static void Refresh()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning ||
                Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save scenes and stop Play/baking before adding dust.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int stage=51;stage<=60;stage++)
                {
                    var main=EditorSceneManager.OpenScene(ChapterSixCanyonBuilder.MainPath(stage));
                    var sub=EditorSceneManager.OpenScene(ChapterSixCanyonBuilder.SubPath(stage),OpenSceneMode.Additive);
                    var floors=sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>())
                        .Where(r=>r.name=="Continuous Convex Floor" || r.name=="Continuous Round Floor").ToArray();
                    var court=floors[0].bounds;foreach(var floor in floors.Skip(1))court.Encapsulate(floor.bounds);
                    EditorSceneManager.CloseScene(sub,true);
                    Apply(main,court,stage);EditorSceneManager.MarkSceneDirty(main);EditorSceneManager.SaveScene(main);
                }
                AssetDatabase.SaveAssets();
            }
            finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }

        internal static void Apply(Scene scene,Bounds court,int stage)
        {
            foreach(var old in scene.GetRootGameObjects().Where(g=>g.name==RootName))Object.DestroyImmediate(old);
            var root=new GameObject(RootName);SceneManager.MoveGameObjectToScene(root,scene);
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(material==null)
            {
                var shader=Shader.Find("CrowdPunch/Chapter Six Cartoon Dust");
                if(shader==null)throw new InvalidOperationException("Cartoon dust shader has not imported.");
                material=new Material(shader);AssetDatabase.CreateAsset(material,MaterialPath);
            }
            float y=court.max.y-.12f+.65f;
            for(int side=-1;side<=1;side+=2)
            {
                Band(root.transform,new Vector3(court.center.x+side*(court.extents.x+6),y,court.center.z),
                    new Vector3(5,.25f,court.size.z+12),new Vector3(side*.12f,.025f,1.1f),material,(uint)(stage*10+side+2));
                Band(root.transform,new Vector3(court.center.x,y,court.center.z+side*(court.extents.z+6)),
                    new Vector3(court.size.x+2,.25f,5),new Vector3(.9f,.025f,side*.12f),material,(uint)(stage*10+side+5));
            }
        }

        private static void Band(Transform root,Vector3 position,Vector3 size,Vector3 wind,Material material,uint seed)
        {
            var go=new GameObject("Low windblown sand puffs");go.transform.SetParent(root,false);go.transform.position=position;
            var particles=go.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed=false;particles.randomSeed=seed;
            var main=particles.main;main.loop=true;main.duration=8;main.prewarm=true;main.playOnAwake=true;
            main.startLifetime=new ParticleSystem.MinMaxCurve(4,6);main.startSpeed=0;main.maxParticles=32;
            main.startSize3D=true;main.startSizeX=new ParticleSystem.MinMaxCurve(1.5f,2.7f);
            main.startSizeY=new ParticleSystem.MinMaxCurve(.45f,.8f);main.startSizeZ=1;
            main.startRotation=new ParticleSystem.MinMaxCurve(-.12f,.12f);
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(.94f,.9f,.8f,.28f),new Color(1,1,.94f,.38f));
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.cullingMode=ParticleSystemCullingMode.Pause;
            var emission=particles.emission;emission.rateOverTime=Mathf.Clamp(Mathf.Max(size.x,size.z)*.065f,1.4f,5);
            var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=size;
            var velocity=particles.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
            velocity.x=wind.x;velocity.y=wind.y;velocity.z=wind.z;
            var color=particles.colorOverLifetime;color.enabled=true;
            var fade=new Gradient();fade.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.2f),new GradientAlphaKey(.7f,.65f),new GradientAlphaKey(0,1)});
            color.color=fade;
            var growth=particles.sizeOverLifetime;growth.enabled=true;
            growth.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.65f,1,1.15f));
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
            renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.sortMode=ParticleSystemSortMode.Distance;
            renderer.allowRoll=false;renderer.maxParticleSize=.08f;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
        }
    }
}
