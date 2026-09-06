using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    /// <summary>
    /// Renders the short, presentation-only initial trajectory supplied by ECS.
    /// </summary>
    [RequireComponent(typeof(PlayerEcsBridge))]
    public sealed class PunchTrajectoryPreview : MonoBehaviour
    {
        [SerializeField] private Color lineColor = new Color(1f, 0.85f, 0.15f, 0.45f);
        [SerializeField] private float lineLength = 3f;
        [SerializeField] private float lineWidth = 0.12f;
        [SerializeField] private float verticalOffset = 0.15f;

        public float LineLength => Mathf.Max(0f, lineLength);

        private readonly List<LineRenderer> renderers = new List<LineRenderer>();
        private PlayerEcsBridge ecsBridge;
        private Material lineMaterial;

        private void Awake()
        {
            ecsBridge = GetComponent<PlayerEcsBridge>();
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                lineMaterial = new Material(shader)
                {
                    name = "Punch Trajectory Preview Material"
                };
            }
        }

        private void LateUpdate()
        {
            IReadOnlyList<PlayerEcsBridge.TrajectoryPreviewSegment> segments = ecsBridge.TrajectoryPreviewSegments;
            EnsureRendererCount(segments.Count);

            for (int i = 0; i < renderers.Count; i++)
            {
                bool visible = i < segments.Count;
                LineRenderer line = renderers[i];
                line.enabled = visible;

                if (!visible)
                {
                    continue;
                }

                PlayerEcsBridge.TrajectoryPreviewSegment segment = segments[i];
                line.SetPosition(0, ToVector3(segment.Start) + Vector3.up * verticalOffset);
                Vector3 end = ToVector3(segment.End) + Vector3.up * verticalOffset;
                Vector3 direction = (ToVector3(segment.End) - ToVector3(segment.Start)).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, direction) * 0.32f;
                line.SetPosition(1, end);
                line.SetPosition(2, end - direction * 0.65f + side);
                line.SetPosition(3, end);
                line.SetPosition(4, end - direction * 0.65f - side);
            }
        }

        private void OnDestroy()
        {
            if (lineMaterial != null)
            {
                Destroy(lineMaterial);
            }
        }

        private void EnsureRendererCount(int count)
        {
            while (renderers.Count < count)
            {
                GameObject lineObject = new GameObject($"Punch Trajectory {renderers.Count + 1}");
                lineObject.transform.SetParent(transform, false);
                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 5;
                line.startWidth = lineWidth;
                line.endWidth = lineWidth;
                line.startColor = lineColor;
                line.endColor = lineColor;
                line.sharedMaterial = lineMaterial;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                renderers.Add(line);
            }
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
