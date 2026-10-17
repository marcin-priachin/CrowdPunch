using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CrowdPunch.Editor
{
    // VISION-006: crisp shadows and fixed geometry detail in saved content and builds.
    public static class CelShadowDetailAuthoring
    {
        private static string[] work;
        private static SceneSetup[] previousSetup;
        public static int Completed { get; private set; }
        public static int Total => work?.Length ?? 0;
        public static bool Running { get; private set; }
        public static string Error { get; private set; }
        public static int ChangedLights { get; private set; }
        public static int FixedLodGroups { get; private set; }
        public static int FixedRenderers { get; private set; }

        [MenuItem("Crowd Punch/Art Direction/Apply Hard Shadows and Full Detail")]
        public static void Begin()
        {
            if(Running || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Finish the current operation before authoring.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            previousSetup=EditorSceneManager.GetSceneManagerSetup();
            ConfigureSettings();
            work=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/CrowdPunch"}).Select(AssetDatabase.GUIDToAssetPath)
                .Concat(AssetDatabase.FindAssets("t:Scene",new[]{"Assets/CrowdPunch/Scenes"}).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p=>p!="Assets/CrowdPunch/Scenes/New Scene.unity")).Distinct().ToArray();
            Completed=ChangedLights=FixedLodGroups=FixedRenderers=0; Error=null; Running=true;
            EditorApplication.update+=Step;
        }

        public static void ConfigureSettings()
        {
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(CelPresentationAuthoring.PipelinePath);
            if(pipeline==null) throw new InvalidOperationException("The production Full Cel pipeline is missing.");
            var serialized=new SerializedObject(pipeline);
            serialized.FindProperty("m_SoftShadowsSupported").boolValue=false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            pipeline.mainLightShadowmapResolution=8192;
            // One stable shadow map avoids cascade resolution changes during camera movement.
            pipeline.shadowCascadeCount=1;
            pipeline.shadowDistance=110;
            EditorUtility.SetDirty(pipeline);
            int selected=QualitySettings.GetQualityLevel();
            for(int i=0;i<QualitySettings.names.Length;i++)
            {
                QualitySettings.SetQualityLevel(i,false);
                QualitySettings.renderPipeline=pipeline;
                // Unity clamps this threshold to .01; exact LOD0 overrides are authored below.
                QualitySettings.meshLodThreshold=.01f;
                QualitySettings.maximumLODLevel=0;
                QualitySettings.lodBias=1;
                QualitySettings.enableLODCrossFade=false;
                QualitySettings.shadows=UnityEngine.ShadowQuality.HardOnly;
            }
            QualitySettings.SetQualityLevel(selected,false);
            var quality=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels=quality.FindProperty("m_QualitySettings");
            for(int i=0;i<levels.arraySize;i++)
                levels.GetArrayElementAtIndex(i).FindPropertyRelative("enableLODCrossFade").boolValue=false;
            quality.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static void Step()
        {
            try
            {
                string path=work[Completed];
                if(path.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))
                {
                    var root=PrefabUtility.LoadPrefabContents(path);
                    try { if(ConfigureHierarchy(new[]{root})) PrefabUtility.SaveAsPrefabAsset(root,path); }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                else
                {
                    var scene=EditorSceneManager.OpenScene(path);
                    if(ConfigureHierarchy(scene.GetRootGameObjects()))
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }
                Completed++;
                if(Completed==Total) Finish();
            }
            catch(Exception exception) { Error=exception.ToString(); Debug.LogException(exception); Finish(); }
        }

        internal static bool ConfigureHierarchy(GameObject[] roots)
        {
            bool changed=false;
            foreach(var renderer in roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)))
            {
                if(renderer is MeshRenderer mesh && mesh.forceMeshLod!=0) mesh.forceMeshLod=0;
                else if(renderer is SkinnedMeshRenderer skin && skin.forceMeshLod!=0) skin.forceMeshLod=0;
                else continue;
                EditorUtility.SetDirty(renderer); PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                FixedRenderers++; changed=true;
            }
            foreach(var light in roots.SelectMany(r=>r.GetComponentsInChildren<Light>(true)))
            {
                if(light.shadows==LightShadows.None || light.shadows==LightShadows.Hard) continue;
                light.shadows=LightShadows.Hard;
                EditorUtility.SetDirty(light); PrefabUtility.RecordPrefabInstancePropertyModifications(light);
                ChangedLights++; changed=true;
            }
            foreach(var group in roots.SelectMany(r=>r.GetComponentsInChildren<LODGroup>(true)))
            {
                var lods=group.GetLODs();
                if(lods.Length==0) continue;
                var highest=lods[0].renderers.Where(r=>r!=null).ToArray();
                // Persist only LOD0. A zero transition height also bakes an infinite ECS range.
                foreach(var renderer in lods.SelectMany(l=>l.renderers).Where(r=>r!=null).Distinct())
                {
                    renderer.enabled=highest.Contains(renderer);
                    EditorUtility.SetDirty(renderer); PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                group.SetLODs(new[]{new LOD(0,highest)});
                group.fadeMode=LODFadeMode.None; group.enabled=false;
                EditorUtility.SetDirty(group); PrefabUtility.RecordPrefabInstancePropertyModifications(group);
                FixedLodGroups++; changed=true;
            }
            return changed;
        }

        private static void Finish()
        {
            EditorApplication.update-=Step; Running=false;
            AssetDatabase.SaveAssets(); EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }
}
