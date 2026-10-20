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
using static CrowdPunch.Editor.ChapterFourObservatoryAssets;

namespace CrowdPunch.Editor
{
    public static class ChapterFourObservatoryBuilder
    {
        public const string RootName = "Chapter Four - Overgrown Observatory";
        public static readonly string[] Compositions = { "Star-reader's garden", "Survey track", "Root gallery", "Survey gates", "Astrolabe court", "Watchkeeper's approach", "Relic housing", "Great observatory terrace", "Alignment terraces", "Fallen planetarium" };
        internal static string MainPath(int stage) => $"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}.unity";
        internal static string SubPath(int stage) => $"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}/Campaign_{stage:00} Sub Scene.unity";

        [MenuItem("Crowd Punch/Art Direction/Dress Chapter Four - Overgrown Observatory")]
        public static void DressAll()
        {
            CheckReady(); var setup = EditorSceneManager.GetSceneManagerSetup();
            try { for (int stage = 31; stage <= 40; stage++) Dress(stage); }
            finally { AssetDatabase.SaveAssets(); EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        public static void Dress(int stage)
        {
            CheckReady(); if (stage < 31 || stage > 40) throw new ArgumentOutOfRangeException(nameof(stage));
            var check=EditorSceneManager.OpenScene(MainPath(stage));
            if(check.GetRootGameObjects().Any(g=>g.name==RootName))throw new InvalidOperationException("Observatory already dressed; use the appearance refresh to preserve placement.");
            Prepare();
            var sub = EditorSceneManager.OpenScene(SubPath(stage));
            var renderers = sub.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            var floors = renderers.Where(r => r.name == "Continuous Convex Floor" || r.name == "Continuous Round Floor").ToArray();
            var court = floors[0].bounds; foreach (var r in floors.Skip(1)) court.Encapsulate(r.bounds);
            var floorBounds = floors.Select(r=>r.bounds).ToArray();
            float groundHeight=floorBounds.Min(b=>b.max.y);
            var hazeEdges=SceneryHazeFloorOutline.Build(floors);
            foreach (var r in renderers)
            {
                if (floors.Contains(r)) r.sharedMaterial = Surface("Court");
                else if (r.name.StartsWith("Perimeter ") || r.name == "Nature rock") r.sharedMaterials = Enumerable.Repeat(Surface("Rock"),r.sharedMaterials.Length).ToArray();
                else if (r.name == "Entry Apron" || r.name == "Launch Lane" || r.name == "Presentation Backdrop") r.enabled = false;
                else if (r.GetComponentInParent<SolidObstacleAuthoring>() != null)
                    r.sharedMaterials = Enumerable.Repeat(Surface("Limestone"),r.sharedMaterials.Length).ToArray();
                // Keep hazards, enemies, boss skins and all effect palettes authoritative.
                r.forceMeshLod = 0;
            }
            ChapterFourObservatoryObjectives.Apply(sub);
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
            var main = EditorSceneManager.OpenScene(MainPath(stage));
            // Layout refreshes are explicit; ordinary material refreshes never rebuild scenery.
            if (main.GetRootGameObjects().Any(g => g.name == RootName)) throw new InvalidOperationException("Observatory is already dressed. Use the appearance refresh to preserve its placement.");
            var root = new GameObject(RootName).transform;
            new GameObject(Compositions[stage-31]).transform.SetParent(root,false);
            ChapterFourObservatoryScenery.Build(root, floorBounds, stage);
            var lighting = main.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CelLevelLighting>()).Single();
            lighting.sun.transform.rotation = Quaternion.Euler(50,-32,0);
            lighting.sun.color = new Color(1,.94f,.80f); lighting.sun.intensity = 1.20f; lighting.sun.shadows = LightShadows.Hard;
            lighting.sky = new Color(.27f,.38f,.44f); lighting.equator = new Color(.17f,.25f,.24f); lighting.ground = new Color(.09f,.13f,.12f);
            lighting.ambientMultiplier = 1; lighting.overrideAtmosphere = true;
            ApplyAtmosphere(main,court,groundHeight,hazeEdges);
            if (root.GetComponentsInChildren<Collider>(true).Length > 0) throw new InvalidOperationException("Decorative observatory geometry must not collide.");
            EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main); AssetDatabase.SaveAssets();
            Debug.Log($"Chapter Four {stage}: {Compositions[stage-31]} saved; bake required.");
        }

