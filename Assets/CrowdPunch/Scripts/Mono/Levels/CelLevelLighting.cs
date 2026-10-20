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
        [Tooltip("Keep gameplay clear and apply world-space haze only to opted-in scenery materials.")]
        public bool sceneryOnlyHaze;
        public Vector4 hazeArenaBounds;
        public const int MaxHazeEdges = 128;
        [Tooltip("Counterclockwise XZ floor edges, each stored as start XY and end ZW.")]
        public Vector4[] hazeArenaEdges = System.Array.Empty<Vector4>();
        public float hazeGroundHeight;

        private static readonly int SceneryHazeBounds = Shader.PropertyToID("_CrowdPunchSceneryHazeBounds");
        private static readonly int SceneryHazeColor = Shader.PropertyToID("_CrowdPunchSceneryHazeColor");
        private static readonly int SceneryHazeSettings = Shader.PropertyToID("_CrowdPunchSceneryHazeSettings");
        private static readonly int SceneryHazeEdges = Shader.PropertyToID("_CrowdPunchSceneryHazeEdges");
        private static readonly int SceneryHazeEdgeCount = Shader.PropertyToID("_CrowdPunchSceneryHazeEdgeCount");
        private readonly Vector4[] edgeUpload = new Vector4[MaxHazeEdges];
        private Vector4[] previousHazeEdges;
        private int previousHazeEdgeCount;

        private Color previousSky, previousEquator, previousGround;
        private AmbientMode previousMode;
        private Light previousSun;
        private Material previousSkybox;
        private bool previousFog;
        private FogMode previousFogMode;
        private Color previousFogColor;
        private float previousFogStart, previousFogEnd;
        private Vector4 previousHazeBounds, previousHazeColor, previousHazeSettings;

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
            previousHazeBounds = Shader.GetGlobalVector(SceneryHazeBounds);
            previousHazeColor = Shader.GetGlobalVector(SceneryHazeColor);
            previousHazeSettings = Shader.GetGlobalVector(SceneryHazeSettings);
            previousHazeEdges = Shader.GetGlobalVectorArray(SceneryHazeEdges);
            previousHazeEdgeCount = Shader.GetGlobalInt(SceneryHazeEdgeCount);
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
                RenderSettings.fog = !sceneryOnlyHaze;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = haze;
                RenderSettings.fogStartDistance = hazeStart;
                RenderSettings.fogEndDistance = hazeEnd;
            }
            // Upload a fixed capacity so Unity never truncates a later, larger arena.
            int edgeCount = Mathf.Min(hazeArenaEdges.Length, MaxHazeEdges);
            System.Array.Clear(edgeUpload, 0, edgeUpload.Length);
            System.Array.Copy(hazeArenaEdges, edgeUpload, edgeCount);
            Shader.SetGlobalVectorArray(SceneryHazeEdges, edgeUpload);
            Shader.SetGlobalInt(SceneryHazeEdgeCount, edgeCount);
            Shader.SetGlobalVector(SceneryHazeBounds, hazeArenaBounds);
            Shader.SetGlobalColor(SceneryHazeColor, QualitySettings.activeColorSpace == ColorSpace.Linear ? haze.linear : haze);
            Shader.SetGlobalVector(SceneryHazeSettings, overrideAtmosphere && sceneryOnlyHaze
                ? new Vector4(1, hazeGroundHeight, 4.5f, 24) : Vector4.zero);
            DynamicGI.UpdateEnvironment();
        }

        private void OnDisable()
        {
            RenderSettings.ambientSkyColor = previousSky;
            RenderSettings.ambientEquatorColor = previousEquator;
            RenderSettings.ambientGroundColor = previousGround;
            RenderSettings.ambientMode = previousMode;
            RenderSettings.sun = previousSun;
            Shader.SetGlobalVector(SceneryHazeBounds, previousHazeBounds);
            Shader.SetGlobalVector(SceneryHazeColor, previousHazeColor);
            Shader.SetGlobalVector(SceneryHazeSettings, previousHazeSettings);
            Shader.SetGlobalVectorArray(SceneryHazeEdges, previousHazeEdges != null && previousHazeEdges.Length > 0
                ? previousHazeEdges : new Vector4[MaxHazeEdges]);
            Shader.SetGlobalInt(SceneryHazeEdgeCount, previousHazeEdgeCount);
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
