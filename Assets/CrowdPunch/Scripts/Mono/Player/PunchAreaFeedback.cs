using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    /// <summary>
    /// Displays the live punch footprint and its cooldown recovery progress.
    /// </summary>
    public sealed class PunchAreaFeedback : MonoBehaviour
    {
        [SerializeField] private Color readyColor = new Color(1f, 0.45f, 0.1f, 0.3f);
        [SerializeField] private Color cooldownColor = new Color(0.2f, 0.35f, 0.65f, 0.3f);
        [SerializeField, Min(0f)] private float verticalOffset = 0.08f;

        private GameObject areaObject;
        private GameObject readyAreaObject;
        private Mesh areaMesh;
        private Mesh readyAreaMesh;
        private Material cooldownMaterial;
        private Material readyMaterial;
        private readonly Vector3[] areaVertices = new Vector3[4];
        private readonly Vector3[] readyAreaVertices = new Vector3[4];

        private void Awake()
        {
            CreateAreaRenderer();
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

            if (readyAreaMesh != null)
            {
                Destroy(readyAreaMesh);
            }

            if (cooldownMaterial != null)
            {
                Destroy(cooldownMaterial);
            }

            if (readyMaterial != null)
            {
                Destroy(readyMaterial);
            }
        }

        public void Show(Vector3 origin, Vector3 direction, float radius, float range, float cooldownProgress)
        {
            if (areaObject == null)
            {
                CreateAreaRenderer();
            }

            Vector3 planarDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (planarDirection.sqrMagnitude <= 0.001f || radius <= 0f || range <= 0f)
            {
                Hide();
                return;
            }

            float halfWidth = Mathf.Max(0f, radius);
            float length = Mathf.Max(0f, range);
            SetRectangle(areaMesh, areaVertices, halfWidth, length);
            SetRectangle(readyAreaMesh, readyAreaVertices, halfWidth, length * Mathf.Clamp01(cooldownProgress));

            areaObject.transform.SetPositionAndRotation(
                origin + Vector3.up * verticalOffset,
                Quaternion.LookRotation(planarDirection, Vector3.up));
            areaObject.SetActive(true);
            readyAreaObject.SetActive(cooldownProgress > 0f);
        }

        public void Hide()
        {
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
            areaMesh.vertices = areaVertices;
            areaMesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            areaObject.AddComponent<MeshFilter>().sharedMesh = areaMesh;

            readyAreaObject = new GameObject("Punch Ready Progress");
            readyAreaObject.transform.SetParent(areaObject.transform, false);
            readyAreaObject.transform.localPosition = Vector3.up * 0.002f;
            readyAreaMesh = new Mesh
            {
                name = "Punch Ready Progress Mesh"
            };
            readyAreaMesh.vertices = readyAreaVertices;
            readyAreaMesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            readyAreaObject.AddComponent<MeshFilter>().sharedMesh = readyAreaMesh;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                cooldownMaterial = new Material(shader)
                {
                    name = "Punch Cooldown Area Material",
                    color = cooldownColor
                };
                readyMaterial = new Material(shader)
                {
                    name = "Punch Ready Area Material",
                    color = readyColor
                };
            }

            MeshRenderer areaRenderer = areaObject.AddComponent<MeshRenderer>();
            areaRenderer.sharedMaterial = cooldownMaterial;
            areaRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            areaRenderer.receiveShadows = false;

            MeshRenderer readyAreaRenderer = readyAreaObject.AddComponent<MeshRenderer>();
            readyAreaRenderer.sharedMaterial = readyMaterial;
            readyAreaRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            readyAreaRenderer.receiveShadows = false;
            areaObject.SetActive(false);
        }

        private static void SetRectangle(Mesh mesh, Vector3[] vertices, float halfWidth, float length)
        {
            vertices[0] = new Vector3(-halfWidth, 0f, 0f);
            vertices[1] = new Vector3(halfWidth, 0f, 0f);
            vertices[2] = new Vector3(-halfWidth, 0f, length);
            vertices[3] = new Vector3(halfWidth, 0f, length);
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }
    }
}
