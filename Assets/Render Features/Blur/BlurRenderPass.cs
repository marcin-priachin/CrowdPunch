using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace RenderFeatures.Blur
{
    public class BlurRenderPass : ScriptableRenderPass
    {
        private static readonly int HorizontalBlurId = Shader.PropertyToID("_HorizontalBlur");
        private static readonly int VerticalBlurId = Shader.PropertyToID("_VerticalBlur");

        private readonly BlurSettings m_DefaultSettings;
        private readonly Material m_Material;

        public BlurRenderPass(Material material, BlurSettings defaultSettings)
        {
            m_DefaultSettings = defaultSettings;
            m_Material = material;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer)
                return;

            var source = resources.activeColorTexture;
            var descriptor = renderGraph.GetTextureDesc(source);
            descriptor.clearBuffer = false;
            descriptor.name = "_BlurVertical";
            var vertical = renderGraph.CreateTexture(descriptor);
            descriptor.name = "_BlurHorizontal";
            var horizontal = renderGraph.CreateTexture(descriptor);

            m_Material.SetFloat(HorizontalBlurId, m_DefaultSettings.HorizontalBlur);
            m_Material.SetFloat(VerticalBlurId, m_DefaultSettings.VerticalBlur);

            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, vertical, m_Material, 0),
                passName: "Blur Vertical");
            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(vertical, horizontal, m_Material, 1),
                passName: "Blur Horizontal");
            resources.cameraColor = horizontal;
        }
    }
}
