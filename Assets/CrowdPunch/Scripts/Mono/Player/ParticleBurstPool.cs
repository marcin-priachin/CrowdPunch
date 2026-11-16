using UnityEngine;
using CrowdPunch.Systems.Presentation;

namespace CrowdPunch.Mono.Player
{
    // Finite, prewarmed accents. A full pool drops cosmetic work instead of allocating.
    internal sealed class ParticleBurstPool
    {
        private readonly ParticleSystem[] effects;
        private readonly ParticleSystem[][] tintedLayers;
        private readonly ParticleSystem.MinMaxGradient[] authoredColors;
        private int frame = -1, emitted;
        public ParticleBurstPool(Transform parent, ParticleSystem prefab, int capacity, bool tintByOwner = false)
        {
            effects = new ParticleSystem[prefab == null ? 0 : Mathf.Clamp(capacity, 1, 64)];
            if (effects.Length > 0 && tintByOwner)
            {
                tintedLayers = new ParticleSystem[effects.Length][];
                var sourceLayers = prefab.GetComponentsInChildren<ParticleSystem>(true);
                authoredColors = new ParticleSystem.MinMaxGradient[sourceLayers.Length];
                for (int i = 0; i < sourceLayers.Length; i++)
                    authoredColors[i] = sourceLayers[i].main.startColor;
            }
            for (int i = 0; i < effects.Length; i++)
            {
                effects[i] = Object.Instantiate(prefab, parent);
                effects[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (tintedLayers != null) tintedLayers[i] = effects[i].GetComponentsInChildren<ParticleSystem>(true);
            }
        }
        public void Show(Vector3 position, Vector3 direction, float scale = 1, Color? accent = null)
        {
            if (FeedbackTimeController.IsSuspended) return;
            if (frame != Time.frameCount) { frame = Time.frameCount; emitted = 0; }
            if (emitted >= 8) return;
            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect.IsAlive(true)) continue;
                if (tintedLayers != null) TintLayers(tintedLayers[i], accent);
                effect.transform.SetPositionAndRotation(position, Quaternion.LookRotation(
                    direction.sqrMagnitude > .0001f ? direction : Vector3.up));
                effect.transform.localScale = Vector3.one * Mathf.Clamp(scale, .1f, 3);
                effect.Play(true); emitted++;
                return;
            }
        }
        private void TintLayers(ParticleSystem[] layers, Color? accent)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                var color = authoredColors[i];
                if (accent.HasValue && color.mode == ParticleSystemGradientMode.Color)
                    color = CombatOwnershipTint.Blend(color.color, accent.Value, .85f);
                var main = layers[i].main;
                main.startColor = color;
            }
        }
        public void Clear()
        {
            foreach (var effect in effects) if (effect != null)
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            emitted = 0; frame = -1;
        }
    }
}
