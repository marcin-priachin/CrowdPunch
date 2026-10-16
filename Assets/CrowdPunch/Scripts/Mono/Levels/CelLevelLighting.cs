using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Mono.Levels
{
    // Additive campaign scenes own their ambient palette for their loaded lifetime.
    // No material scans or ECS access are needed: cel materials are authored assets.
    public sealed class CelLevelLighting : MonoBehaviour
    {
        public Color sky = new(.212f, .227f, .259f);
        public Color equator = new(.114f, .125f, .133f);
        public Color ground = new(.047f, .043f, .035f);
        public float ambientMultiplier = 1.08f;
        public Light sun;
        public bool overrideAtmosphere;
        public Material skybox;
        public Color haze = new(.64f, .73f, .73f);
        public float hazeStart = 70;
        public float hazeEnd = 290;

        private Color previousSky, previousEquator, previousGround;
        private AmbientMode previousMode;
        private Light previousSun;
        private Material previousSkybox;
        private bool previousFog;
        private FogMode previousFogMode;
        private Color previousFogColor;
        private float previousFogStart, previousFogEnd;

        private void OnEnable()
        {
            previousSky = RenderSettings.ambientSkyColor;
            previousEquator = RenderSettings.ambientEquatorColor;
            previousGround = RenderSettings.ambientGroundColor;
            previousMode = RenderSettings.ambientMode;
            previousSun = RenderSettings.sun;
            previousSkybox = RenderSettings.skybox;
            previousFog = RenderSettings.fog;
            previousFogMode = RenderSettings.fogMode;
            previousFogColor = RenderSettings.fogColor;
            previousFogStart = RenderSettings.fogStartDistance;
            previousFogEnd = RenderSettings.fogEndDistance;
            Apply();
        }

        public void Apply()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky * ambientMultiplier;
            RenderSettings.ambientEquatorColor = equator * ambientMultiplier;
            RenderSettings.ambientGroundColor = ground * ambientMultiplier;
            if (sun != null) RenderSettings.sun = sun;
            if (overrideAtmosphere)
            {
                RenderSettings.skybox = skybox;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = haze;
                RenderSettings.fogStartDistance = hazeStart;
                RenderSettings.fogEndDistance = hazeEnd;
            }
            DynamicGI.UpdateEnvironment();
        }

        private void OnDisable()
        {
            RenderSettings.ambientSkyColor = previousSky;
            RenderSettings.ambientEquatorColor = previousEquator;
            RenderSettings.ambientGroundColor = previousGround;
            RenderSettings.ambientMode = previousMode;
            RenderSettings.sun = previousSun;
            if (overrideAtmosphere)
            {
                RenderSettings.skybox = previousSkybox;
                RenderSettings.fog = previousFog;
                RenderSettings.fogMode = previousFogMode;
                RenderSettings.fogColor = previousFogColor;
                RenderSettings.fogStartDistance = previousFogStart;
                RenderSettings.fogEndDistance = previousFogEnd;
            }
            DynamicGI.UpdateEnvironment();
        }
    }
}
