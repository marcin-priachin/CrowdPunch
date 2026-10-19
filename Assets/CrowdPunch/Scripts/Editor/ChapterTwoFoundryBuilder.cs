using System;
using System.Linq;
using CrowdPunch.Mono.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
using static CrowdPunch.Editor.ChapterTwoFoundryAssets;

namespace CrowdPunch.Editor
{
    public static class ChapterTwoFoundryBuilder
    {
        public const string RootName="Chapter Two - Emberworks Foundry";
        public static readonly string[] Compositions={"Coal delivery yard","Boiler repair court","Furnace gate","Pumpworks runway",
            "Pipe junction","Freight dock","Governor courtyard","Great foundry yard","Switchback loading bay","Smelter ring"};

        [MenuItem("Crowd Punch/Art Direction/Dress Chapter Two - Emberworks Foundry")]
        public static void DressAll()
        {
            CheckReady();
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try { for(int n=11;n<=20;n++) Dress(n); }
            finally { AssetDatabase.SaveAssets(); EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        public static void Dress(int stage)
        {
            CheckReady();
            if(stage<11 || stage>20) throw new ArgumentOutOfRangeException(nameof(stage));
            Prepare();
            var sub=EditorSceneManager.OpenScene(SubPath(stage));
            var renderers=sub.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            var floors=renderers.Where(r=>r.name=="Continuous Convex Floor" || r.name=="Continuous Round Floor").ToArray();
            var bounds=floors[0].bounds; foreach(var floor in floors.Skip(1)) bounds.Encapsulate(floor.bounds);
            foreach(var r in renderers)
            {
                Material material=null;
                if(floors.Contains(r)) material=Ground;
                else if(r.name.StartsWith("Perimeter ") || r.name=="Nature rock") material=Steel;
                else if(r.name=="Entry Apron" || r.name=="Launch Lane") {r.enabled=false; continue;}
                else if(r.name=="Presentation Backdrop") material=Earth;
                if(material!=null) r.sharedMaterials=Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray();
            }
            ChapterTwoFoundryObjectives.Build(sub,stage);
            ChapterTwoPaintedLook.ApplyScene(sub,stage);
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);
            var main=EditorSceneManager.OpenScene(MainPath(stage));
            foreach(var old in main.GetRootGameObjects().Where(g=>g.name==RootName)) Object.DestroyImmediate(old);
            var lighting=main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CelLevelLighting>(true)).Single();
            var policy=lighting.GetComponent<BakedShadowLighting>();
            if(policy!=null) Object.DestroyImmediate(policy);
            Lightmapping.Clear();
            var root=new GameObject(RootName).transform;
            new GameObject(Compositions[stage-11]).transform.SetParent(root,false);
            ChapterTwoFoundryScenery.Build(root,bounds,stage);
            ConfigureLighting(lighting,bounds);
            ChapterTwoPaintedLook.ApplyScene(main,stage);
            if(root.GetComponentsInChildren<Collider>(true).Length!=0) throw new InvalidOperationException("Foundry scenery may not add collisions.");
            EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main); AssetDatabase.SaveAssets();
            Debug.Log($"Chapter Two {stage}: {Compositions[stage-11]} saved. Shadow bake required.");
        }

        internal static string MainPath(int stage)=>$"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}.unity";
        internal static string SubPath(int stage)=>$"Assets/CrowdPunch/Scenes/Campaign/Campaign_{stage:00}/Campaign_{stage:00} Sub Scene.unity";
        private static void CheckReady()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Stop Play/baking and save open scenes before refreshing foundry art.");
        }
        private static void ConfigureLighting(CelLevelLighting lighting,Bounds floor)
        {
            lighting.sun.transform.rotation=Quaternion.Euler(48,-38,0);
            lighting.sun.color=new Color(1,.89f,.73f); lighting.sun.intensity=1.35f;
            lighting.sun.shadows=LightShadows.Hard; lighting.sun.shadowStrength=1;
            lighting.sky=new Color(.28f,.37f,.49f); lighting.equator=new Color(.17f,.22f,.26f);
            lighting.ground=new Color(.08f,.09f,.11f); lighting.ambientMultiplier=1;
            lighting.overrideAtmosphere=true; lighting.haze=new Color(.56f,.64f,.68f);
            lighting.hazeStart=Mathf.Max(85,floor.extents.magnitude+40); lighting.hazeEnd=lighting.hazeStart+100;
            string path=Output+"/FoundrySky.mat";
            var sky=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(sky==null){sky=new Material(Shader.Find("Skybox/Procedural"));AssetDatabase.CreateAsset(sky,path);}
            sky.SetColor("_SkyTint",new Color(.45f,.58f,.70f)); sky.SetColor("_GroundColor",lighting.haze);
            sky.SetFloat("_AtmosphereThickness",.8f);sky.SetFloat("_Exposure",1.05f);sky.SetFloat("_SunSize",.025f);
            EditorUtility.SetDirty(sky);lighting.skybox=sky;lighting.Apply();EditorUtility.SetDirty(lighting);
        }
    }
}
