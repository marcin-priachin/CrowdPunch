using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CrowdPunch.Mono.Levels
{
    /// <summary>Scopes the example's atmosphere to its additive level lifetime.</summary>
    public sealed class ExampleEnvironmentLighting : MonoBehaviour
    {
        public Material sky;
        public Light sun;
        public UniversalRenderPipelineAsset presentationPipeline;
        public Color ambientSky = Color.gray;
        public Color ambientEquator = Color.gray;
        public Color ambientGround = Color.gray;
        public float ambientMultiplier = 1.08f;
        public Color haze = Color.gray;
        public float hazeStart = 45;
        public float hazeEnd = 230;

        private Material previousSky;
        private Light previousSun;
        private AmbientMode previousAmbientMode;
        private Color previousAmbientSky, previousEquator, previousGround, previousFogColor;
        private bool previousFog;
        private FogMode previousFogMode;
        private float previousFogStart, previousFogEnd;
        private UnityEngine.Camera view;
        private UniversalAdditionalCameraData cameraData;
        private bool previousPost, previousHdr;
        private LayerMask previousMask;
        private AntialiasingMode previousAntialiasing;
        private bool applied;
        private RenderPipelineAsset previousPipeline;

        private void OnEnable()
        {
            previousPipeline = QualitySettings.renderPipeline;
            if (presentationPipeline != null) QualitySettings.renderPipeline = presentationPipeline;
            previousSky = RenderSettings.skybox;
            previousSun = RenderSettings.sun;
            previousAmbientMode = RenderSettings.ambientMode;
            previousAmbientSky = RenderSettings.ambientSkyColor;
            previousEquator = RenderSettings.ambientEquatorColor;
            previousGround = RenderSettings.ambientGroundColor;
            previousFog = RenderSettings.fog;
            previousFogMode = RenderSettings.fogMode;
            previousFogColor = RenderSettings.fogColor;
            previousFogStart = RenderSettings.fogStartDistance;
            previousFogEnd = RenderSettings.fogEndDistance;
            ApplyAtmosphere();
            applied = true;
            ConfigureCamera();
        }

        public void ApplyAtmosphere()
        {
            // Bootstrap stays active when GauntletSequence loads levels additively.
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky * ambientMultiplier;
            RenderSettings.ambientEquatorColor = ambientEquator * ambientMultiplier;
            RenderSettings.ambientGroundColor = ambientGround * ambientMultiplier;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = haze;
            RenderSettings.fogStartDistance = hazeStart;
            RenderSettings.fogEndDistance = hazeEnd;
            DynamicGI.UpdateEnvironment();
        }

        private void LateUpdate()
        {
            if (view == null) ConfigureCamera();
        }

        private void ConfigureCamera()
        {
            var candidate = UnityEngine.Camera.main;
            if (candidate == null || !candidate.TryGetComponent(out UniversalAdditionalCameraData data)) return;
            view = candidate;
            cameraData = data;
            previousHdr = view.allowHDR;
            previousPost = data.renderPostProcessing;
            previousMask = data.volumeLayerMask;
            previousAntialiasing = data.antialiasing;
            view.allowHDR = true;
            data.renderPostProcessing = true;
            data.volumeLayerMask |= 1 << gameObject.layer;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        private void OnDisable()
        {
            if (!applied) return;
            RenderSettings.skybox = previousSky;
            RenderSettings.sun = previousSun;
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientSkyColor = previousAmbientSky;
            RenderSettings.ambientEquatorColor = previousEquator;
            RenderSettings.ambientGroundColor = previousGround;
            RenderSettings.fog = previousFog;
            RenderSettings.fogMode = previousFogMode;
            RenderSettings.fogColor = previousFogColor;
            RenderSettings.fogStartDistance = previousFogStart;
            RenderSettings.fogEndDistance = previousFogEnd;
            DynamicGI.UpdateEnvironment();
            if (view != null) view.allowHDR = previousHdr;
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = previousPost;
                cameraData.volumeLayerMask = previousMask;
                cameraData.antialiasing = previousAntialiasing;
            }
            applied = false;
            if (presentationPipeline != null && QualitySettings.renderPipeline == presentationPipeline)
                QualitySettings.renderPipeline = previousPipeline;
        }
    }
}
