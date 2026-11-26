using System.Collections.Generic;
using CrowdPunch.Configuration;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CrowdPunch.Mono.Camera
{
    // Owns the campaign's runtime pipeline and applies preferences to its gameplay camera.
    internal sealed class GameplayGraphics : MonoBehaviour
    {
        private static GameplayGraphics active;
        private readonly HashSet<UniversalRenderPipelineAsset> levelPipelines = new HashSet<UniversalRenderPipelineAsset>();
        private UniversalRenderPipelineAsset pipeline;
        private RenderPipelineAsset previousPipeline;
        private UniversalAdditionalCameraData cameraData;
        private AntialiasingMode previousAntiAliasing;

        private void Awake()
        {
            if (active != null || GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset source) return;

            active = this;
            previousPipeline = QualitySettings.renderPipeline;
            pipeline = Instantiate(source);
            pipeline.name = "Crowd Punch - graphics settings";
            pipeline.hideFlags = HideFlags.DontSave;
            ApplyScale(pipeline);
            QualitySettings.renderPipeline = pipeline;

            var camera = UnityEngine.Camera.main;
            if (camera != null && camera.TryGetComponent(out cameraData))
                previousAntiAliasing = cameraData.antialiasing;
            ApplyCamera();
        }

        internal static void Refresh()
        {
            if (active == null) return;
            ApplyScale(active.pipeline);
            foreach (var levelPipeline in active.levelPipelines) ApplyScale(levelPipeline);
            active.ApplyCamera();
        }

        internal static void RegisterLevelPipeline(UniversalRenderPipelineAsset levelPipeline)
        {
            if (active == null) return;
            active.levelPipelines.Add(levelPipeline);
            ApplyScale(levelPipeline);
        }

        internal static void UnregisterLevelPipeline(UniversalRenderPipelineAsset levelPipeline)
        {
            if (active != null) active.levelPipelines.Remove(levelPipeline);
        }

        private static void ApplyScale(UniversalRenderPipelineAsset target) =>
            target.renderScale = Mathf.Clamp(PlayerPreferences.Gain(PlayerPreference.RenderScale), .7f, 1f);

        private void ApplyCamera()
        {
            if (cameraData == null) return;
            cameraData.antialiasing = PlayerPreferences.Get(PlayerPreference.AntiAliasing) switch
            {
                0 => AntialiasingMode.None,
                1 => AntialiasingMode.FastApproximateAntialiasing,
                _ => AntialiasingMode.SubpixelMorphologicalAntiAliasing
            };
        }

        private void OnDestroy()
        {
            if (active != this) return;
            active = null;
            if (cameraData != null)
                cameraData.antialiasing = previousAntiAliasing;
            var selected = QualitySettings.renderPipeline;
            if (selected == pipeline || selected is UniversalRenderPipelineAsset level && levelPipelines.Contains(level))
                QualitySettings.renderPipeline = previousPipeline;
            Destroy(pipeline);
        }
    }
}
