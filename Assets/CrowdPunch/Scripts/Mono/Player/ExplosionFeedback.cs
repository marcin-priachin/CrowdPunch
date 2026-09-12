using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    /// <summary>Reuses a bounded set of the existing grey-box explosion spheres.</summary>
    [RequireComponent(typeof(PlayerEcsBridge))]
    public sealed class ExplosionFeedback : MonoBehaviour
    {
        [SerializeField, Range(1, 64)] private int poolCapacity = 16;
        private PlayerEcsBridge bridge;
        private Material material;
        private Visual[] visuals;
        private Transform poolRoot;

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
