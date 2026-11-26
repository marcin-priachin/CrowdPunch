using CrowdPunch.Configuration;
using MoreMountains.Tools;
using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    /// <summary>Bounded explosion accents with a short radius pulse; consumes bridge events only.</summary>
    [RequireComponent(typeof(PlayerEcsBridge))]
    public sealed class ExplosionFeedback : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        [SerializeField, Range(1, 64)] private int poolCapacity = 16;
        private PlayerEcsBridge bridge;
        private CombatFeedbackSettings settings;
        private Material material;
        private Visual[] visuals;
        private Transform poolRoot;
        private ParticleSystem[] bursts;
        private Mesh ring, singleRing;
        private MaterialPropertyBlock properties;

        private sealed class Visual
        {
            public GameObject Object;
            public float Duration, Remaining, TargetDiameter;
            public Renderer Renderer;
            public MeshFilter Filter;
        }

        private void Awake() => EnsurePool();

        private void EnsurePool()
        {
            if (visuals != null) return;
            if (poolRoot != null) Destroy(poolRoot.gameObject);
            if (material != null) Destroy(material);
            bridge = GetComponent<PlayerEcsBridge>();
            var library = Resources.Load<CombatVfxLibrary>("CombatVfxLibrary");
            if (library != null && library.Shockwave != null) material = new Material(library.Shockwave);
            else
            {
                Shader shader = Resources.Load<Shader>("Shaders/CombatSprite");
                if (shader == null)
                {
                    Debug.LogError("Explosion feedback needs the Combat Sprite shader.", this);
                    enabled = false;
                    return;
                }
                material = new Material(shader);
            }
            poolRoot = new GameObject("Explosion feedback pool").transform;
            visuals = new Visual[Mathf.Clamp(poolCapacity, 1, 64)];
            settings = Resources.Load<CombatFeedbackSettings>("CombatFeedbackSettings");
            properties = new MaterialPropertyBlock();
            ring = BuildRing(includeEcho: true);
            singleRing = BuildRing(includeEcho: false);
            if (settings != null && settings.ExplosionParticles != null)
            {
                bursts = new ParticleSystem[visuals.Length];
                for (int i = 0; i < bursts.Length; i++)
                {
                    bursts[i] = Instantiate(settings.ExplosionParticles, poolRoot);
                    bursts[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            for (int i = 0; i < visuals.Length; i++)
            {
                var go = new GameObject("Explosion radius shockwave");
                go.transform.SetParent(poolRoot, false);
                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = ring;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                go.SetActive(false);
                visuals[i] = new Visual { Object = go, Renderer = renderer, Filter = filter };
            }
        }

        private void OnEnable()
        {
            EnsurePool();
            if (bridge != null && material != null) bridge.ExplosionReceived += Show;
        }

        private void OnDisable()
        {
            if (bridge != null) bridge.ExplosionReceived -= Show;
            Clear();
        }

        private void Update()
        {
            if (visuals == null) return;
            if (FeedbackTimeController.IsSuspended) { Clear(); return; }
            bool reducedFlashes = settings != null && settings.UseReducedFlashes;
            float gain = reducedFlashes ? .19f : 1f;
            var mesh = reducedFlashes || PlayerPreferences.Enabled(PlayerPreference.ReducedEffects) ? singleRing : ring;
            foreach (var visual in visuals)
            {
                if (visual.Remaining <= 0f) continue;
                visual.Remaining = Mathf.Max(0f, visual.Remaining - Time.unscaledDeltaTime);
                if (visual.Remaining <= 0f) visual.Object.SetActive(false);
                else
                {
                    float t = 1f - visual.Remaining / visual.Duration;
                    float expansion = MMTween.EaseOutCubic(Mathf.Clamp01(t / .58f));
                    visual.Object.transform.localScale = Vector3.one * (visual.TargetDiameter * expansion);
                    float opacity = (reducedFlashes ? .65f : .82f) * (1f - MMTween.EaseInOutCubic(t));
                    if (visual.Filter.sharedMesh != mesh) visual.Filter.sharedMesh = mesh;
                    properties.SetColor(BaseColor, new Color(gain, .64f * gain, .2f * gain, opacity));
                    visual.Renderer.SetPropertyBlock(properties);
                }
            }
        }

        public void Clear()
        {
            if (bursts != null) foreach (var burst in bursts)
                if (burst != null) burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (visuals == null) return;
            foreach (var visual in visuals)
            {
                visual.Remaining = 0f;
                if (visual.Object != null) visual.Object.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (poolRoot != null) Destroy(poolRoot.gameObject);
            if (material != null) Destroy(material);
            if (ring != null) Destroy(ring);
            if (singleRing != null) Destroy(singleRing);
        }

        private void Show(Vector3 position, float radius, float duration, float sizeMultiplier)
        {
            if (visuals == null || radius <= 0f || FeedbackTimeController.IsSuspended) return;
            if (bursts != null && (settings == null || !settings.UseReducedFlashes))
            {
                foreach (var burst in bursts)
                {
                    if (burst.IsAlive(true)) continue;
                    burst.transform.position = position;
                    // The transparent pulse communicates radius; this compact accent must not hide targets.
                    float scale = PlayerPreferences.Enabled(PlayerPreference.ReducedEffects) ? .35f : .45f;
                    burst.transform.localScale = Vector3.one * Mathf.Clamp(radius * scale, .5f, 1.85f);
                    burst.Play(true);
                    break;
                }
            }
            foreach (var visual in visuals)
            {
                if (visual.Remaining > 0f || visual.Object == null) continue;
                visual.Duration = Mathf.Max(.01f, duration);
                visual.Remaining = visual.Duration;
                // Radius is authoritative; cosmetic size multipliers must not suggest extra blast reach.
                visual.TargetDiameter = Mathf.Max(0f, radius * 2f);
                visual.Object.transform.position = position + Vector3.up * .065f;
                visual.Object.transform.localScale = Vector3.zero;
                visual.Object.SetActive(true);
                return;
            }
        }
        internal static Mesh BuildRing(bool includeEcho)
        {
            const int segments = 64;
            // The outer edge remains at half the mesh diameter so the pulse marks the true blast radius.
            float[] radii = { .448f, .466f, .485f, .494f, .5f };
            Color[] profile = { new Color(1.2f, .50f, .19f, 0f), new Color(1.4f, .56f, .26f, .38f),
                new Color(2f, .93f, .30f, .7f), new Color(5.2f, 3.25f, 1.30f, 1f), new Color(4.3f, 2.45f, .93f, 0f) };
            int bands = radii.Length;
            int rings = includeEcho ? 2 : 1;
            var vertices = new Vector3[(segments + 1) * bands * rings];
            var colors = new Color[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var indices = new int[segments * rings * (bands - 1) * 6];
            int nextIndex = 0;
            for (int wave = 0; wave < rings; wave++)
            {
                bool echo = includeEcho && wave == 0;
                for (int segment = 0; segment <= segments; segment++)
                {
                    float angle = segment * (2f * Mathf.PI / segments);
                    var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    for (int band = 0; band < bands; band++)
                    {
                        int vertex = (wave * (segments + 1) + segment) * bands + band;
                        vertices[vertex] = direction * radii[band] * (echo ? .78f : 1f);
                        colors[vertex] = echo ? new Color(1.35f, .7f, .28f, profile[band].a * .55f) : profile[band];
                        uv[vertex] = new Vector2(segment / (float)segments, Mathf.InverseLerp(radii[0], radii[bands - 1], radii[band]));
                        if (segment == 0 || band == 0) continue;
                        int previous = vertex - bands;
                        indices[nextIndex++] = previous - 1;
                        indices[nextIndex++] = vertex - 1;
                        indices[nextIndex++] = previous;
                        indices[nextIndex++] = previous;
                        indices[nextIndex++] = vertex - 1;
                        indices[nextIndex++] = vertex;
                    }
                }
            }
            return new Mesh { name = "Blast radius ring", vertices = vertices, colors = colors, uv = uv, triangles = indices };
        }
    }
}
