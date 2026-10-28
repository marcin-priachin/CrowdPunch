using System.Collections.Generic;
using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    internal sealed class LaunchedTrailPool : System.IDisposable
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
        private readonly LaunchTrailStyle style = new LaunchTrailStyle();
        public LaunchedTrailPool(Transform parent, CombatFeedbackSettings settings, Material material)
        {
            this.settings = settings;
            style.Refresh(settings);
            var library = Resources.Load<CombatVfxLibrary>("CombatVfxLibrary");
            slots = new Slot[Mathf.Clamp(settings.MaximumTrails, 0, 256)];
            assigned = new Dictionary<ulong, int>(slots.Length);
            for (int i = 0; i < slots.Length; i++)
            {
                var go = new GameObject("Launched trail");
                go.transform.SetParent(parent, false);
                var trail = go.AddComponent<TrailRenderer>();
                trail.sharedMaterial = library != null && library.LaunchTrail != null ? library.LaunchTrail : material;
                trail.SetPropertyBlock(style.Properties);
                trail.textureMode = LineTextureMode.Stretch;
                // Rounded corner fans distort interior stripes; mitered joins keep them parallel.
                trail.numCornerVertices = 0;
                trail.minVertexDistance = .08f;
                trail.emitting = false;
                trail.autodestruct = false;
                trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 1);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                slots[i] = new Slot { Trail = trail };
            }
        }
        public void Begin()
        {
            bool changed = style.Refresh(settings);
            foreach (var slot in slots)
            {
                slot.Seen = false;
                if (changed) slot.Trail.SetPropertyBlock(style.Properties);
            }
        }
        public void Show(ulong id, uint sequence, Vector3 position, float speed, int depth, Vector3 modelSize = default, float launchAge = float.MaxValue)
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
            }
            var slot = slots[index];
            if (slot.Sequence != sequence) { slot.Trail.Clear(); slot.Sequence = sequence; }
            slot.Seen = true;
            style.Apply(slot.Trail, settings, launchAge);
            slot.Trail.transform.position = position;
            float width = Mathf.Max(.1f, modelSize.x);
            float length = Mathf.Max(width, Mathf.Max(modelSize.y, modelSize.z));
            slot.Trail.time = length * settings.TrailLengthInModels / Mathf.Max(.01f, speed);
            slot.Trail.widthMultiplier = width * settings.TrailWidthInModels;
            var color = settings.TrailColor;
            slot.Trail.startColor = slot.Trail.endColor = color;
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
        public void Clear() { Begin(); End(); }
        public void Dispose() => style.Dispose();
    }
}
