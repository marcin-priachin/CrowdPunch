using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CrowdPunch.Editor.ArtLab
{
    public static class ArtLabAssets
    {
        public const string Folder = "Assets/CrowdPunch/Data/ArtLab";
        public static readonly string[] Names = {"Current (Full cel)", "Soft toon hybrid", "Full cel shading", "Graphic comic", "Miniature / diorama"};
        public static ArtLabPreset Load(int index) => AssetDatabase.LoadAssetAtPath<ArtLabPreset>($"{Folder}/{index}_Preset.asset");

        [MenuItem("Crowd Punch/Art Lab/Create Missing Assets")]
        public static void CreateMissing()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create assets outside Play mode.");
            Directory.CreateDirectory(Folder+"/Shaders"); AssetDatabase.Refresh();
            GenerateGraph("Assets/CrowdPunch/Shaders/EnemySkinning.shadergraph","Enemy");
            GenerateGraph("Assets/Synty/SidekickCharacters/Resources/Shaders/Sidekick_ShaderGraph.shadergraph","Player");
            string litPath=Folder+"/Shaders/Environment.shader";
            if(!File.Exists(litPath))
            {
                string source=File.ReadAllText("Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader".Replace("Packages/com.unity.render-pipelines.universal", UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.render-pipelines.universal").resolvedPath));
                source=source.Replace("Shader \"Universal Render Pipeline/Lit\"","Shader \"CrowdPunch/ArtLab/Environment\"");
                source=source.Replace("\"LightMode\" = \"UniversalForward\"","\"LightMode\" = \"UniversalForwardOnly\"")
                    .Replace("\"LightMode\" = \"UniversalGBuffer\"","\"LightMode\" = \"ArtLabUnusedGBuffer\"");
                source=source.Replace("#include \"LitForwardPass.hlsl\"","#include \"Assets/CrowdPunch/Shaders/ArtLabLitForward.hlsl\"");
                source=source.Replace("#include \"Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl\"","#include \"Assets/CrowdPunch/Shaders/ArtLabLitForward.hlsl\"");
                source=System.Text.RegularExpressions.Regex.Replace(source,"#include \\\"(Lit[^\\\"]+\\.hlsl)\\\"", "#include \"Packages/com.unity.render-pipelines.universal/Shaders/$1\"");
                File.WriteAllText(litPath,source);
            }
            AssetDatabase.Refresh();
            string[] descriptions={"The authored chapter lighting, materials and post-processing.","Soft environments, three-tone characters and restrained rim light.","Distinct light bands on characters and scenery with neutral grading.","Hard bands, interior silhouette ink and subtle shadow halftone.","Smooth toy-like highlights, warm lighting and gentle bloom. No depth blur."};
            for(int i=0;i<5;i++)
            {
                if(Load(i)!=null) continue;
                var preset=ScriptableObject.CreateInstance<ArtLabPreset>();
                preset.title=Names[i]; preset.description=descriptions[i]; preset.shading=(ArtLabStyle)i;
                preset.sunTint=i==4?new Color(1,.95f,.85f):Color.white;
                preset.sunIntensityMultiplier=i==3?1.08f:1;
                preset.ambientMultiplier=i==2?1.08f:i==4?1.1f:1;
                preset.fogDistanceMultiplier=i==3?1.2f:1;
                var profile=ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile,$"{Folder}/{i}_Post.asset");
                var tone=profile.Add<Tonemapping>(true);tone.mode.Override(i==2||i==3?TonemappingMode.Neutral:TonemappingMode.ACES);
                var grade=profile.Add<ColorAdjustments>(true);grade.contrast.Override(new[]{6f,8f,5f,20f,6f}[i]);grade.saturation.Override(new[]{0f,6f,4f,-6f,10f}[i]);grade.postExposure.Override(.25f);
                var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(new[]{.22f,.2f,.06f,.04f,.35f}[i]);bloom.threshold.Override(1.1f);bloom.scatter.Override(.65f);
                var vignette=profile.Add<Vignette>(true);vignette.intensity.Override(i==3?.2f:.12f);
                // White balance is deliberately inherited from the chapter's volume.
                foreach(var component in profile.components) AssetDatabase.AddObjectToAsset(component,profile);
                EditorUtility.SetDirty(profile);preset.postProcessing=profile;
                AssetDatabase.CreateAsset(preset,$"{Folder}/{i}_Preset.asset");
            }
            AssetDatabase.SaveAssets();
        }

        private static void GenerateGraph(string path,string name)
        {
            string output=$"{Folder}/Shaders/{name}.shader";
            if(File.Exists(output)) return;
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.ShaderGraph.ShaderGraphImporter")).First(t=>t!=null);
            var method=type.GetMethods(BindingFlags.NonPublic|BindingFlags.Static).Single(m=>m.Name=="GetShaderText" && m.GetParameters().Length==4);
            string source=(string)method.Invoke(null,new object[]{path,null,null,null});
            // The project is URP-only. Exclude the generated Built-in fallback and its legacy warnings.
            int second=source.IndexOf("\nSubShader",source.IndexOf("\nSubShader",StringComparison.Ordinal)+1,StringComparison.Ordinal);
            int editor=source.IndexOf("\nCustomEditor",StringComparison.Ordinal);
            if(second>=0 && editor>second) source=source.Remove(second,editor-second);
            source=System.Text.RegularExpressions.Regex.Replace(source,"^Shader \\\"[^\\\"]+\\\"","Shader \"CrowdPunch/ArtLab/"+name+"\"");
            source=source.Replace("\"LightMode\" = \"UniversalForward\"","\"LightMode\" = \"UniversalForwardOnly\"")
                .Replace("\"LightMode\" = \"UniversalGBuffer\"","\"LightMode\" = \"ArtLabUnusedGBuffer\"")
                .Replace("Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBRForwardPass.hlsl","Assets/CrowdPunch/Shaders/ArtLabGraphForward.hlsl");
            File.WriteAllText(output,source);
        }
    }
}
