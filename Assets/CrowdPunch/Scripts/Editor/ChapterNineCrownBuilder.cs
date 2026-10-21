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
using static CrowdPunch.Editor.ChapterNineCrownAssets;

namespace CrowdPunch.Editor
{
    // VISION-004..006, FINALE-001..007: presentation owns no encounter state.
    public static class ChapterNineCrownBuilder
    {
        public const string RootName = "Chapter Nine - Crown Above the Clouds";
        public const string MainPath = "Assets/CrowdPunch/Scenes/Campaign/Campaign_81.unity";
        public const string SubPath = "Assets/CrowdPunch/Scenes/Campaign/Campaign_81/Campaign_81 Sub Scene.unity";

        [MenuItem("Crowd Punch/Art Direction/Dress Chapter Nine - Crown Above the Clouds")]
        public static void Dress()
        {
            CheckReady(); var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var main = EditorSceneManager.OpenScene(MainPath);
                if(main.GetRootGameObjects().Any(g => g.name == RootName))
                    throw new InvalidOperationException("Crown already dressed; refresh surfaces to preserve its composition.");
                Prepare();
                var sub = EditorSceneManager.OpenScene(SubPath);
                foreach(var r in sub.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)))
                {
                    if(r.name == "Continuous Convex Floor") r.sharedMaterial = Surface("Court");
                    else if(r.name.StartsWith("Perimeter ") || r.name == "Nature rock")
                        r.sharedMaterials = Enumerable.Repeat(Surface("Rock"),r.sharedMaterials.Length).ToArray();
                    else if(r.name == "Entry Apron" || r.name == "Launch Lane" || r.name == "Presentation Backdrop") r.enabled = false;
                    else if(r.GetComponentInParent<SolidObstacleAuthoring>() != null)
                        r.sharedMaterials = Enumerable.Repeat(Surface("Limestone"),r.sharedMaterials.Length).ToArray();
                    r.forceMeshLod = 0;
                }
                ChapterNineCrownObjectives.Apply(sub);
                EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
                main = EditorSceneManager.OpenScene(MainPath);
                var root = new GameObject(RootName).transform;
                ChapterNineCrownScenery.Build(root);
                Atmosphere(main);
                if(root.GetComponentsInChildren<Collider>(true).Length != 0) throw new InvalidOperationException("Scenery must be noncolliding.");
                EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main); AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        [MenuItem("Crowd Punch/Art Direction/Bake Chapter Nine Shadows")]
        public static void Bake() => CampaignShadowBake.Begin(new[]{MainPath},Output + "/BakedShadows");

        [MenuItem("Crowd Punch/Art Direction/Refresh Chapter Nine Marble Finish")]
        public static void RefreshMarble()
        {
            CheckReady();var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                RefreshSurfaces();
                var main=EditorSceneManager.OpenScene(MainPath);
                var root=main.GetRootGameObjects().Single(g=>g.name==RootName);
                foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    // Separate construction planes through pigment while retaining every
                    // transform, mesh, native lightmap index and UV chart binding.
                    if(r.name=="Gallery rear wall" || r.name=="Gallery return" || r.name=="Royal palace" || r.name=="Observatory rotunda")
                        r.sharedMaterial=Surface("Facade",true);
                    else if(r.name=="Royal terrace") r.sharedMaterial=Surface("Terrace");
                }
                EditorSceneManager.MarkSceneDirty(main);EditorSceneManager.SaveScene(main);AssetDatabase.SaveAssets();
            }
            finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }

        [MenuItem("Crowd Punch/Art Direction/Refresh Chapter Nine Painted Surfaces")]
        public static void RefreshSurfaces()
        {
            CheckReady(); Prepare();
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Output + "/Materials"}))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                Surface(m.name.Split(' ')[0],m.name.Contains("Decorative"),m.name.Contains("Distant"),m.name.Contains("Local"));
            }
            string folder = Output + "/BakedShadows/Receivers";
            if(AssetDatabase.IsValidFolder(folder)) foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{folder}))
            {
                var receiver = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                string path = receiver.GetTag("CrowdPunchSourceMaterial",false,"");
                var source = AssetDatabase.LoadAssetAtPath<Material>(path);
                if(source == null || source == receiver) throw new InvalidOperationException("Invalid receiver source: " + path);
                var mask = receiver.GetTexture("_BakedShadowMask"); var st = receiver.GetVector("_BakedShadowST");
                receiver.CopyPropertiesFromMaterial(source); receiver.SetTexture("_BakedShadowMask",mask); receiver.SetVector("_BakedShadowST",st);
                receiver.SetFloat("_UseBakedShadowMask",1); receiver.SetOverrideTag("CrowdPunchSourceMaterial",path); EditorUtility.SetDirty(receiver);
            }
            AssetDatabase.SaveAssets();
        }

        private static void Atmosphere(Scene scene)
        {
            var lighting = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CelLevelLighting>()).Single();
            lighting.sun.transform.rotation = Quaternion.Euler(48,-35,0); lighting.sun.color = new Color(1,.95f,.84f);
            lighting.sun.intensity = 1.12f; lighting.sun.shadows = LightShadows.Hard;
            lighting.sky = new Color(.40f,.52f,.68f); lighting.equator = new Color(.28f,.36f,.48f); lighting.ground = new Color(.18f,.23f,.31f);
            lighting.ambientMultiplier = 1; lighting.overrideAtmosphere = true; lighting.sceneryOnlyHaze = false;
            lighting.haze = new Color(.72f,.82f,.92f); lighting.hazeStart = 101; lighting.hazeEnd = 204;
            var sky = new Material(Shader.Find("CrowdPunch/Cel/Painted Haze Sky"));
            sky.SetColor("_Horizon",lighting.haze); sky.SetColor("_Zenith",new Color(.31f,.52f,.77f));
            AssetDatabase.CreateAsset(sky,Output + "/CrownSky.mat"); lighting.skybox = sky; lighting.Apply(); EditorUtility.SetDirty(lighting);
            string path = Output + "/CrownPaintedPost.asset";
            AssetDatabase.CopyAsset("Assets/CrowdPunch/Data/Presentation/FullCelPost.asset",path);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            profile.TryGet<ColorAdjustments>(out var grade); grade.postExposure.Override(.18f); grade.contrast.Override(3); grade.saturation.Override(-5);
            profile.TryGet<Bloom>(out var bloom); bloom.intensity.Override(.015f);
            if(!profile.TryGet<SplitToning>(out var split)) split = profile.Add<SplitToning>();
            split.shadows.Override(new Color(.46f,.49f,.55f)); split.highlights.Override(new Color(.54f,.52f,.48f)); split.balance.Override(0);
            foreach(var c in profile.components) EditorUtility.SetDirty(c); EditorUtility.SetDirty(profile);
            var volume = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Volume>()).Single(v => v.priority == 150);
            volume.sharedProfile = profile; EditorUtility.SetDirty(volume);
        }

        private static void CheckReady()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || Enumerable.Range(0,SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save scenes and finish Play/baking before dressing the crown.");
        }
    }
}
