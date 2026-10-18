using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CrowdPunch.Mono.Levels
{
    // Scene-scoped shadow policy. The shared pipeline and other chapters stay intact.
    public sealed class BakedShadowLighting : MonoBehaviour
    {
        public LightProbes bakedProbes;
        private UniversalRenderPipelineAsset ownedPipeline;
        private LightProbes ownedProbes,previousProbes;
        private RenderPipelineAsset previousPipeline;
        private ShadowmaskMode previousMaskMode;
        private float previousBakedOnly;
        private static readonly int BakedOnly=Shader.PropertyToID("_CrowdPunchBakedShadowsOnly");

        private void OnEnable()
        {
            if(!Application.isPlaying) return;
            previousPipeline=QualitySettings.renderPipeline;
            var source=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(source==null) return;
            ownedPipeline=Instantiate(source);
            ownedPipeline.name="Baked shadows - active level";
            ownedPipeline.hideFlags=HideFlags.DontSave;
            ownedPipeline.shadowDistance=0;
            previousMaskMode=QualitySettings.shadowmaskMode;
            previousBakedOnly=Shader.GetGlobalFloat(BakedOnly);
            QualitySettings.shadowmaskMode=ShadowmaskMode.Shadowmask;
            QualitySettings.renderPipeline=ownedPipeline;
            Shader.SetGlobalFloat(BakedOnly,1);
            previousProbes=LightmapSettings.lightProbes;
            if(bakedProbes!=null)
            {
                ownedProbes=Instantiate(bakedProbes);
                ownedProbes.hideFlags=HideFlags.DontSave;
                ApplyProbes();
            }
            LightProbes.needsRetetrahedralization+=RebuildProbes;
        }

        private void OnDisable()
        {
            if(ownedPipeline==null) return;
            LightProbes.needsRetetrahedralization-=RebuildProbes;
            if(QualitySettings.renderPipeline==ownedPipeline) QualitySettings.renderPipeline=previousPipeline;
            QualitySettings.shadowmaskMode=previousMaskMode;
            Shader.SetGlobalFloat(BakedOnly,previousBakedOnly);
            if(ownedProbes!=null)
            {
                if(LightmapSettings.lightProbes==ownedProbes) LightmapSettings.lightProbes=previousProbes;
                Destroy(ownedProbes); ownedProbes=null;
            }
            Destroy(ownedPipeline);
            ownedPipeline=null;
        }

        // ECS SubScene streaming can replace the global probe set after main-scene enable.
        // Reassert only the level-owned reference; there is no renderer/entity scan.
        private void LateUpdate() => ApplyProbes();

        private void ApplyProbes()
        {
            if(ownedProbes==null || LightmapSettings.lightProbes==ownedProbes) return;
            LightmapSettings.lightProbes=ownedProbes;
            RebuildProbes();
        }

        private static void RebuildProbes() => LightProbes.TetrahedralizeAsync();
    }
}
