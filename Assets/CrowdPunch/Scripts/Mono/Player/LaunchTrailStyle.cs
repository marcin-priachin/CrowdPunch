using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    // One shared lookup per pool; Inspector edits upload once, never per enemy or frame.
    internal sealed class LaunchTrailStyle : System.IDisposable
    {
        private const int Samples = 128;
        private static readonly int ProfilesId = Shader.PropertyToID("_LineProfiles");
        private static readonly int ParametersId = Shader.PropertyToID("_LineParameters");
        private static readonly int OpacityId = Shader.PropertyToID("_LineOpacity");
        private static readonly int BrightnessId = Shader.PropertyToID("_LineBrightness");
        private readonly Color[] pixels = new Color[Samples * 2];
        private readonly Texture2D profiles = new Texture2D(Samples, 2, TextureFormat.RGBAHalf, false, true)
        {
            name = "Launch trail length profiles", wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave
        };
        private int version = -1;
        internal MaterialPropertyBlock Properties { get; } = new MaterialPropertyBlock();

        internal bool Refresh(CombatFeedbackSettings settings)
        {
            if (version == settings.TrailStyleVersion) return false;
            version = settings.TrailStyleVersion;
            for (int i = 0; i < Samples; i++)
            {
                float t = i / (float)(Samples - 1);
                pixels[i] = new Color(Evaluate(settings.TrailLineWidthAlongLength, t),
                    Mathf.Clamp01(Evaluate(settings.TrailOpacityAlongLength, t)),
                    Evaluate(settings.TrailLineBlurAlongLength, t), Evaluate(settings.TrailLineSpacingAlongLength, t));
                pixels[Samples + i] = (settings.TrailTintAlongLength?.Evaluate(t) ?? Color.white).linear;
            }
            profiles.SetPixels(pixels); profiles.Apply(false, false);
            Properties.SetTexture(ProfilesId, profiles);
            Properties.SetVector(ParametersId, new Vector4(Mathf.Clamp(settings.TrailLineCount, 1, 12),
                Mathf.Max(0, settings.TrailLineWidth), Mathf.Clamp01(settings.TrailLineBlur), Mathf.Clamp01(settings.TrailLineSpacing)));
            Properties.SetFloat(OpacityId, Mathf.Clamp01(settings.TrailLineOpacity));
            Properties.SetFloat(BrightnessId, Mathf.Max(0, settings.TrailLineBrightness));
            return true;
        }
        private static float Evaluate(AnimationCurve curve, float t)
            => Mathf.Clamp(curve != null && curve.length > 0 ? curve.Evaluate(t) : 1, 0, 4);
        internal void Apply(TrailRenderer trail, CombatFeedbackSettings settings, float launchAge)
        {
            float boost = settings.LaunchSpeedLineBoost
                ? 1f - Mathf.Clamp01(launchAge / Mathf.Max(.01f, settings.LaunchSpeedLineBoostDuration)) : 0f;
            boost = boost * boost * (3f - 2f * boost);
            Properties.SetFloat(OpacityId, Mathf.Clamp01(settings.TrailLineOpacity
                * Mathf.Lerp(1f, settings.LaunchSpeedLineOpacityMultiplier, boost)));
            Properties.SetFloat(BrightnessId, Mathf.Max(0, settings.TrailLineBrightness
                * Mathf.Lerp(1f, settings.LaunchSpeedLineBrightnessMultiplier, boost)));
            // SetPropertyBlock copies values; slots share one profile texture, never a material instance.
            trail.SetPropertyBlock(Properties);
        }
        public void Dispose()
        {
            if (profiles == null) return;
            if (Application.isPlaying) Object.Destroy(profiles); else Object.DestroyImmediate(profiles);
        }
    }
}
