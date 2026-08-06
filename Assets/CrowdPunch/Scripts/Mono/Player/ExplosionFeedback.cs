using System.Collections.Generic;
using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    /// <summary>Draws short-lived grey-box explosion spheres received through the player bridge.</summary>
    [RequireComponent(typeof(PlayerEcsBridge))]
    public sealed class ExplosionFeedback : MonoBehaviour
    {
        private readonly List<Visual> visuals = new List<Visual>();
        private PlayerEcsBridge bridge;
        private Material material;

        private sealed class Visual
        {
            public GameObject Object;
            public float Duration;
            public float Remaining;
            public float TargetDiameter;
        }

        private void Awake()
        {
            bridge = GetComponent<PlayerEcsBridge>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            material = new Material(shader);
            material.color = new Color(1f, 0.35f, 0.05f, 0.28f);
        }

        private void OnEnable()
        {
            bridge.ExplosionReceived += Show;
        }

        private void OnDisable()
        {
            bridge.ExplosionReceived -= Show;
        }

        private void Update()
        {
            for (int index = visuals.Count - 1; index >= 0; index--)
            {
                Visual visual = visuals[index];
                visual.Remaining -= Time.deltaTime;
                if (visual.Remaining <= 0f)
                {
                    Destroy(visual.Object);
                    visuals.RemoveAt(index);
                    continue;
                }

                float progress = 1f - visual.Remaining / visual.Duration;
                visual.Object.transform.localScale = Vector3.one * (visual.TargetDiameter * progress);
            }
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        private void Show(Vector3 position, float radius, float duration, float sizeMultiplier)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "ExplosionFeedback";
            sphere.transform.position = position;
            Destroy(sphere.GetComponent<Collider>());
            sphere.GetComponent<Renderer>().sharedMaterial = material;
            sphere.transform.localScale = Vector3.zero;
            visuals.Add(new Visual
            {
                Object = sphere,
                Duration = duration,
                Remaining = duration,
                TargetDiameter = radius * 2f * sizeMultiplier
            });
        }
    }
}
