using System.Collections.Generic;
using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.UI;
using CrowdPunch.Systems.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Mono.Player
{
    [RequireComponent(typeof(PlayerEcsBridge))]
    public sealed class ProjectileVfxPresentation : MonoBehaviour
    {
        private sealed class Slot
        {
            public ParticleSystem Flight;
            public MeshRenderer Spike;
            public TrailRenderer Trail;
            public ProjectileVisualKind Kind;
            public ulong Key;
            public uint Sequence;
            public bool Active, Seen;
        }
        private Slot[] slots;
        private CombatVfxLibrary library;
        private readonly Dictionary<ulong, Slot> assigned = new Dictionary<ulong, Slot>();
        private ParticleBurstPool[] bursts;
        private Transform root;
        private Mesh spikeMesh;
        private Material spikeMaterial;
        private MaterialPropertyBlock spikeTint;
        private readonly Dictionary<Hash128, Texture2D> spikeGradients = new();
        private PlayerEcsBridge bridge;
        private uint restart;
        internal int ActiveFlightCount => assigned.Count;
        internal int FlightCapacity => slots?.Length ?? 0;

        private void OnEnable()
        {
            bridge = GetComponent<PlayerEcsBridge>();
            EnsurePool();
            bridge.ProjectileVisualsBegan += Begin;
            bridge.ProjectileVisualReceived += Show;
            bridge.ProjectileVisualsEnded += End;
            bridge.ProjectileBurstReceived += Burst;
            restart = GameRestartRegistry.Sequence;
        }
        private void EnsurePool()
        {
            if (slots != null) return;
            if (root != null) Destroy(root.gameObject);
            library = Resources.Load<CombatVfxLibrary>("CombatVfxLibrary");
            if (library == null) return;
            root = new GameObject("Arcane projectile VFX pool").transform;
            var prefabs = new[] { library.RangedFlight, library.BossFlight, library.SnakeFlight, library.RangedCharge };
            int[] counts = { Mathf.Clamp(library.RangedCapacity, 1, 128), Mathf.Clamp(library.BossCapacity, 1, 64),
                Mathf.Clamp(library.SnakeCapacity, 1, 64), Mathf.Clamp(library.RangedCapacity, 1, 128) };
            const int spikeCapacity = 96;
            slots = new Slot[counts[0] + counts[1] + counts[2] + counts[3] + spikeCapacity];
            int index = 0;
            for (int kind = 0; kind < counts.Length; kind++)
            for (int i = 0; i < counts[kind]; i++)
            {
                var slot = new Slot { Kind = (ProjectileVisualKind)kind };
                if (prefabs[kind] != null)
                {
                    slot.Flight = Instantiate(prefabs[kind], root);
                    slot.Flight.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                if (kind < 2)
                {
                    var go = new GameObject(kind == 0 ? "Arcane comet streak" : "Archmage orb streak");
                    go.transform.SetParent(root, false);
                    slot.Trail = go.AddComponent<TrailRenderer>();
                    slot.Trail.sharedMaterial = kind == 0 ? library.ArcaneTrail : library.BossTrail;
                    slot.Trail.time = kind == 0 ? .16f : .23f;
                    slot.Trail.minVertexDistance = .07f;
                    slot.Trail.textureMode = LineTextureMode.Stretch;
                    slot.Trail.widthCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
                    slot.Trail.shadowCastingMode = ShadowCastingMode.Off;
                    slot.Trail.receiveShadows = false;
                    slot.Trail.emitting = false;
                }
                slots[index++] = slot;
            }
            for (int i = 0; i < spikeCapacity; i++) slots[index++] = new Slot { Kind = ProjectileVisualKind.AlienSpike };
            bursts = new[] { new ParticleBurstPool(root, library.RangedImpact, library.BurstCapacity),
                new ParticleBurstPool(root, library.BossImpact, library.BurstCapacity),
                new ParticleBurstPool(root, library.SnakeSever, library.BurstCapacity) };
        }
        private MeshRenderer CreateSpikeRenderer()
        {
            spikeTint ??= new MaterialPropertyBlock();
            if (spikeMesh == null) spikeMesh = CreateSpikeMesh();
            if (spikeMaterial == null)
                spikeMaterial = new Material(Resources.Load<Shader>("Shaders/AlienSpikeProjectile")) { name = "Alien Spike Projectile" };
            var spike = new GameObject("Alien spike projectile");
            spike.transform.SetParent(root, false);
            spike.AddComponent<MeshFilter>().sharedMesh = spikeMesh;
            var renderer = spike.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.sharedMaterial = spikeMaterial;
            return renderer;
        }

        private static Mesh CreateSpikeMesh()
        {
            var mesh = new Mesh { name = "Alien Spike Tip" };
            mesh.vertices = new[] { new Vector3(0, 0, .9f), new Vector3(0, 0, -.65f),
                new Vector3(.45f, 0, -.25f), new Vector3(0, .45f, -.25f),
                new Vector3(-.45f, 0, -.25f), new Vector3(0, -.45f, -.25f) };
            mesh.triangles = new[] { 0, 3, 2, 0, 4, 3, 0, 5, 4, 0, 2, 5,
                1, 2, 3, 1, 3, 4, 1, 4, 5, 1, 5, 2 };
            const float shoulder = (0.9f + 0.25f) / (0.9f + 0.65f);
            mesh.uv = new[] { Vector2.zero, Vector2.right, new Vector2(shoulder, 0),
                new Vector2(shoulder, 0), new Vector2(shoulder, 0), new Vector2(shoulder, 0) };
            mesh.RecalculateNormals();
            return mesh;
        }

        private Texture2D SpikeGradient(in ProjectileVisualSample sample)
        {
            if (!sample.SpikeColorGradient.IsCreated) return Texture2D.whiteTexture;
            ref var ramp = ref sample.SpikeColorGradient.Value;
            if (spikeGradients.TryGetValue(ramp.Hash, out var texture)) return texture;

            texture = new Texture2D(ramp.Colors.Length, 1, TextureFormat.RGBA32, false)
            {
                name = "Alien Spike Color Gradient", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var pixels = texture.GetRawTextureData<Color32>();
            for (int i = 0; i < pixels.Length; i++)
            {
                uint color = ramp.Colors[i];
                pixels[i] = new Color32((byte)color, (byte)(color >> 8), (byte)(color >> 16), 255);
            }
            texture.Apply(false, true);
            spikeGradients.Add(ramp.Hash, texture);
            return texture;
        }

        private void Begin() { if (slots != null) foreach (var slot in slots) slot.Seen = false; }
        private void Show(ProjectileVisualSample sample)
        {
            if (slots == null || FeedbackTimeController.IsSuspended) return;
            bool fresh = !assigned.TryGetValue(sample.Key, out var slot);
            if (fresh)
            {
                foreach (var candidate in slots)
                    if (!candidate.Active && candidate.Kind == sample.Kind) { slot = candidate; break; }
                if (slot == null) return;
                slot.Active = true; slot.Key = sample.Key; slot.Sequence = sample.Sequence;
                assigned.Add(sample.Key, slot);
            }
            bool newLaunch = fresh || slot.Sequence != sample.Sequence;
            slot.Sequence = sample.Sequence; slot.Seen = true;
            float diameter = Mathf.Max(.15f, sample.Radius * 2);
            if (sample.Kind == ProjectileVisualKind.RangedCharge)
            {
                float progress = Mathf.SmoothStep(0, 1, sample.ChargeProgress);
                // Ease the growing orb and tighten its pulse as release approaches.
                diameter *= Mathf.Lerp(.25f, 1.15f, progress)
                    * (1 + .07f * progress * Mathf.Sin(sample.ChargeProgress * Mathf.PI * 12));
            }
            if (slot.Kind == ProjectileVisualKind.AlienSpike)
            {
                if (slot.Spike == null) slot.Spike = CreateSpikeRenderer();
                slot.Spike.enabled = true;
                slot.Spike.transform.SetPositionAndRotation(sample.Position, Quaternion.LookRotation(sample.Velocity));
                slot.Spike.transform.localScale = Vector3.one * diameter * 1.45f;
                if (newLaunch)
                {
                    var color = CombatOwnershipTint.Accent(sample.Redirected ? EnemyLaunchOwner.Player : EnemyLaunchOwner.Boss, library);
                    spikeTint.SetColor("_BaseColor", color);
                    spikeTint.SetFloat("_PlayerOwned", sample.Redirected ? 1f : 0f);
                    spikeTint.SetTexture("_Gradient", SpikeGradient(sample));
                    slot.Spike.SetPropertyBlock(spikeTint);
                }
            }
            if (slot.Flight != null)
            {
                slot.Flight.transform.SetPositionAndRotation(sample.Position, Quaternion.LookRotation(
                    sample.Velocity.sqrMagnitude > .001f ? sample.Velocity : Vector3.forward));
                slot.Flight.transform.localScale = Vector3.one * diameter;
                if (newLaunch)
                {
                    slot.Flight.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    slot.Flight.Play(true);
                }
            }
            if (slot.Trail != null)
            {
                slot.Trail.transform.position = sample.Position;
                if (newLaunch) slot.Trail.Clear();
                slot.Trail.widthMultiplier = diameter * .65f;
                Color color = sample.Kind == ProjectileVisualKind.Ranged ? new Color(.3f, .92f, 1, .85f)
                    : CombatOwnershipTint.Accent(sample.Redirected ? EnemyLaunchOwner.Player : EnemyLaunchOwner.Boss, library);
                if (sample.Kind == ProjectileVisualKind.Boss) color.a = sample.Redirected ? .95f : .9f;
                slot.Trail.startColor = color; color.a = 0; slot.Trail.endColor = color;
                slot.Trail.emitting = true;
            }
            if (fresh && sample.Kind == ProjectileVisualKind.Snake)
                Burst(sample.Kind, sample.Position, sample.Velocity, .7f);
            if (newLaunch && sample.Kind == ProjectileVisualKind.Boss && sample.Redirected)
                Burst(sample.Kind, sample.Position, sample.Velocity, .9f);
        }
        private void End()
        {
            if (slots == null) return;
            foreach (var slot in slots)
            {
                if (!slot.Active || slot.Seen) continue;
                assigned.Remove(slot.Key); slot.Active = false;
                if (slot.Spike != null) slot.Spike.enabled = false;
                if (slot.Flight != null) slot.Flight.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (slot.Trail != null) { slot.Trail.emitting = false; slot.Trail.Clear(); }
            }
        }
        private void Burst(ProjectileVisualKind kind, Vector3 position, Vector3 direction, float scale)
        {
            if (kind == ProjectileVisualKind.AlienSpike) kind = ProjectileVisualKind.Boss;
            if (isActiveAndEnabled && !FeedbackTimeController.IsSuspended && bursts != null && (int)kind < bursts.Length)
                bursts[(int)kind].Show(position, direction, scale);
        }
        private void Update()
        {
            if (restart == GameRestartRegistry.Sequence && !FeedbackTimeController.IsSuspended) return;
            restart = GameRestartRegistry.Sequence; Clear();
        }
        public void Clear()
        {
            Begin(); End();
            if (bursts != null) foreach (var pool in bursts) pool.Clear();
        }
        private void OnDisable()
        {
            if (bridge != null)
            {
                bridge.ProjectileVisualsBegan -= Begin; bridge.ProjectileVisualReceived -= Show;
                bridge.ProjectileVisualsEnded -= End; bridge.ProjectileBurstReceived -= Burst;
            }
            Clear();
        }
        private void OnDestroy()
        {
            foreach (var gradient in spikeGradients.Values)
            {
                if (Application.isPlaying) Destroy(gradient);
                else DestroyImmediate(gradient);
            }
            if (Application.isPlaying)
            {
                if (root != null) Destroy(root.gameObject);
                if (spikeMesh != null) Destroy(spikeMesh);
                if (spikeMaterial != null) Destroy(spikeMaterial);
            }
            else
            {
                if (root != null) DestroyImmediate(root.gameObject);
                if (spikeMesh != null) DestroyImmediate(spikeMesh);
                if (spikeMaterial != null) DestroyImmediate(spikeMaterial);
            }
        }
    }
}
