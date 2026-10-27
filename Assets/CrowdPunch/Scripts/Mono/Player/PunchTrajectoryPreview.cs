using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Mono.Player
{
    /// <summary>
    /// Renders the short, presentation-only initial trajectory supplied by ECS.
    /// </summary>
    [RequireComponent(typeof(PlayerEcsBridge))]
    public sealed class PunchTrajectoryPreview : MonoBehaviour
    {
        [SerializeField] private float lineLength = 3f;
        [SerializeField, Min(0.01f)] private float lineWidth = 0.36f;
        [SerializeField] private float verticalOffset = 0.15f;

        public float LineLength => Mathf.Max(0f, lineLength);

        private PlayerEcsBridge ecsBridge;
        private Material lineMaterial;
        private Mesh arrowMesh;

        private void Awake()
        {
            ecsBridge = GetComponent<PlayerEcsBridge>();
            Shader shader = Resources.Load<Shader>("Shaders/CombatFeedback");
            if (shader != null)
            {
                lineMaterial = new Material(shader)
                {
                    name = "Punch Trajectory Preview Material"
                };
                arrowMesh = CreateArrowMesh();
            }
        }

        private void LateUpdate()
        {
            if (lineMaterial == null || arrowMesh == null)
            {
                return;
            }

            IReadOnlyList<PlayerEcsBridge.TrajectoryPreviewSegment> segments = ecsBridge.TrajectoryPreviewSegments;
            for (int i = 0; i < segments.Count; i++)
            {
                PlayerEcsBridge.TrajectoryPreviewSegment segment = segments[i];
                Vector3 direction = ToVector3(segment.End - segment.Start);
                float length = direction.magnitude;
                if (length < 0.001f)
                {
                    continue;
                }

                direction /= length;
                Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f
                    ? Vector3.forward : Vector3.up;
                // World-space matrices keep the arrow independent of the giant player's scale.
                Matrix4x4 matrix = Matrix4x4.TRS(
                    ToVector3(segment.Start) + Vector3.up * verticalOffset,
                    Quaternion.LookRotation(direction, up),
                    new Vector3(Mathf.Max(0.01f, lineWidth), 1f, length));
                Graphics.DrawMesh(arrowMesh, matrix, lineMaterial, gameObject.layer, null, 0, null,
                    ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
            }
        }

        private void OnDestroy()
        {
            if (lineMaterial != null)
            {
                Destroy(lineMaterial);
            }
            if (arrowMesh != null)
            {
                Destroy(arrowMesh);
            }
        }

        private static Mesh CreateArrowMesh()
        {
            Mesh mesh = new Mesh { name = "Punch Trajectory Arrow" };
            // A clipped tail, broad shoulders and a solid tip match the flat cartoon feedback.
            mesh.vertices = new[]
            {
                new Vector3(-0.32f, 0f, 0f),
                new Vector3(-0.5f, 0f, 0.025f),
                new Vector3(-0.5f, 0f, 0.76f),
                new Vector3(-1.65f, 0f, 0.76f),
                new Vector3(0f, 0f, 1f),
                new Vector3(1.65f, 0f, 0.76f),
                new Vector3(0.5f, 0f, 0.76f),
                new Vector3(0.5f, 0f, 0.025f),
                new Vector3(0.32f, 0f, 0f)
            };
            // Shaft and head share edges without overlapping transparent faces.
            mesh.triangles = new[] { 0, 1, 8, 1, 7, 8, 1, 2, 7, 2, 6, 7, 3, 4, 2, 2, 4, 6, 6, 4, 5 };
            Color[] colors = new Color[9];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = new Color(1f, 1f, 1f, 0.5f);
            mesh.colors = colors;
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
