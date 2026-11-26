using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace RenderFeatures
{
    public class DesaturationRenderPass : ScriptableRenderPass
    {
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int FilterTextureId = Shader.PropertyToID("_FilterTexture");

        private readonly DesaturationSettings m_Settings;
        private readonly Material m_FullscreenMaterial;
        private readonly Material m_OverrideMaterial;
        private readonly FilteringSettings m_FilteringSettings;
        private readonly List<ShaderTagId> m_ShaderTagIds = new()
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly")
        };

        private class FilterPassData
        {
            public RendererListHandle Renderers;
        }

        public DesaturationRenderPass(DesaturationSettings settings)
        {
            m_Settings = settings;
            m_FullscreenMaterial = CoreUtils.CreateEngineMaterial(settings.FullscreenShader);
            if (settings.OverrideShader != null)
                m_OverrideMaterial = CoreUtils.CreateEngineMaterial(settings.OverrideShader);

            var renderLayer = 1u << settings.RenderLayerMask;
            m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque, settings.LayerMask, renderLayer);
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer)
                return;

            var cameraData = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var lights = frameData.Get<UniversalLightData>();
            var depth = resources.activeDepthTexture;
            var source = resources.activeColorTexture;

            var maskDescriptor = renderGraph.GetTextureDesc(source);
            maskDescriptor.name = "_FilterTexture";
            var formatDescriptor = cameraData.cameraTargetDescriptor;
            formatDescriptor.colorFormat = m_Settings.RenderTextureFormat;
            maskDescriptor.colorFormat = formatDescriptor.graphicsFormat;
            maskDescriptor.clearBuffer = true;
            maskDescriptor.clearColor = Color.clear;
            var mask = renderGraph.CreateTexture(maskDescriptor);

            var drawingSettings = RenderingUtils.CreateDrawingSettings(m_ShaderTagIds, renderingData, cameraData,
                lights, cameraData.defaultOpaqueSortFlags);
            drawingSettings.overrideMaterial = m_OverrideMaterial;
            drawingSettings.overrideMaterialPassIndex = 0;
            var rendererList = new RendererListParams(renderingData.cullResults, drawingSettings, m_FilteringSettings);

            using (var builder = renderGraph.AddRasterRenderPass<FilterPassData>("Desaturation Mask", out var passData))
            {
                passData.Renderers = renderGraph.CreateRendererList(rendererList);
                builder.UseRendererList(passData.Renderers);
                builder.SetRenderAttachment(mask, 0);
                if (depth.IsValid())
                    builder.SetRenderAttachmentDepth(depth, AccessFlags.ReadWrite);
                builder.SetGlobalTextureAfterPass(mask, FilterTextureId);
                builder.SetRenderFunc(static (FilterPassData data, RasterGraphContext context) =>
                    context.cmd.DrawRendererList(data.Renderers));
            }

            var colorDescriptor = renderGraph.GetTextureDesc(source);
            colorDescriptor.name = "_DesaturatedColor";
            colorDescriptor.clearBuffer = false;
            var destination = renderGraph.CreateTexture(colorDescriptor);

            m_FullscreenMaterial.SetFloat(SaturationId, m_Settings.Saturation);
            var blit = new RenderGraphUtils.BlitMaterialParameters(source, destination, m_FullscreenMaterial, 0);
            using (var builder = renderGraph.AddBlitPass(blit, passName: "Desaturation", returnBuilder: true))
                builder.UseGlobalTexture(FilterTextureId);

            resources.cameraColor = destination;
        }

        public void Dispose()
        {
            CoreUtils.Destroy(m_FullscreenMaterial);
            CoreUtils.Destroy(m_OverrideMaterial);
        }
    }
}
