using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CrowdPunch.Mono.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using static CrowdPunch.Editor.ChapterOneVillageAssets;

namespace CrowdPunch.Editor
{
    // VISION-004/005/006: visual-only dressing of existing saved campaign encounters.
    public static class ChapterOneVillageBuilder
    {
        private const string Scenes = "Assets/CrowdPunch/Scenes/Campaign/";
        internal const string RootName = "Chapter One - Sunlit Border Village";
        public static readonly string[] Settings = {
            "Orchard Commons", "Stonecutters Lane", "Market Court", "West Village Gate",
            "Caravan Square", "Mill Courtyard", "Bell Tower Garden", "Harvest Fairgrounds",
            "Watchtower Approach", "Gatekeepers Keep" };
        private static SceneSetup[] previousSetup;
        public static int Completed { get; private set; }
        public static bool Running { get; private set; }
        public static string Error { get; private set; }

        [MenuItem("Crowd Punch/Art Direction/Dress Chapter One - Sunlit Village")]
        public static void Begin()
        {
            if (Running || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Finish the current operation before authoring.");
            for (int i=0;i<SceneManager.sceneCount;i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
            previousSetup = EditorSceneManager.GetSceneManagerSetup();
            Prepare();
            Completed=0; Error=null; Running=true;
            EditorApplication.update += Step;
        }

        private static void Step()
        {
            try
            {
                if (Completed == 10) { Finish(); return; }
                Dress(Completed+1);
                Completed++;
            }
            catch (Exception exception)
            {
                Error=exception.ToString(); Debug.LogException(exception); Finish();
            }
        }

        private static void Finish()
        {
            EditorApplication.update -= Step;
            Running=false;
            AssetDatabase.SaveAssets();
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }

        private static void Dress(int number)
        {
            var sub = EditorSceneManager.OpenScene($"{Scenes}Campaign_{number:00}/Campaign_{number:00} Sub Scene.unity");
            var renderers = sub.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            var floor = renderers.Single(r=>r.name=="Continuous Convex Floor" || r.name=="Continuous Round Floor");
            var bounds = floor.bounds;
            var floorMesh=floor.GetComponent<MeshFilter>().sharedMesh;
            var outline=ConvexOutline(floorMesh.vertices.Select(v=>floor.transform.TransformPoint(v))
                .Where(v=>Mathf.Abs(v.y-bounds.max.y)<.01f)
                .Select(v=>new Vector2(v.x,v.z)).Distinct().ToArray());
            var paving=Surface("Warm courtyard paving",new Color(.52f,.47f,.33f));
            var stone=Surface("Limestone",new Color(.64f,.59f,.43f));
            var foundation=Surface("Foundation",new Color(.32f,.34f,.23f));
            floor.sharedMaterials = Enumerable.Range(0,floor.sharedMaterials.Length).Select(i=>i==0?paving:foundation).ToArray();
            foreach(var renderer in renderers)
            {
                if(renderer.name=="Presentation Backdrop") renderer.enabled=false;
                if(renderer.name.StartsWith("Perimeter ") || renderer.name=="Nature rock")
                    renderer.sharedMaterials=Enumerable.Repeat(stone,renderer.sharedMaterials.Length).ToArray();
                if(renderer.name=="Entry Apron" || renderer.name=="Launch Lane")
                    renderer.sharedMaterials=Enumerable.Repeat(paving,renderer.sharedMaterials.Length).ToArray();
            }
            // Keep existing damage bands, cracks, exit cues and all collider/authoring data.
            foreach(var renderer in renderers.Where(r=>r.name=="Solid plate"))
                renderer.sharedMaterials=Enumerable.Repeat(Surface("Gate timber",new Color(.48f,.29f,.13f)),renderer.sharedMaterials.Length).ToArray();
            EditorSceneManager.MarkSceneDirty(sub); EditorSceneManager.SaveScene(sub);

            var scene=EditorSceneManager.OpenScene($"{Scenes}Campaign_{number:00}.unity");
            var existing=scene.GetRootGameObjects().FirstOrDefault(r=>r.name==RootName);
            if(existing!=null) Object.DestroyImmediate(existing);
            var scenery=new GameObject(RootName).transform;
            new GameObject($"{number:00} - {Settings[number-1]}").transform.SetParent(scenery,false);
            ChapterOneVillageLandscape.Build(scenery,bounds,number);
            ChapterOneVillageLandscape.Paving(scenery,bounds,outline,number);
            ChapterOneVillageScenery.Build(scenery,bounds,number);
            ConfigureLighting(scene,scenery,bounds,number);
            if(scenery.GetComponentsInChildren<Collider>(true).Length!=0)
                throw new InvalidOperationException("Village dressing must remain non-colliding.");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log($"Chapter One {number:00}: {Settings[number-1]}, floor {bounds.size}, visual dressing only.");
        }

        private static Vector2[] ConvexOutline(Vector2[] points)
        {
            // Round floor meshes also contain centre vertices: angular sorting is insufficient.
            var sorted=points.OrderBy(p=>p.x).ThenBy(p=>p.y).ToArray();
            var hull=new List<Vector2>();
            float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
            foreach(var point in sorted)
            {
                while(hull.Count>=2 && Cross(hull[hull.Count-2],hull[hull.Count-1],point)<=0) hull.RemoveAt(hull.Count-1);
                hull.Add(point);
            }
            int lower=hull.Count;
            for(int i=sorted.Length-2;i>=0;i--)
            {
                while(hull.Count>lower && Cross(hull[hull.Count-2],hull[hull.Count-1],sorted[i])<=0) hull.RemoveAt(hull.Count-1);
                hull.Add(sorted[i]);
            }
            if(hull.Count>1) hull.RemoveAt(hull.Count-1);
            return hull.ToArray();
        }

        private static void ConfigureLighting(Scene scene,Transform scenery,Bounds floor,int number)
        {
            var lighting=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CelLevelLighting>(true)).Single();
            var sun=lighting.sun;
            sun.transform.rotation=Quaternion.Euler(48+(number%3)*3,-38+(number-1)*5,0);
            sun.color=new Color(1,.96f,.87f); sun.intensity=1.65f;
            sun.shadows=LightShadows.Soft; sun.shadowStrength=.8f; sun.shadowBias=.04f; sun.shadowNormalBias=.25f;
            lighting.sky=new Color(.65f,.76f,.87f);
            lighting.equator=new Color(.46f,.52f,.46f);
            lighting.ground=new Color(.23f,.25f,.17f);
            lighting.ambientMultiplier=1.08f;
            lighting.overrideAtmosphere=true;
            lighting.haze=new Color(.67f,.76f,.74f);
            lighting.hazeStart=Mathf.Max(70,floor.extents.magnitude+40);
            lighting.hazeEnd=lighting.hazeStart+210;
            string path=Output+"/VillageSky.mat";
            var sky=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(sky==null)
            {
                sky=new Material(Shader.Find("Skybox/Procedural"));
                sky.SetColor("_SkyTint",new Color(.46f,.60f,.72f));
                sky.SetColor("_GroundColor",lighting.haze);
                sky.SetFloat("_AtmosphereThickness",.8f); sky.SetFloat("_Exposure",1.1f);
                sky.SetFloat("_SunSize",.025f);
                AssetDatabase.CreateAsset(sky,path);
            }
            lighting.skybox=sky; lighting.Apply(); EditorUtility.SetDirty(lighting);
            // Retain the shared Full Cel profile at priority 150; this only warms white balance.
            path=Output+"/VillageWarmth.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(profile==null)
            {
                profile=ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile,path);
                var warmth=profile.Add<WhiteBalance>(true); warmth.temperature.Override(4); warmth.tint.Override(0);
                AssetDatabase.AddObjectToAsset(warmth,profile); EditorUtility.SetDirty(profile);
            }
            var volume=scenery.gameObject.AddComponent<Volume>(); volume.isGlobal=true; volume.priority=160; volume.sharedProfile=profile;
        }
    }
}
