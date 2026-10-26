using System.Collections.Generic;
using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    internal sealed class LaunchedTrailPool
    {
        private sealed class Slot
        {
            public TrailRenderer Trail;
            public ulong Id;
            public uint Sequence;
            public bool Active, Seen;
        }
        private readonly Slot[] slots;
        private readonly Dictionary<ulong, int> assigned;
        private readonly CombatFeedbackSettings settings;
        private readonly ParticleBurstPool dust;
        public LaunchedTrailPool(Transform parent, CombatFeedbackSettings settings, Material material)
        {
            this.settings = settings;
            var library = Resources.Load<CombatVfxLibrary>("CombatVfxLibrary");
            dust = new ParticleBurstPool(parent, library != null ? library.LaunchDust : null, 8);
            slots = new Slot[Mathf.Clamp(settings.MaximumTrails, 0, 256)];
            assigned = new Dictionary<ulong, int>(slots.Length);
            for (int i = 0; i < slots.Length; i++)
            {
                var go = new GameObject("Launched trail");
                go.transform.SetParent(parent, false);
                var trail = go.AddComponent<TrailRenderer>();
                trail.sharedMaterial = library != null && library.LaunchTrail != null ? library.LaunchTrail : material;
                trail.textureMode = LineTextureMode.Stretch;
                trail.numCornerVertices = 2;
                trail.minVertexDistance = .08f;
                trail.emitting = false;
                trail.autodestruct = false;
                trail.endWidth = 0f;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                slots[i] = new Slot { Trail = trail };
            }
        }
        public void Begin() { foreach (var slot in slots) slot.Seen = false; }
        public void Show(ulong id, uint sequence, Vector3 position, float speed, int depth)
        {
            if (speed < settings.TrailMinimumSpeed) return;
            if (!assigned.TryGetValue(id, out int index))
            {
                index = -1;
                for (int i = 0; i < slots.Length; i++) if (!slots[i].Active) { index = i; break; }
                if (index < 0) return;
                assigned.Add(id, index);
                var fresh = slots[index];
                fresh.Active = true; fresh.Id = id; fresh.Sequence = sequence;
                fresh.Trail.transform.position = position;
                fresh.Trail.Clear();
                dust.Show(position + Vector3.up * .06f, Vector3.up, .7f);
            }
            var slot = slots[index];
            if (slot.Sequence != sequence) { slot.Trail.Clear(); slot.Sequence = sequence; }
            slot.Seen = true;
            slot.Trail.transform.position = position;
            slot.Trail.time = settings.TrailDuration;
            float intensity = Mathf.Clamp01(settings.Intensity(speed) * settings.ChainMultiplier(depth));
            slot.Trail.startWidth = settings.TrailWidth * Mathf.Lerp(.45f, 1.3f, intensity);
            var color = settings.TrailColor; color.a *= Mathf.Lerp(.4f, 1f, intensity);
            slot.Trail.startColor = color; color.a = 0f; slot.Trail.endColor = color;
            slot.Trail.emitting = true;
        }
        public void End()
        {
            foreach (var slot in slots)
            {
                if (!slot.Active || slot.Seen) continue;
                assigned.Remove(slot.Id);
                slot.Active = false;
                slot.Trail.emitting = false;
                slot.Trail.Clear();
            }
        }
        public void Clear() { Begin(); End(); dust.Clear(); }
    }
}
