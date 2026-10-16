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
using Object = UnityEngine.Object;

namespace CrowdPunch.Editor
{
    // One-time migration plus an explicit repair command for subsequently authored content.
    // Materials retain their GUIDs; imported/package assets are copied into game ownership.
    public static class CelPresentationAuthoring
    {
        public const string Folder = "Assets/CrowdPunch/Data/Presentation";
        public const string ProfilePath = Folder + "/FullCelPost.asset";
        public const string PipelinePath = Folder + "/CelRendering.asset";
        private static string[] work;
        private static SceneSetup[] previousSetup;
        public static int Completed { get; private set; }
        public static int Total => work?.Length ?? 0;
        public static bool Running { get; private set; }
        public static string Error { get; private set; }
        public static int ChangedMaterials { get; private set; }

        public static string ShaderKind(string name) => name switch
        {
            "Universal Render Pipeline/Lit" => "Environment",
            "CrowdPunch/EnemySkinning" => "Enemy",
            "Shader Graphs/Sidekick_ShaderGraph" => "Player",
            _ => null
        };

        [MenuItem("Crowd Punch/Art Direction/Apply Full Cel Default")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Running)
                throw new InvalidOperationException("Finish Play mode or the current migration first.");
            for (int i=0; i<SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            foreach (var window in Resources.FindObjectsOfTypeAll<ArtLab.ArtLabWindow>()) window.Close();
            previousSetup=EditorSceneManager.GetSceneManagerSetup();
            Completed=ChangedMaterials=0; Error=null;
            PrepareAssets();
            work=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/CrowdPunch"}).Select(AssetDatabase.GUIDToAssetPath)
                .Concat(AssetDatabase.FindAssets("t:Scene",new[]{"Assets/CrowdPunch/Scenes"}).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path=>path!= "Assets/CrowdPunch/Scenes/New Scene.unity")).Distinct().ToArray();
            Running=true; EditorApplication.update+=Step;
        }

