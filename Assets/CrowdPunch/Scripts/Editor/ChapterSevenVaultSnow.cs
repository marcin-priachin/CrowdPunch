using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace CrowdPunch.Editor
{
    // VISION-005/006: native, scene-owned weather; no gameplay state or enemy queries.
    public static class ChapterSevenVaultSnow
    {
        public const string RootName="Chapter Seven - Cartoon Snowfall";
        private const string MaterialPath=ChapterSevenVaultAssets.Output+"/Snow/Faceted cartoon snow.mat";

        [MenuItem("Crowd Punch/Art Direction/Add Chapter Seven Cartoon Snowfall")]
        public static void Refresh()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning ||
                Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save scenes and stop Play/baking before adding snow.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int stage=61;stage<=70;stage++)
                {
                    var main=EditorSceneManager.OpenScene(ChapterSevenVaultBuilder.MainPath(stage));
                    var sub=EditorSceneManager.OpenScene(ChapterSevenVaultBuilder.SubPath(stage),OpenSceneMode.Additive);
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
            var shader=Shader.Find("CrowdPunch/Chapter Seven Cartoon Snow");
            if(shader==null)throw new InvalidOperationException("Cartoon snow shader has not imported.");
            if(!AssetDatabase.IsValidFolder(ChapterSevenVaultAssets.Output+"/Snow"))
                AssetDatabase.CreateFolder(ChapterSevenVaultAssets.Output,"Snow");
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,MaterialPath);}
            var root=new GameObject(RootName);SceneManager.MoveGameObjectToScene(root,scene);
            root.transform.position=new Vector3(court.center.x,court.max.y+14,court.center.z);
            var particles=root.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed=false;particles.randomSeed=(uint)(stage*107);
            var main=particles.main;main.loop=true;main.duration=10;main.prewarm=true;main.playOnAwake=true;
            main.useUnscaledTime=false;main.startLifetime=new ParticleSystem.MinMaxCurve(9,10);
            main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(.10f,.23f);
            main.startRotation=new ParticleSystem.MinMaxCurve(-Mathf.PI,Mathf.PI);
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(.84f,.94f,1,.65f),new Color(1,1,1,.85f));
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.cullingMode=ParticleSystemCullingMode.Pause;
            // Area-scaled density covers long routes as well as courts, with a fixed total budget.
            var size=new Vector3(court.size.x+36,.5f,court.size.z+36);
            var emission=particles.emission;emission.rateOverTime=Mathf.Clamp(size.x*size.z*.012f,18,100);
            main.maxParticles=Mathf.Min(1024,Mathf.CeilToInt(emission.rateOverTime.constant*10)+24);
            var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=size;
            var velocity=particles.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
            velocity.x=new ParticleSystem.MinMaxCurve(.12f,.38f);velocity.y=new ParticleSystem.MinMaxCurve(-1.4f,-1.1f);
            velocity.z=new ParticleSystem.MinMaxCurve(-.16f,.12f);
            var noise=particles.noise;noise.enabled=true;noise.separateAxes=true;
            noise.strengthX=.18f;noise.strengthY=0;noise.strengthZ=.18f;
            noise.frequency=.25f;noise.scrollSpeed=.12f;noise.quality=ParticleSystemNoiseQuality.Low;
            var rotation=particles.rotationOverLifetime;rotation.enabled=true;rotation.z=new ParticleSystem.MinMaxCurve(-.35f,.35f);
            var color=particles.colorOverLifetime;color.enabled=true;
            var fade=new Gradient();fade.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(1,.75f),new GradientAlphaKey(0,1)});
            color.color=fade;
            var renderer=root.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
            renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.sortMode=ParticleSystemSortMode.Distance;
            renderer.allowRoll=false;renderer.maxParticleSize=.012f;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
        }
    }
}
