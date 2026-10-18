using System;
using System.IO;
using System.Linq;
using CrowdPunch.Mono.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace CrowdPunch.Editor
{
    // VISION-006: native shadowmasks, shared UV2 meshes and no runtime environment shadow pass.
    public static class ChapterOneShadowBake
    {
        private const string Output=ChapterOneVillageAssets.Output+"/BakedShadows";
        private static SceneSetup[] previousSetup;
        private static int[] stages;
        private static bool awaitingBake, bakeCompleted;
        public static bool Running { get; private set; }
        public static int Completed { get; private set; }
        public static string Error { get; private set; }

        [MenuItem("Crowd Punch/Art Direction/Bake Chapter One Shadows")]
        public static void BakeAll() => Begin(Enumerable.Range(1,10).ToArray());

        public static void Begin(params int[] numbers)
        {
            if(Running || Lightmapping.isRunning || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Finish the current operation before baking.");
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save open scenes before baking.");
            if(numbers.Length==0 || numbers.Any(n=>n<1 || n>10)) throw new ArgumentOutOfRangeException(nameof(numbers));
            previousSetup=EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory(Output+"/Meshes"); AssetDatabase.Refresh();
            stages=numbers; Completed=0; Error=null; awaitingBake=false; Running=true;
            Lightmapping.bakeCompleted+=OnBaked;
            EditorApplication.update+=Step;
        }

        private static void OnBaked() => bakeCompleted=true;

        private static void Step()
        {
            try
            {
                if(awaitingBake)
                {
                    if(Lightmapping.isRunning || !bakeCompleted) return;
                    if(!LightmapSettings.lightmaps.Any(l=>l.shadowMask!=null))
                        throw new InvalidOperationException("Bake produced no shadow masks; realtime shadows have not been disabled.");
                    PreserveLightmapResolution();
                    for(int i=0;i<SceneManager.sceneCount;i++)
                    {
                        var sub=SceneManager.GetSceneAt(i);
                        if(sub.path.EndsWith(" Sub Scene.unity",StringComparison.Ordinal)) ChapterOneShadowReceivers.Bind(sub);
                    }
                    var scene=SceneManager.GetActiveScene();
                    var lighting=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CelLevelLighting>(true)).Single();
                    if(lighting.GetComponent<BakedShadowLighting>()==null) lighting.gameObject.AddComponent<BakedShadowLighting>();
                    lighting.GetComponent<BakedShadowLighting>().bakedProbes=LightmapSettings.lightProbes;
                    EditorSceneManager.MarkAllScenesDirty(); EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
                    Debug.Log($"Chapter One {stages[Completed]:00}: baked {LightmapSettings.lightmaps.Length} lightmap/shadowmask atlases.");
                    Completed++; awaitingBake=false;
                    if(Completed==stages.Length) Finish();
                    return;
                }
                Prepare(stages[Completed]);
                bakeCompleted=false;
                awaitingBake=true;
                if(!Lightmapping.BakeAsync()) throw new InvalidOperationException("Unity refused to start the light bake.");
            }
            catch(Exception exception) { Error=exception.ToString(); Debug.LogException(exception); Finish(); }
        }

        private static void Finish()
        {
            EditorApplication.update-=Step; Lightmapping.bakeCompleted-=OnBaked;
            Running=false;
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }

        private static void Prepare(int number)
        {
            string main=$"Assets/CrowdPunch/Scenes/Campaign/Campaign_{number:00}.unity";
            var scene=EditorSceneManager.OpenScene(main);
            var sub=EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{number:00}/Campaign_{number:00} Sub Scene.unity",OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var lighting=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CelLevelLighting>(true)).Single();
            // A cancelled/failed rebake must not leave a level forcing missing visibility.
            var oldPolicy=lighting.GetComponent<BakedShadowLighting>();
            if(oldPolicy!=null) Object.DestroyImmediate(oldPolicy);
            lighting.Apply(); lighting.sun.lightmapBakeType=LightmapBakeType.Mixed;
            lighting.sun.shadows=LightShadows.Hard; lighting.sun.shadowAngle=0;
            lighting.sun.gameObject.isStatic=true;
            string settingsPath=Output+"/ShadowmaskSettings.lighting";
            var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>(settingsPath);
            if(settings==null)
            {
                settings=new LightingSettings {name="Chapter One crisp baked shadowmasks"};
                AssetDatabase.CreateAsset(settings,settingsPath);
            }
            settings.bakedGI=true; settings.realtimeGI=false;
            settings.mixedBakeMode=MixedLightingMode.Shadowmask;
            settings.lightmapper=LightingSettings.Lightmapper.ProgressiveGPU;
            settings.lightmapResolution=10; settings.lightmapMaxSize=2048; settings.lightmapPadding=4;
            settings.directionalityMode=LightmapsMode.NonDirectional;
            settings.directSampleCount=16; settings.indirectSampleCount=8; settings.environmentSampleCount=16;
            settings.maxBounces=1; settings.ao=false; settings.indirectScale=0;
            settings.filteringMode=LightingSettings.FilterMode.Advanced;
            settings.denoiserTypeDirect=LightingSettings.DenoiserType.None;
            settings.filterTypeDirect=LightingSettings.FilterType.None;
            EditorUtility.SetDirty(settings); Lightmapping.lightingSettings=settings;
            SceneManager.SetActiveScene(sub);
            Lightmapping.lightingSettings=settings;
            RenderSettings.skybox=lighting.skybox;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=lighting.sky*lighting.ambientMultiplier;
            RenderSettings.ambientEquatorColor=lighting.equator*lighting.ambientMultiplier;
            RenderSettings.ambientGroundColor=lighting.ground*lighting.ambientMultiplier;
            SceneManager.SetActiveScene(scene);
            ChapterOneShadowTerrain.Split(scene,number);
            foreach(var item in new[]{scene,sub}) ConfigureGeometry(item);
            AddProbes(scene,sub);
            EditorSceneManager.MarkAllScenesDirty(); EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
        }

        private static bool Fixed(Component component)
        {
            if(component is Transform || component is MeshFilter || component is MeshRenderer || component is Collider || component is LODGroup) return true;
            string name=component.GetType().Name;
            return name=="ArenaAuthoring" || name=="NavigationArenaAuthoring" || name=="SolidObstacleAuthoring" ||
                name=="Volume" || name=="CelLevelLighting" || name=="BakedShadowLighting";
        }

        private static void ConfigureGeometry(Scene scene)
        {
            foreach(var root in scene.GetRootGameObjects())
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool fixedObject=renderer.GetComponentsInParent<Component>(true).Where(c=>c!=null).All(Fixed);
                if(!fixedObject)
                {
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,0);
                    continue;
                }
                foreach(var t in renderer.GetComponentsInParent<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.BatchingStatic|StaticEditorFlags.ContributeGI|
                        StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic|StaticEditorFlags.ReflectionProbeStatic);
                renderer.receiveGI=ReceiveGI.Lightmaps;
                var filter=renderer.GetComponent<MeshFilter>();
                if(filter!=null && filter.sharedMesh!=null) filter.sharedMesh=LightmapMesh(filter.sharedMesh);
                var serialized=new SerializedObject(renderer);
                float scale=renderer.name.StartsWith("Distant meadow tile")?.08f:
                    renderer.name.Contains("Grass") || renderer.name.Contains("Flower")?.18f:.7f;
                serialized.FindProperty("m_ScaleInLightmap").floatValue=scale;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void PreserveLightmapResolution()
        {
            // Entities Graphics copies these into arrays with full mip chains. Applying a
            // quality mip limit to just the source textures breaks that copy on low quality.
            foreach(var texture in LightmapSettings.lightmaps.SelectMany(l=>new[]{l.lightmapColor,l.lightmapDir,l.shadowMask}).Where(t=>t!=null).Distinct())
            {
                var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
                if(importer==null || importer.ignoreMipmapLimit) continue;
                importer.ignoreMipmapLimit=true;
                importer.SaveAndReimport();
            }
        }

        private static Mesh LightmapMesh(Mesh original)
        {
            if(AssetDatabase.GetAssetPath(original).StartsWith(Output+"/Meshes/")) return original;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original,out string guid,out long localId);
            string sourcePath=AssetDatabase.GetAssetPath(original);
            string revision=string.IsNullOrEmpty(sourcePath)?"":AssetDatabase.GetAssetDependencyHash(sourcePath).ToString();
            string path=Output+"/Meshes/"+Hash128.Compute(guid+"/"+localId+"/"+revision)+".asset";
            var copy=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(copy!=null) return copy;
            copy=Object.Instantiate(original); copy.name=original.name+" baked UV2";
            if(!Unwrapping.GenerateSecondaryUVSet(copy)) throw new InvalidOperationException("Could not unwrap "+original.name);
            AssetDatabase.CreateAsset(copy,path);
            return copy;
        }

        private static void AddProbes(Scene main,Scene sub)
        {
            if(main.GetRootGameObjects().Any(g=>g.name=="Baked environment probes")) return;
            var floor=sub.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>(true))
                .First(r=>r.name=="Continuous Convex Floor" || r.name=="Continuous Round Floor").bounds;
            var go=new GameObject("Baked environment probes"); SceneManager.MoveGameObjectToScene(go,main);
            var positions=new System.Collections.Generic.List<Vector3>();
            for(float x=floor.min.x-4;x<=floor.max.x+4;x+=6)
            for(float z=floor.min.z-4;z<=floor.max.z+4;z+=6)
            foreach(float y in new[]{.7f,3f,8f}) positions.Add(new Vector3(x,floor.max.y+y,z));
            go.AddComponent<LightProbeGroup>().probePositions=positions.ToArray();
        }
    }
}