        public static void PrepareAssets()
        {
            Directory.CreateDirectory(Folder+"/Materials"); AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath)==null)
                AssetDatabase.CopyAsset("Assets/CrowdPunch/Data/ArtLab/2_Post.asset",ProfilePath);
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath)==null)
            {
                AssetDatabase.CopyAsset("Assets/CrowdPunch/Data/EnvironmentExamples/Atmosphere/ExampleRendering.asset",PipelinePath);
                AssetDatabase.CopyAsset("Assets/CrowdPunch/Data/EnvironmentExamples/Atmosphere/ExampleRenderer.asset",Folder+"/CelRenderer.asset");
                var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
                var data=new SerializedObject(pipeline);
                data.FindProperty("m_RendererDataList").GetArrayElementAtIndex(data.FindProperty("m_DefaultRendererIndex").intValue).objectReferenceValue=
                    AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Folder+"/CelRenderer.asset");
                data.ApplyModifiedPropertiesWithoutUndo(); pipeline.supportsHDR=true; EditorUtility.SetDirty(pipeline);
            }
            var target=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            GraphicsSettings.defaultRenderPipeline=target;
            int quality=QualitySettings.GetQualityLevel();
            for (int i=0; i<QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i,false); QualitySettings.renderPipeline=target;
            }
            QualitySettings.SetQualityLevel(quality,false);
            foreach (string path in AssetDatabase.FindAssets("t:Material",new[]{"Assets/CrowdPunch"}).Select(AssetDatabase.GUIDToAssetPath).Distinct())
                if (path.EndsWith(".mat",StringComparison.OrdinalIgnoreCase)) ConvertMaterial(AssetDatabase.LoadAssetAtPath<Material>(path));
            AssetDatabase.SaveAssets();
        }

        private static void Step()
        {
            try
            {
                if (Completed>=work.Length) { Finish(); return; }
                string path=work[Completed];
                if (path.EndsWith(".prefab"))
                {
                    var root=PrefabUtility.LoadPrefabContents(path);
                    try { if (ConvertRenderers(new[]{root})) PrefabUtility.SaveAsPrefabAsset(root,path); }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                else ConvertScene(path);
                Completed++;
            }
            catch (Exception exception) { Error=exception.ToString(); Debug.LogException(exception); Finish(); }
        }

        private static void Finish()
        {
            EditorApplication.update-=Step; Running=false;
            AssetDatabase.SaveAssets(); EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }

        public static Material ConvertMaterial(Material material)
        {
            if (material==null) return null;
            string kind=ShaderKind(material.shader.name);
            if (kind==null) return material;
            string source=AssetDatabase.GetAssetPath(material);
            bool owned=source.StartsWith("Assets/CrowdPunch/",StringComparison.Ordinal) && source.EndsWith(".mat",StringComparison.OrdinalIgnoreCase);
            bool embedded=string.IsNullOrEmpty(source) || source.EndsWith(".unity",StringComparison.OrdinalIgnoreCase);
            if (!owned && !embedded)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material,out string guid,out long id);
                string path=$"{Folder}/Materials/{kind}_{guid}_{id}.mat";
                var copy=AssetDatabase.LoadAssetAtPath<Material>(path);
                if (copy!=null) return copy;
                material=new Material(material) {name=material.name+" Cel"};
                AssetDatabase.CreateAsset(material,path);
            }
            var shader=AssetDatabase.LoadAssetAtPath<Shader>($"Assets/CrowdPunch/Shaders/Cel/{kind}.shader");
            if (shader==null) throw new InvalidOperationException("Missing production cel shader: "+kind);
            material.shader=shader; EditorUtility.SetDirty(material); ChangedMaterials++;
            return material;
        }

        private static bool ConvertRenderers(GameObject[] roots)
        {
            bool changed=false;
            foreach (var renderer in roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)))
            {
                if (renderer is ParticleSystemRenderer) continue;
                var materials=renderer.sharedMaterials; bool local=false;
                for (int i=0; i<materials.Length; i++)
                {
                    var before=materials[i]; bool needsConversion=before!=null && ShaderKind(before.shader.name)!=null;
                    materials[i]=ConvertMaterial(before); local|=needsConversion || before!=materials[i];
                }
                if (!local) continue;
                renderer.sharedMaterials=materials; EditorUtility.SetDirty(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); changed=true;
            }
            return changed;
        }

        public static void ConvertScene(string path)
        {
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            if (ConfigureScene(scene)) {EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);}
        }

        public static bool ConfigureScene(Scene scene)
        {
            var roots=scene.GetRootGameObjects(); bool changed=ConvertRenderers(roots);
            var example=roots.SelectMany(r=>r.GetComponentsInChildren<ExampleEnvironmentLighting>(true)).FirstOrDefault();
            bool level=roots.Any(r=>r.GetComponentInChildren<GauntletLevel>(true)!=null);
            bool bootstrap=scene.path==CampaignBuildRegistration.BootstrapPath;
            if (level || bootstrap)
            {
                var presentation=roots.FirstOrDefault(r=>r.name=="Full Cel Presentation") ?? new GameObject("Full Cel Presentation");
                var volume=presentation.GetComponent<Volume>() ?? presentation.AddComponent<Volume>();
                volume.isGlobal=true; volume.weight=1; volume.priority=bootstrap?0:150;
                volume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
                if (example!=null)
                {
                    example.ambientMultiplier=1.08f;
                    example.presentationPipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
                    EditorUtility.SetDirty(example);
                }
                else if (level)
                {
                    var lighting=presentation.GetComponent<CelLevelLighting>();
                    if (lighting==null)
                    {
                        var sky=RenderSettings.ambientSkyColor; var equator=RenderSettings.ambientEquatorColor; var ground=RenderSettings.ambientGroundColor;
                        lighting=presentation.AddComponent<CelLevelLighting>();
                        lighting.sky=sky; lighting.equator=equator; lighting.ground=ground;
                    }
                    lighting.sun=roots.SelectMany(r=>r.GetComponentsInChildren<Light>(true)).FirstOrDefault(l=>l.type==LightType.Directional);
                    lighting.Apply(); EditorUtility.SetDirty(lighting);
                }
                changed=true;
            }
            foreach (var camera in roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)))
            {
                var data=camera.GetComponent<UniversalAdditionalCameraData>(); if(data==null) continue;
                camera.allowHDR=true; data.renderPostProcessing=true; data.volumeLayerMask|=1;
                data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                EditorUtility.SetDirty(camera); EditorUtility.SetDirty(data); changed=true;
            }
            return changed;
        }
    }
}
