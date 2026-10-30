using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    // Presentation IDs are process-local values, not retained ECS entity references.
    internal sealed class DasherTelegraphParticlePool
    {
        private readonly Entry[] entries;

        private struct Entry
        {
            public ulong Id;
            public ParticleSystem Effect;
            public ParticleSystem[] Layers;
            public float[] Rates;
            public bool Seen;
        }

        public DasherTelegraphParticlePool(Transform parent, CombatFeedbackSettings settings, Material fallbackMaterial)
        {
            int capacity = Mathf.Clamp(settings.ParticlePoolSizePerEffect, 1, 64);
            entries = new Entry[capacity];
            for (int index = 0; index < capacity; index++)
            {
                ParticleSystem effect = settings.DasherTelegraphParticles != null
                    ? Object.Instantiate(settings.DasherTelegraphParticles, parent)
                    : CreateFallback(parent, fallbackMaterial);
                effect.name = $"Dasher telegraph {index + 1:00}";
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                entries[index].Effect = effect;
                entries[index].Layers = effect.GetComponentsInChildren<ParticleSystem>(true);
                entries[index].Rates = new float[entries[index].Layers.Length];
                for (int layer = 0; layer < entries[index].Layers.Length; layer++)
                    entries[index].Rates[layer] = entries[index].Layers[layer].emission.rateOverTimeMultiplier;
            }
        }

        public void Begin()
        {
            for (int index = 0; index < entries.Length; index++) entries[index].Seen = false;
        }

        public void Show(ulong id, Vector3 position, Vector3 direction, float progress)
        {
            int slot = Find(id);
            if (slot < 0) slot = Find(0);
            if (slot < 0) return;
            ref Entry entry = ref entries[slot];
            entry.Id = id;
            entry.Seen = true;
            Vector3 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            // Physics roots sit one capsule half-height above the floor.
            entry.Effect.transform.SetPositionAndRotation(position + Vector3.down * 0.9f,
                Quaternion.LookRotation(forward, Vector3.up));
            entry.Effect.transform.localScale = Vector3.one;
            for (int layer = 0; layer < entry.Layers.Length; layer++)
            {
                var emission = entry.Layers[layer].emission;
                emission.rateOverTimeMultiplier = entry.Rates[layer] * Mathf.Lerp(.45f, 1f, Mathf.Clamp01(progress));
            }
            if (!entry.Effect.isPlaying) entry.Effect.Play(true);
        }

        public void End()
        {
            for (int index = 0; index < entries.Length; index++)
            {
                if (entries[index].Seen || entries[index].Id == 0) continue;
                entries[index].Effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                entries[index].Id = 0;
            }
        }

        public void Clear()
        {
            for (int index = 0; index < entries.Length; index++)
            {
                entries[index].Effect?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                entries[index].Id = 0;
                entries[index].Seen = false;
            }
        }

        private int Find(ulong id)
        {
            for (int index = 0; index < entries.Length; index++)
                if (entries[index].Id == id) return index;
            return -1;
        }

        private static ParticleSystem CreateFallback(Transform parent, Material material)
        {
            var gameObject = new GameObject("Dasher telegraph particles");
            gameObject.transform.SetParent(parent, false);
            ParticleSystem effect = gameObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = effect.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.28f;
            main.startSpeed = 0.8f;
            main.startSize = 0.18f;
            main.startColor = new Color(1f, 0.95f, 0.82f, 0.8f);
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = effect.emission;
            emission.rateOverTime = 12f;
            ParticleSystem.ShapeModule shape = effect.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = .4f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            gameObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return effect;
        }
    }
}
