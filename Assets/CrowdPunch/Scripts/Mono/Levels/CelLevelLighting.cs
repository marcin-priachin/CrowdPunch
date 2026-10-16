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

        private Color previousSky, previousEquator, previousGround;
        private AmbientMode previousMode;
        private Light previousSun;

        private void OnEnable()
        {
            previousSky = RenderSettings.ambientSkyColor;
            previousEquator = RenderSettings.ambientEquatorColor;
            previousGround = RenderSettings.ambientGroundColor;
            previousMode = RenderSettings.ambientMode;
            previousSun = RenderSettings.sun;
            Apply();
        }

        public void Apply()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky * ambientMultiplier;
            RenderSettings.ambientEquatorColor = equator * ambientMultiplier;
            RenderSettings.ambientGroundColor = ground * ambientMultiplier;
            if (sun != null) RenderSettings.sun = sun;
            DynamicGI.UpdateEnvironment();
        }

        private void OnDisable()
        {
            RenderSettings.ambientSkyColor = previousSky;
            RenderSettings.ambientEquatorColor = previousEquator;
            RenderSettings.ambientGroundColor = previousGround;
            RenderSettings.ambientMode = previousMode;
            RenderSettings.sun = previousSun;
            DynamicGI.UpdateEnvironment();
        }
    }
}
