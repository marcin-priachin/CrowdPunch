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

        private sealed class Visual
        {
            public GameObject Object;
            public float Duration, Remaining, TargetDiameter;
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
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "ExplosionFeedback";
                sphere.transform.SetParent(poolRoot, false);
                Destroy(sphere.GetComponent<Collider>());
                sphere.GetComponent<Renderer>().sharedMaterial = material;
                sphere.SetActive(false);
                visuals[i] = new Visual { Object = sphere };
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
                visual.Remaining = Mathf.Max(0f, visual.Remaining - Time.deltaTime);
                if (visual.Remaining <= 0f) visual.Object.SetActive(false);
                else visual.Object.transform.localScale = Vector3.one
                    * (visual.TargetDiameter * (1f - visual.Remaining / visual.Duration));
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
                visual.TargetDiameter = Mathf.Max(0f, radius * 2f * sizeMultiplier);
                visual.Object.transform.position = position;
                visual.Object.transform.localScale = Vector3.zero;
                visual.Object.SetActive(true);
                return;
            }
        }
    }
}
