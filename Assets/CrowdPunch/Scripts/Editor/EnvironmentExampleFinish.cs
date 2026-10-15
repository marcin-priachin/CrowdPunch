using System;
using System.IO;
using System.Linq;
using CrowdPunch.Mono.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    public static partial class EnvironmentExamplesBuilder
    {
        [MenuItem("Crowd Punch/Levels/Polish Example Environments 28-31")]
        public static void PolishEnvironments()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            for (int i=0;i<SceneManager.sceneCount;i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            modelPaths=AssetDatabase.FindAssets("t:Model",new[]{Root+"Models"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Directory.CreateDirectory(Output+"Atmosphere");
            AssetDatabase.Refresh();
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int theme=0;theme<4;theme++)
                {
                    int n=28+theme;
                    var sub=EditorSceneManager.OpenScene($"{Scenes}Gauntlet_{n}/Gauntlet_{n} Sub Scene.unity");
                    var layout=sub.GetRootGameObjects().Single(g=>g.name.StartsWith("Layout - ")).transform;
                    var floor=layout.Find("Continuous Convex Floor").GetComponent<Renderer>().bounds;
                    // Replace only the old visual backdrop; retain the actual floor and collision boundaries.
                    var backdrop=layout.Find("Presentation Backdrop");
                    if(backdrop!=null) backdrop.gameObject.SetActive(false);
                    EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
                    var main=EditorSceneManager.OpenScene($"{Scenes}Gauntlet_{n}.unity");
                    var root=main.GetRootGameObjects().Single(g=>g.name.StartsWith("Environment Example - ")).transform;
                    FinishArena(root,floor,theme);
                    EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        private static void FinishArena(Transform scenery,Bounds floor,int theme)
        {
            var previous=scenery.Find("World and atmosphere");
            if(previous!=null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var shelf=scenery.Find("Landscape shelf");
            if(shelf!=null) shelf.gameObject.SetActive(false);
            var root=new GameObject("World and atmosphere").transform;
            root.SetParent(scenery,false);
            BuildLandscape(root,theme);
            BuildDistantSetting(root,theme);
            BuildGroundDetail(root,theme);
            FinishLighting(root,theme);
            AddPracticalLights(root,floor,theme);
        }

        private static void FinishLighting(Transform root,int theme)
        {
            Color[] haze={new(.64f,.73f,.73f),new(.36f,.40f,.46f),new(.35f,.54f,.51f),new(.52f,.66f,.79f)};
            Color[] skyTint={new(.46f,.57f,.66f),new(.43f,.44f,.48f),new(.42f,.58f,.57f),new(.38f,.51f,.68f)};
            var light=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l=>l.type==LightType.Directional && l.name!="Soft sky fill");
            light.transform.rotation=Quaternion.Euler(new[]{48f,52f,55f,42f}[theme],new[]{-38f,-65f,32f,-40f}[theme],0);
            light.color=new[]{new Color(1,.96f,.88f),new Color(1,.86f,.69f),new Color(.96f,1,.93f),new Color(.91f,.95f,1)}[theme];
            light.intensity=new[]{1.9f,2f,1.8f,1.9f}[theme];
            light.shadows=LightShadows.Soft; light.shadowStrength=.8f; light.shadowBias=.04f; light.shadowNormalBias=.25f;
            var fill=root.parent.Find("Soft sky fill").GetComponent<Light>();
            fill.intensity=.25f; fill.color=new Color(.8f,.9f,1);
            string skyPath=Output+$"Atmosphere/{theme}_Sky.mat";
            var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if(sky==null) { sky=new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky,skyPath); }
            sky.SetColor("_SkyTint",skyTint[theme]); sky.SetColor("_GroundColor",haze[theme]);
            sky.SetFloat("_AtmosphereThickness",theme==1?1.25f:.8f); sky.SetFloat("_Exposure",1.1f);
            sky.SetFloat("_SunSize",.025f); sky.SetFloat("_SunSizeConvergence",5);
            EditorUtility.SetDirty(sky);
            var lighting=root.gameObject.AddComponent<ExampleEnvironmentLighting>();
            lighting.presentationPipeline=PresentationPipeline();
            lighting.sky=sky; lighting.sun=light; lighting.haze=haze[theme];
            lighting.ambientSky=new Color(.8f,.9f,1);
            lighting.ambientEquator=new Color(.55f,.65f,.68f); lighting.ambientGround=new Color(.25f,.28f,.3f);
            lighting.hazeStart=theme==2?38:55; lighting.hazeEnd=theme==2?190:theme==3?260:290;
            lighting.ApplyAtmosphere();
            var volume=root.gameObject.AddComponent<Volume>(); volume.isGlobal=true; volume.priority=100;
            string path=Output+$"Atmosphere/{theme}_Grade.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(profile==null) { profile=ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile,path); }
            foreach(var component in profile.components.ToArray()) UnityEngine.Object.DestroyImmediate(component,true);
            profile.components.Clear();
            var tone=profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var bloom=profile.Add<Bloom>(true); bloom.threshold.Override(1.1f); bloom.intensity.Override(theme==1?.4f:.22f); bloom.scatter.Override(.65f);
            var grade=profile.Add<ColorAdjustments>(true); grade.postExposure.Override(.25f); grade.contrast.Override(6); grade.saturation.Override(0);
            var balance=profile.Add<WhiteBalance>(true); balance.temperature.Override(new[]{5f,-5f,-4f,-8f}[theme]); balance.tint.Override(0);
            var vignette=profile.Add<Vignette>(true); vignette.intensity.Override(.16f); vignette.smoothness.Override(.55f);
            foreach(var component in profile.components) AssetDatabase.AddObjectToAsset(component,profile);
            EditorUtility.SetDirty(profile); volume.sharedProfile=profile;
        }

        private static void AddPracticalLights(Transform root,Bounds floor,int theme)
        {
            var glow=Material(theme,"LanternGlow",theme==1?new Color(1,.34f,.04f):theme==2?new Color(.3f,1,.73f):new Color(1,.65f,.24f),3);
            var iron=Material(theme,"LanternIron",new Color(.12f,.16f,.18f));
            for(int side=-1;side<=1;side+=2)
                for(int end=-1;end<=1;end+=2)
                {
                    var p=new Vector3(side*(floor.extents.x+1.5f),-1,end*(floor.extents.z-4));
                    Model(root,"KayKit","column",p,new Vector3(.8f,3.2f,.8f),0,theme,iron);
                    Block(root,"Lantern housing",p+Vector3.up*3.2f,new Vector3(.75f,.85f,.75f),glow);
                    Block(root,"Lantern cap",p+Vector3.up*4.05f,new Vector3(1.1f,.18f,1.1f),iron);
                    var lamp=new GameObject("Warm perimeter practical").AddComponent<Light>(); lamp.transform.SetParent(root,false);
                    lamp.transform.position=p+Vector3.up*3.65f; lamp.type=LightType.Point;
                    lamp.color=glow.color; lamp.intensity=theme==1?5:3; lamp.range=8; lamp.shadows=LightShadows.None;
                }
        }
    }
}
