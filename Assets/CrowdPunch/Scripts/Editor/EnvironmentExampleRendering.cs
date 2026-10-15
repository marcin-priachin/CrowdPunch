using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CrowdPunch.Editor
{
    public static partial class EnvironmentExamplesBuilder
    {
        private static UniversalRenderPipelineAsset PresentationPipeline()
        {
            string path=Output+"Atmosphere/ExampleRendering.asset";
            var existing=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if(existing!=null) return existing;
            var source=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            var pipeline=Object.Instantiate(source);
            pipeline.name="Example rendering - contact shading";
            var data=new SerializedObject(pipeline);
            var list=data.FindProperty("m_RendererDataList");
            int index=data.FindProperty("m_DefaultRendererIndex").intValue;
            var renderer=Object.Instantiate((UniversalRendererData)list.GetArrayElementAtIndex(index).objectReferenceValue);
            renderer.name="Example renderer";
            AssetDatabase.CreateAsset(renderer,Output+"Atmosphere/ExampleRenderer.asset");
            var ao=ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ao.name="Example contact shading";
            var settings=new SerializedObject(ao);
            settings.FindProperty("m_Settings.Downsample").boolValue=true;
            settings.FindProperty("m_Settings.Intensity").floatValue=1.1f;
            settings.FindProperty("m_Settings.Radius").floatValue=.35f;
            settings.FindProperty("m_Settings.DirectLightingStrength").floatValue=.2f;
            settings.FindProperty("m_Settings.Falloff").floatValue=60;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.AddObjectToAsset(ao,renderer);
            renderer.rendererFeatures.Add(ao); renderer.SetDirty();
            list.GetArrayElementAtIndex(index).objectReferenceValue=renderer;
            data.ApplyModifiedPropertiesWithoutUndo();
            data.Update();
            data.FindProperty("m_SoftShadowsSupported").boolValue=true;
            data.ApplyModifiedPropertiesWithoutUndo();
            pipeline.shadowDistance=85;
            pipeline.shadowCascadeCount=2;
            pipeline.cascade2Split=.4f;
            pipeline.mainLightShadowmapResolution=2048;
            AssetDatabase.CreateAsset(pipeline,path);
            EditorUtility.SetDirty(renderer);
            return pipeline;
        }
    }
}
