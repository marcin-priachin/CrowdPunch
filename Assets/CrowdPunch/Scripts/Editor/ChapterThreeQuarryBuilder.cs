using System;
using System.Linq;
using CrowdPunch.Mono.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using static CrowdPunch.Editor.ChapterThreeQuarryAssets;

namespace CrowdPunch.Editor
{
    public static class ChapterThreeQuarryBuilder
    {
        public const string RootName = "Chapter Three - Highland Quarry and Aqueduct";
        public static readonly string[] Compositions = { "Armorer's stone court", "Quarry terraces", "Aqueduct crossing", "Supply causeway", "Geothermal cut", "Winch court", "Kiln walk", "Grand quarry basin", "Ruined valve rotunda", "Rolling pit" };
        internal static string MainPath(int stage) => $"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}.unity";
        internal static string SubPath(int stage) => $"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}/Campaign_{stage:00} Sub Scene.unity";

        [MenuItem("Crowd Punch/Art Direction/Dress Chapter Three - Highland Quarry")]
        public static void DressAll()
        {
            CheckReady(); var setup = EditorSceneManager.GetSceneManagerSetup();
            try { for (int stage = 21; stage <= 30; stage++) Dress(stage); }
            finally { AssetDatabase.SaveAssets(); EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        public static void Dress(int stage)
        {
            CheckReady(); if (stage < 21 || stage > 30) throw new ArgumentOutOfRangeException(nameof(stage));
            var check=EditorSceneManager.OpenScene(MainPath(stage));
            if(check.GetRootGameObjects().Any(g=>g.name==RootName))throw new InvalidOperationException("Quarry already dressed; use the appearance refresh to preserve placement.");
            Prepare();
            var sub = EditorSceneManager.OpenScene(SubPath(stage));
            var renderers = sub.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            var floors = renderers.Where(r => r.name == "Continuous Convex Floor" || r.name == "Continuous Round Floor").ToArray();
            var court = floors[0].bounds; foreach (var r in floors.Skip(1)) court.Encapsulate(r.bounds);
            var floorBounds = floors.Select(r=>r.bounds).ToArray();
            foreach (var r in renderers)
            {
                if (floors.Contains(r)) r.sharedMaterial = Surface("Court");
                else if (r.name.StartsWith("Perimeter ") || r.name == "Nature rock") r.sharedMaterials = Enumerable.Repeat(Surface("Cliff"),r.sharedMaterials.Length).ToArray();
                else if (r.name == "Entry Apron" || r.name == "Launch Lane" || r.name == "Presentation Backdrop") r.enabled = false;
                // Keep hazards, enemies, boss skins and all effect palettes authoritative.
                r.forceMeshLod = 0;
            }
            ChapterThreeQuarryObjectives.Apply(sub);
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
            var main = EditorSceneManager.OpenScene(MainPath(stage));
            // Layout refreshes are explicit; ordinary material refreshes never rebuild scenery.
            if (main.GetRootGameObjects().Any(g => g.name == RootName)) throw new InvalidOperationException("Quarry is already dressed. Use the appearance refresh to preserve its placement.");
            var root = new GameObject(RootName).transform;
            new GameObject(Compositions[stage-21]).transform.SetParent(root,false);
            ChapterThreeQuarryScenery.Build(root, floorBounds, stage);
            var lighting = main.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CelLevelLighting>()).Single();
            lighting.sun.transform.rotation = Quaternion.Euler(48,-38,0);
            lighting.sun.color = new Color(1,.95f,.83f); lighting.sun.intensity = 1.25f; lighting.sun.shadows = LightShadows.Hard;
            lighting.sky = new Color(.29f,.39f,.50f); lighting.equator = new Color(.20f,.25f,.28f); lighting.ground = new Color(.10f,.12f,.13f);
            lighting.ambientMultiplier = 1; lighting.overrideAtmosphere = true;
            ApplyAtmosphere(main,court);
            if (root.GetComponentsInChildren<Collider>(true).Length > 0) throw new InvalidOperationException("Decorative quarry geometry must not collide.");
            EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main); AssetDatabase.SaveAssets();
            Debug.Log($"Chapter Three {stage}: {Compositions[stage-21]} saved; bake required.");
        }