        [MenuItem("Crowd Punch/Art Direction/Refresh Chapter Four Painted Appearance")]
        public static void RefreshAppearance()
        {
            RefreshSurfaces();
            RefreshAtmosphere();
        }

        [MenuItem("Crowd Punch/Art Direction/Refresh Chapter Four Scenery Fog Only")]
        public static void RefreshAtmosphere()
        {
            CheckReady();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Output}))
                {
                    var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if(!material.HasProperty("_SceneryHaze"))continue;
                    string source=material.GetTag("CrowdPunchSourceMaterial",false,AssetDatabase.GetAssetPath(material));
                    string name=System.IO.Path.GetFileNameWithoutExtension(source);
                    float role=name=="Earth" ? 2 : name.Contains("Decorative") ? 1 : 0;
                    if(material.GetFloat("_SceneryHaze")==role)continue;
                    material.SetFloat("_SceneryHaze",role);
                    EditorUtility.SetDirty(material);
                }
                for (int stage=31; stage<=40; stage++)
                {
                    var sub = EditorSceneManager.OpenScene(SubPath(stage));
                    var floors = sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>()).Where(r=>r.name.Contains("Continuous") && r.name.Contains("Floor")).ToArray();
                    var court = floors[0].bounds; foreach(var r in floors.Skip(1)) court.Encapsulate(r.bounds);
                    float groundHeight=floors.Min(r=>r.bounds.max.y);
                    var hazeEdges=SceneryHazeFloorOutline.Build(floors);
                    var main = EditorSceneManager.OpenScene(MainPath(stage)); ApplyAtmosphere(main,court,groundHeight,hazeEdges);
                    EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        [MenuItem("Crowd Punch/Art Direction/Refresh Chapter Four Surface Mapping Only")]
        public static void RefreshSurfaces()
        {
            CheckReady(); var setup=EditorSceneManager.GetSceneManagerSetup();Prepare();
            try
            {
                foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Output+"/Materials"}))
                {
                    var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));string name=material.name;
                    Surface(name.Split(' ')[0],name.Contains("Decorative"),name.Contains("Distant"),name.Contains("Local"));
                }
                foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Output+"/BakedShadows/Receivers"}))
                {
                    var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    string path=material.GetTag("CrowdPunchSourceMaterial",false,"");
                    if(path==AssetDatabase.GetAssetPath(material))throw new InvalidOperationException("Receiver source points to itself: "+path);
                    var source=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(source==null)throw new InvalidOperationException("Missing receiver source: "+path);
                    CopyReceiverSurface(material,source);
                }
                for(int stage=31;stage<=40;stage++)foreach(string path in new[]{MainPath(stage),SubPath(stage)})
                {
                    var scene=EditorSceneManager.OpenScene(path);bool sub=path==SubPath(stage),changed=false;
                    var roots=scene.GetRootGameObjects().Where(g=>sub || g.name==RootName);
                    foreach(var renderer in roots.SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)))
                    {
                        bool rock=sub && (renderer.name.StartsWith("Perimeter ") || renderer.name=="Nature rock");
                        if(!sub)for(var t=renderer.transform;t!=null;t=t.parent)rock|=t.name.StartsWith("rock_",StringComparison.Ordinal);
                        if(!rock)continue;
                        var surface=Surface("Rock",!sub);var materials=renderer.sharedMaterials;
                        for(int i=0;i<materials.Length;i++)
                        {
                            var current=materials[i];
                            if(current!=null && current.HasProperty("_UseBakedShadowMask") && current.GetFloat("_UseBakedShadowMask")>.5f)
                                CopyReceiverSurface(current,surface);
                            else if(current!=surface){materials[i]=surface;changed=true;}
                        }
                        renderer.sharedMaterials=materials;
                    }
                    if(changed){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
                }
                AssetDatabase.SaveAssets();
            }
            finally {EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }

        [MenuItem("Crowd Punch/Art Direction/Refresh Chapter Four Court Surface Only")]
        public static void RefreshCourtSurface()
        {
            CheckReady(); Prepare();
            var source=Surface("Court");
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Output+"/BakedShadows/Receivers"}))
            {
                var receiver=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if(receiver.GetTag("CrowdPunchSourceMaterial",false,"")!=AssetDatabase.GetAssetPath(source))continue;
                CopyReceiverSurface(receiver,source);
            }
            AssetDatabase.SaveAssets();
        }

        private static void CopyReceiverSurface(Material receiver,Material source)
        {
            var mask=receiver.GetTexture("_BakedShadowMask");var st=receiver.GetVector("_BakedShadowST");
            receiver.CopyPropertiesFromMaterial(source);receiver.SetTexture("_BakedShadowMask",mask);
            receiver.SetVector("_BakedShadowST",st);receiver.SetFloat("_UseBakedShadowMask",1);
            receiver.SetOverrideTag("CrowdPunchSourceMaterial",AssetDatabase.GetAssetPath(source));EditorUtility.SetDirty(receiver);
        }

        [MenuItem("Crowd Punch/Art Direction/Bake Chapter Four Shadows")]
        public static void Bake() => CampaignShadowBake.Begin(Enumerable.Range(31,10).Select(MainPath).ToArray(),Output+"/BakedShadows");

        private static void ApplyAtmosphere(Scene scene, Bounds court, float groundHeight, Vector4[] hazeEdges)
        {
            var lighting=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CelLevelLighting>()).Single();
            lighting.haze=new Color(.57f,.68f,.63f); lighting.sceneryOnlyHaze=true;
            lighting.hazeArenaBounds=new Vector4(court.center.x,court.center.z,court.extents.x,court.extents.z);
            lighting.hazeGroundHeight=groundHeight;
            lighting.hazeArenaEdges=hazeEdges;
            string skyPath=Output+"/ObservatorySky.mat"; var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            var skyShader=Shader.Find("CrowdPunch/Cel/Painted Haze Sky");
            if(skyShader==null)throw new InvalidOperationException("Painted haze sky shader is missing.");
            if(sky==null){sky=new Material(skyShader);AssetDatabase.CreateAsset(sky,skyPath);}
            sky.shader=skyShader;sky.SetColor("_Horizon",lighting.haze);sky.SetColor("_Zenith",new Color(.65f,.74f,.72f));
            EditorUtility.SetDirty(sky); lighting.skybox=sky;
            var previousBounds=Shader.GetGlobalVector("_CrowdPunchSceneryHazeBounds");
            var previousColor=Shader.GetGlobalVector("_CrowdPunchSceneryHazeColor");
            var previousSettings=Shader.GetGlobalVector("_CrowdPunchSceneryHazeSettings");
            var previousEdges=Shader.GetGlobalVectorArray("_CrowdPunchSceneryHazeEdges");
            int previousEdgeCount=Shader.GetGlobalInt("_CrowdPunchSceneryHazeEdgeCount");
            try { lighting.Apply(); }
            finally
            {
                Shader.SetGlobalVector("_CrowdPunchSceneryHazeBounds",previousBounds);
                Shader.SetGlobalVector("_CrowdPunchSceneryHazeColor",previousColor);
                Shader.SetGlobalVector("_CrowdPunchSceneryHazeSettings",previousSettings);
                Shader.SetGlobalVectorArray("_CrowdPunchSceneryHazeEdges",previousEdges.Length>0 ? previousEdges : new Vector4[CelLevelLighting.MaxHazeEdges]);
                Shader.SetGlobalInt("_CrowdPunchSceneryHazeEdgeCount",previousEdgeCount);
            }
            EditorUtility.SetDirty(lighting);
            string path=Output+"/ObservatoryPaintedPost.asset"; var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
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
                throw new InvalidOperationException("Save open scenes and stop Play/baking before dressing the observatory.");
        }
    }
}
