using System;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Mono.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using static CrowdPunch.Editor.ChapterSevenVaultAssets;

namespace CrowdPunch.Editor
{
    // VISION-004..006: author presentation in the saved campaign, preserving gameplay.
    public static class ChapterSevenVaultBuilder
    {
        public const string RootName="Chapter Seven - Frostbound Vault";
        public static readonly string[] Compositions={"Thawline court","Vault ascent","Meltwater galleries","Frozen reliquary","Three-buttress court","Regulator rotunda","Rime garden","Great glacial basin","Crystal delivery route","Frozen coliseum"};
        internal static string MainPath(int stage)=>$"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}.unity";
        internal static string SubPath(int stage)=>$"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}/Campaign_{stage:00} Sub Scene.unity";

        [MenuItem("Crowd Punch/Art Direction/Dress Chapter Seven - Frostbound Vault")]
        public static void DressAll()
        {
            CheckReady();var setup=EditorSceneManager.GetSceneManagerSetup();
            try{for(int stage=61;stage<=70;stage++)Dress(stage);}
            finally{AssetDatabase.SaveAssets();EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }

        public static void Dress(int stage)
        {
            CheckReady();if(stage<61 || stage>70)throw new ArgumentOutOfRangeException(nameof(stage));
            var main=EditorSceneManager.OpenScene(MainPath(stage));
            if(main.GetRootGameObjects().Any(g=>g.name==RootName))throw new InvalidOperationException("Vault already dressed; refresh surfaces to preserve placement.");
            Prepare();var sub=EditorSceneManager.OpenScene(SubPath(stage));
            var renderers=sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            var floors=renderers.Where(r=>r.name=="Continuous Convex Floor" || r.name=="Continuous Round Floor").ToArray();
            var bounds=floors.Select(r=>r.bounds).ToArray();
            foreach(var r in renderers)
            {
                if(floors.Contains(r))r.sharedMaterial=Surface("Court");
                else if(r.name.StartsWith("Perimeter ") || r.name=="Nature rock")r.sharedMaterials=Enumerable.Repeat(Surface("Rock"),r.sharedMaterials.Length).ToArray();
                else if(r.name=="Entry Apron" || r.name=="Launch Lane" || r.name=="Presentation Backdrop")r.enabled=false;
                else if(r.GetComponentInParent<SolidObstacleAuthoring>()!=null)r.sharedMaterials=Enumerable.Repeat(Surface("Limestone"),r.sharedMaterials.Length).ToArray();
                r.forceMeshLod=0;
            }
            ChapterSevenVaultObjectives.Apply(sub);
            EditorSceneManager.MarkSceneDirty(sub);EditorSceneManager.SaveScene(sub);
            main=EditorSceneManager.OpenScene(MainPath(stage));
            var root=new GameObject(RootName).transform;
            new GameObject(Compositions[stage-61]).transform.SetParent(root,false);
            ChapterSevenVaultScenery.Build(root,bounds,stage);
            var court=bounds[0];foreach(var b in bounds.Skip(1))court.Encapsulate(b);
            Atmosphere(main,court);
            if(root.GetComponentsInChildren<Collider>(true).Length!=0)throw new InvalidOperationException("Vault scenery must not collide.");
            EditorSceneManager.MarkSceneDirty(main);EditorSceneManager.SaveScene(main);AssetDatabase.SaveAssets();
            Debug.Log($"Chapter Seven {stage}: {Compositions[stage-61]} saved; bake required.");
        }

        [MenuItem("Crowd Punch/Art Direction/Refresh Chapter Seven Painted Surfaces")]
        public static void RefreshSurfaces()
        {
            CheckReady();Prepare();
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Output+"/Materials"}))
            {
                var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                Surface(m.name.Split(' ')[0],m.name.Contains("Decorative"),m.name.Contains("Distant"),m.name.Contains("Local"));
            }
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Output+"/BakedShadows/Receivers"}))
            {
                var receiver=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                string path=receiver.GetTag("CrowdPunchSourceMaterial",false,"");
                var source=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(source==null || source==receiver)throw new InvalidOperationException("Invalid receiver source: "+path);
                var mask=receiver.GetTexture("_BakedShadowMask");var st=receiver.GetVector("_BakedShadowST");
                receiver.CopyPropertiesFromMaterial(source);receiver.SetTexture("_BakedShadowMask",mask);receiver.SetVector("_BakedShadowST",st);
                receiver.SetFloat("_UseBakedShadowMask",1);receiver.SetOverrideTag("CrowdPunchSourceMaterial",path);EditorUtility.SetDirty(receiver);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Crowd Punch/Art Direction/Bake Chapter Seven Shadows")]
        public static void Bake()=>CampaignShadowBake.Begin(Enumerable.Range(61,10).Select(MainPath).ToArray(),Output+"/BakedShadows");

        private static void Atmosphere(Scene scene,Bounds court)
        {
            var lighting=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CelLevelLighting>()).Single();
            lighting.sun.transform.rotation=Quaternion.Euler(46,-32,0);lighting.sun.color=new Color(.88f,.95f,1);
            lighting.sun.intensity=1.12f;lighting.sun.shadows=LightShadows.Hard;
            lighting.sky=new Color(.29f,.38f,.52f);lighting.equator=new Color(.18f,.24f,.34f);lighting.ground=new Color(.10f,.14f,.20f);
            lighting.ambientMultiplier=1;lighting.overrideAtmosphere=true;lighting.sceneryOnlyHaze=false;
            lighting.haze=new Color(.65f,.75f,.82f);lighting.hazeStart=Mathf.Max(52,court.extents.magnitude+30);lighting.hazeEnd=lighting.hazeStart+103;
            string skyPath=Output+"/VaultSky.mat";var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if(sky==null){sky=new Material(Shader.Find("CrowdPunch/Cel/Painted Haze Sky"));AssetDatabase.CreateAsset(sky,skyPath);}
            sky.SetColor("_Horizon",lighting.haze);sky.SetColor("_Zenith",new Color(.32f,.48f,.65f));EditorUtility.SetDirty(sky);lighting.skybox=sky;
            lighting.Apply();EditorUtility.SetDirty(lighting);
            string path=Output+"/VaultPaintedPost.asset";var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(profile==null){AssetDatabase.CopyAsset("Assets/CrowdPunch/Data/Presentation/FullCelPost.asset",path);profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);}
            profile.TryGet<ColorAdjustments>(out var grade);grade.postExposure.Override(.18f);grade.contrast.Override(3);grade.saturation.Override(-5);
            profile.TryGet<Bloom>(out var bloom);bloom.intensity.Override(.015f);
            foreach(var c in profile.components)EditorUtility.SetDirty(c);EditorUtility.SetDirty(profile);
            var volume=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Volume>()).Single(v=>v.priority==150);
            volume.sharedProfile=profile;EditorUtility.SetDirty(volume);
        }

        private static void CheckReady()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save scenes and stop Play/baking before dressing the vault.");
        }
    }
}