        [MenuItem("Crowd Punch/Art Direction/Refresh Chapter Three Painted Appearance")]
        public static void RefreshAppearance()
        {
            CheckReady(); var setup = EditorSceneManager.GetSceneManagerSetup(); Prepare();
            try
            {
                foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Output+"/Materials"}))
                {
                    var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    string name=material.name;
                    Surface(name.Split(' ')[0],name.Contains("Decorative"),name.Contains("Distant"),name.Contains("Local"));
                }
                for (int stage=21; stage<=30; stage++)
                {
                    var sub = EditorSceneManager.OpenScene(SubPath(stage));
                    var floors = sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>()).Where(r=>r.name.Contains("Continuous") && r.name.Contains("Floor")).ToArray();
                    var court = floors[0].bounds; foreach(var r in floors.Skip(1)) court.Encapsulate(r.bounds);
                    var main = EditorSceneManager.OpenScene(MainPath(stage)); ApplyAtmosphere(main,court);
                    EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                }
                // Reassert palette values in place. Existing explicit receivers keep mask/ST/GUIDs.
                foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Output}))
                {
                    var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    string source=material.GetTag("CrowdPunchSourceMaterial",false,"");
                    if(string.IsNullOrEmpty(source)) continue;
                    var original=AssetDatabase.LoadAssetAtPath<Material>(source); if(original==null)continue;
                    var mask=material.GetTexture("_BakedShadowMask"); var st=material.GetVector("_BakedShadowST");
                    material.CopyPropertiesFromMaterial(original); material.SetTexture("_BakedShadowMask",mask);
                    material.SetVector("_BakedShadowST",st); material.SetFloat("_UseBakedShadowMask",1);
                    material.SetOverrideTag("CrowdPunchSourceMaterial",source); EditorUtility.SetDirty(material);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        [MenuItem("Crowd Punch/Art Direction/Bake Chapter Three Shadows")]
        public static void Bake() => CampaignShadowBake.Begin(Enumerable.Range(21,10).Select(MainPath).ToArray(),Output+"/BakedShadows");

        private static void ApplyAtmosphere(Scene scene, Bounds court)
        {
            var lighting=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CelLevelLighting>()).Single();
            lighting.haze=new Color(.63f,.70f,.72f); lighting.hazeStart=Mathf.Max(52,court.extents.magnitude+30); lighting.hazeEnd=lighting.hazeStart+103;
            string skyPath=Output+"/HighlandSky.mat"; var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if(sky==null){sky=new Material(Shader.Find("Skybox/Procedural"));AssetDatabase.CreateAsset(sky,skyPath);}
            sky.SetColor("_SkyTint",new Color(.48f,.61f,.72f));sky.SetColor("_GroundColor",lighting.haze);
            sky.SetFloat("_Exposure",.9f);sky.SetFloat("_AtmosphereThickness",.8f);sky.SetFloat("_SunSize",.015f);
            EditorUtility.SetDirty(sky); lighting.skybox=sky; lighting.Apply(); EditorUtility.SetDirty(lighting);
            string path=Output+"/HighlandPaintedPost.asset"; var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(profile==null){AssetDatabase.CopyAsset("Assets/CrowdPunch/Data/Presentation/FullCelPost.asset",path);profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);}
            profile.TryGet<ColorAdjustments>(out var grade);grade.postExposure.Override(.18f);grade.contrast.Override(3);grade.saturation.Override(-5);
            profile.TryGet<Bloom>(out var bloom);bloom.intensity.Override(.015f);
            if(!profile.TryGet<SplitToning>(out var split)){split=profile.Add<SplitToning>();AssetDatabase.AddObjectToAsset(split,profile);}
            split.shadows.Override(new Color(.485f,.495f,.525f));split.highlights.Override(new Color(.52f,.505f,.48f));split.balance.Override(0);
            foreach(var c in profile.components)EditorUtility.SetDirty(c);EditorUtility.SetDirty(profile);
            var volume=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Volume>()).Single(v=>v.priority==150);
            volume.sharedProfile=profile;EditorUtility.SetDirty(volume);
        }

        private static void CheckReady()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save open scenes and stop Play/baking before dressing the quarry.");
        }
    }
}
