using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    // Finite, prewarmed accents. A full pool drops cosmetic work instead of allocating.
    internal sealed class ParticleBurstPool
    {
        private readonly ParticleSystem[] effects;
        private int frame = -1, emitted;
        public ParticleBurstPool(Transform parent, ParticleSystem prefab, int capacity)
        {
            effects = new ParticleSystem[prefab == null ? 0 : Mathf.Clamp(capacity, 1, 64)];
            for (int i = 0; i < effects.Length; i++)
            {
                effects[i] = Object.Instantiate(prefab, parent);
                effects[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
        public void Show(Vector3 position, Vector3 direction, float scale = 1)
        {
            if (FeedbackTimeController.IsSuspended) return;
            if (frame != Time.frameCount) { frame = Time.frameCount; emitted = 0; }
            if (emitted >= 8) return;
            foreach (var effect in effects)
            {
                if (effect.IsAlive(true)) continue;
                effect.transform.SetPositionAndRotation(position, Quaternion.LookRotation(
                    direction.sqrMagnitude > .0001f ? direction : Vector3.up));
                effect.transform.localScale = Vector3.one * Mathf.Clamp(scale, .1f, 3);
                effect.Play(true); emitted++;
                return;
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
