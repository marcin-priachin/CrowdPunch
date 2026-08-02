using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    /// <summary>
    /// Displays the committed punch footprint for a short presentation-only interval.
    /// </summary>
    public sealed class PunchAreaFeedback : MonoBehaviour
    {
        [SerializeField] private Color areaColor = new Color(1f, 0.45f, 0.1f, 0.3f);
        [SerializeField, Min(0f)] private float verticalOffset = 0.08f;

        private GameObject areaObject;
        private Mesh areaMesh;
        private Material areaMaterial;
        private float visibleUntil;

        private void Awake()
        {
            CreateAreaRenderer();
        }

        private void Update()
        {
            if (areaObject != null && areaObject.activeSelf && Time.time >= visibleUntil)
            {
                areaObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            if (areaObject != null)
            {
                Destroy(areaObject);
            }

            if (areaMesh != null)
            {
                Destroy(areaMesh);
            }

            if (areaMaterial != null)
            {
                Destroy(areaMaterial);
            }
        }

        public void Show(Vector3 origin, Vector3 direction, float radius, float range, float duration)
        {
            if (areaObject == null)
            {
                CreateAreaRenderer();
            }

            Vector3 planarDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (planarDirection.sqrMagnitude <= 0.001f || radius <= 0f || range <= 0f || duration <= 0f)
            {
                Hide();
                return;
            }

            float halfWidth = Mathf.Max(0f, radius);
            float length = Mathf.Max(0f, range);
            areaMesh.vertices = new[]
            {
                new Vector3(-halfWidth, 0f, 0f),
                new Vector3(halfWidth, 0f, 0f),
                new Vector3(-halfWidth, 0f, length),
                new Vector3(halfWidth, 0f, length)
            };
            areaMesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            areaMesh.RecalculateBounds();

            areaObject.transform.SetPositionAndRotation(
                origin + Vector3.up * verticalOffset,
                Quaternion.LookRotation(planarDirection, Vector3.up));
            visibleUntil = Time.time + duration;
            areaObject.SetActive(true);
        }

        public void Hide()
        {
            visibleUntil = 0f;
            if (areaObject != null)
            {
                areaObject.SetActive(false);
            }
        }

        private void CreateAreaRenderer()
        {
            areaObject = new GameObject("Punch Area Feedback");

            areaMesh = new Mesh
            {
                name = "Punch Area Feedback Mesh"
            };
            areaObject.AddComponent<MeshFilter>().sharedMesh = areaMesh;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                areaMaterial = new Material(shader)
                {
                    name = "Punch Area Feedback Material",
                    color = areaColor
                };
            }

            MeshRenderer areaRenderer = areaObject.AddComponent<MeshRenderer>();
            areaRenderer.sharedMaterial = areaMaterial;
            areaRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            areaRenderer.receiveShadows = false;
            areaObject.SetActive(false);
        }
    }
}
