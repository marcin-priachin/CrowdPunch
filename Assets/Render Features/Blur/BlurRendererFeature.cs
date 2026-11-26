using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RenderFeatures.Blur
{
    [Serializable]
    public class BlurSettings
    {
        [Range(0, 0.4f)]
        public float HorizontalBlur;

        [Range(0, 0.4f)]
        public float VerticalBlur;
    }

    public class BlurRendererFeature : ScriptableRendererFeature
    {
        [SerializeField]
        private BlurSettings BlurSettings;

        [SerializeField]
        private Shader Shader;

        private Material m_Material;

        private BlurRenderPass m_BlurRenderPass;

        public override void Create()
        {
            CoreUtils.Destroy(m_Material);
            m_BlurRenderPass = null;
            if (Shader == null || BlurSettings == null)
                return;

            m_Material = CoreUtils.CreateEngineMaterial(Shader);
            m_BlurRenderPass = new BlurRenderPass(m_Material, BlurSettings)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_BlurRenderPass != null && renderingData.cameraData.cameraType == CameraType.Game)
                renderer.EnqueuePass(m_BlurRenderPass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(m_Material);
        }
    }
}