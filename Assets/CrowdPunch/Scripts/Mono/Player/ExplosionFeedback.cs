using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    /// <summary>Bounded explosion accents with a short radius pulse; consumes bridge events only.</summary>
    [RequireComponent(typeof(PlayerEcsBridge))]
    public sealed class ExplosionFeedback : MonoBehaviour
    {
        [SerializeField, Range(1, 64)] private int poolCapacity = 16;
        private PlayerEcsBridge bridge;
        private Material material;
        private Visual[] visuals;
        private Transform poolRoot;
        private ParticleSystem[] bursts;
        private Mesh ring;
        private MaterialPropertyBlock properties;

        private sealed class Visual
        {
            public GameObject Object;
            public float Duration, Remaining, TargetDiameter;
            public Renderer Renderer;
        }

        private void Awake() => EnsurePool();

        private void EnsurePool()
        {
            if (visuals != null) return;
            if (poolRoot != null) Destroy(poolRoot.gameObject);
            if (material != null) Destroy(material);
            bridge = GetComponent<PlayerEcsBridge>();
            Shader shader = Resources.Load<Shader>("Shaders/ExplosionFeedback");
            if (shader == null)
            {
                Debug.LogError("Explosion feedback shader is missing from Resources/Shaders/ExplosionFeedback.", this);
                enabled = false;
                return;
            }
            material = new Material(shader);
            material.SetColor("_BaseColor", new Color(1f, .35f, .05f, .28f));
            poolRoot = new GameObject("Explosion feedback pool").transform;
            visuals = new Visual[Mathf.Clamp(poolCapacity, 1, 64)];
            var settings = Resources.Load<CombatFeedbackSettings>("CombatFeedbackSettings");
            var library = Resources.Load<CombatVfxLibrary>("CombatVfxLibrary");
            if (library != null && library.Shockwave != null)
            {
                Destroy(material);
                material = new Material(library.Shockwave);
            }
            properties = new MaterialPropertyBlock();
            ring = BuildRing();
            if (settings != null && settings.ExplosionParticles != null)
            {
                bursts = new ParticleSystem[visuals.Length];
                for (int i = 0; i < bursts.Length; i++)
                {
                    bursts[i] = Instantiate(settings.ExplosionParticles, poolRoot);
                    bursts[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                material.SetColor("_BaseColor", new Color(1f, .55f, .12f, .09f));
            }
            for (int i = 0; i < visuals.Length; i++)
            {
                var go = new GameObject("Explosion radius shockwave");
                go.transform.SetParent(poolRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = ring;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                go.SetActive(false);
                visuals[i] = new Visual { Object = go, Renderer = renderer };
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
            foreach (var visual in visuals)
            {
                if (visual.Remaining <= 0f) continue;
                visual.Remaining = Mathf.Max(0f, visual.Remaining - Time.unscaledDeltaTime);
                if (visual.Remaining <= 0f) visual.Object.SetActive(false);
                else
                {
                    float t = 1f-visual.Remaining/visual.Duration;
                    float expansion = 1f-Mathf.Pow(1f-Mathf.Clamp01(t/.65f),3);
                    visual.Object.transform.localScale = Vector3.one * (visual.TargetDiameter * expansion);
                    properties.SetColor("_BaseColor",new Color(1,.64f,.2f,.65f*(1-Mathf.SmoothStep(0,1,t))));
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
        }

        private void Show(Vector3 position, float radius, float duration, float sizeMultiplier)
        {
            if (visuals == null || FeedbackTimeController.IsSuspended) return;
            if (bursts != null)
            {
                foreach (var burst in bursts)
                {
                    if (burst.IsAlive(true)) continue;
                    burst.transform.position = position;
                    // The transparent pulse communicates radius; this compact accent must not hide targets.
                    burst.transform.localScale = Vector3.one * Mathf.Clamp(radius * .35f, .5f, 1.6f);
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
        private static Mesh BuildRing()
        {
            const int segments = 64;
            var vertices = new Vector3[(segments+1)*2];
            var colors = new Color[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var indices = new int[segments*6];
            for (int i=0;i<=segments;i++)
            {
                float a=i*Mathf.PI*2/segments;
                var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                vertices[i*2]=direction*.46f; vertices[i*2+1]=direction*.5f;
                colors[i*2]=colors[i*2+1]=Color.white;
                uv[i*2]=new Vector2(i/(float)segments,0); uv[i*2+1]=new Vector2(i/(float)segments,1);
                if(i==segments) continue;
                int t=i*6,n=i*2;
                indices[t]=n;indices[t+1]=n+1;indices[t+2]=n+3;
                indices[t+3]=n;indices[t+4]=n+3;indices[t+5]=n+2;
            }
            return new Mesh { name="Blast radius ring",vertices=vertices,colors=colors,uv=uv,triangles=indices };
        }
    }
}
