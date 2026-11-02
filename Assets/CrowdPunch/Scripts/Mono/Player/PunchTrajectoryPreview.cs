using System.Collections.Generic;
using CrowdPunch.Configuration;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Mono.Player
{
    /// <summary>
    /// Renders launch arrows or dotted direct-body trajectories supplied by ECS.
    /// </summary>
    [RequireComponent(typeof(PlayerEcsBridge))]
    public sealed class PunchTrajectoryPreview : MonoBehaviour
    {
        // Keep serialized dimensions as a fallback for older standalone preview scenes.
        [SerializeField, HideInInspector] private float lineLength = 3f;
        [SerializeField, HideInInspector] private float lineWidth = 0.36f;
        private const float GroundClearance = 0.015f;

        public float LineLength => settings != null ? settings.LaunchArrowLength : Mathf.Max(0f, lineLength);

        private PlayerPunchSettings settings;
        private PlayerEcsBridge ecsBridge;
        private Material lineMaterial;
        private Mesh arrowMesh;
        private Mesh collisionArrowMesh;
        private Mesh dottedMesh;
        private readonly List<Vector3> dotVertices = new List<Vector3>();
        private readonly List<int> dotTriangles = new List<int>();
        private readonly List<Color> dotColors = new List<Color>();
        private static readonly Vector3[] DotRim = CreateDotRim();
        private readonly Color[] vertexColors = new Color[9];
        private Color directColor, collisionColor;
        private MaterialPropertyBlock arrowProperties;
        private static readonly int OpacityProperty = Shader.PropertyToID("_Opacity");

        public void Initialize(PlayerPunchSettings punchSettings) => settings = punchSettings;

        private void Awake()
        {
            ecsBridge = GetComponent<PlayerEcsBridge>();
            arrowProperties = new MaterialPropertyBlock();
            Shader shader = Resources.Load<Shader>("Shaders/CombatFeedback");
            if (shader != null)
            {
                lineMaterial = new Material(shader)
                {
                    name = "Punch Trajectory Preview Material"
                };
                arrowMesh = CreateArrowMesh();
                collisionArrowMesh = CreateArrowMesh();
                collisionArrowMesh.name = "Predicted Collision Arrow";
                dottedMesh = new Mesh { name = "Dotted Punch Trajectories", indexFormat = IndexFormat.UInt32 };
                dottedMesh.MarkDynamic();
                directColor = collisionColor = new Color(1f, 1f, 1f, 0.5f);
            }
        }

        private void LateUpdate()
        {
            if (lineMaterial == null || arrowMesh == null)
            {
                return;
            }

            UpdateColor(arrowMesh, settings != null ? settings.LaunchArrowColor : new Color(1f, 1f, 1f, 0.5f), ref directColor);
            UpdateColor(collisionArrowMesh, settings != null ? settings.CollisionArrowColor : new Color(1f, 0.7f, 0.15f, 0.5f), ref collisionColor);
            IReadOnlyList<PlayerEcsBridge.TrajectoryPreviewSegment> segments = ecsBridge.TrajectoryPreviewSegments;
            DrawDottedTrajectories(segments);
            for (int i = 0; i < segments.Count; i++)
            {
                PlayerEcsBridge.TrajectoryPreviewSegment segment = segments[i];
                if (segment.IsDotted) continue;
                Vector3 direction = ToVector3(segment.End - segment.Start);
                direction.y = 0f;
                float length = direction.magnitude;
                if (length < 0.001f)
                {
                    continue;
                }

                direction /= length;
                Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f
                    ? Vector3.forward : Vector3.up;
                float width = settings != null
                    ? segment.IsCollision ? settings.CollisionArrowWidth : settings.LaunchArrowWidth
                    : lineWidth;
                // World-space matrices keep the arrow independent of the giant player's scale.
                Matrix4x4 matrix = Matrix4x4.TRS(
                    GroundPosition(segment.Start),
                    Quaternion.LookRotation(direction, up),
                    new Vector3(Mathf.Max(0.01f, width), 1f, length));
                arrowProperties.SetFloat(OpacityProperty, segment.Opacity);
                Graphics.DrawMesh(segment.IsCollision ? collisionArrowMesh : arrowMesh, matrix, lineMaterial, gameObject.layer, null, 0, arrowProperties,
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
            if (collisionArrowMesh != null) Destroy(collisionArrowMesh);
            if (dottedMesh != null) Destroy(dottedMesh);
        }

        private void DrawDottedTrajectories(IReadOnlyList<PlayerEcsBridge.TrajectoryPreviewSegment> segments)
        {
            dotVertices.Clear(); dotTriangles.Clear(); dotColors.Clear();
            float diameter = settings != null ? settings.TrajectoryDotDiameter : .22f;
            float spacing = settings != null ? settings.TrajectoryDotSpacing : .65f;
            Color color = settings != null ? settings.TrajectoryDotColor : new Color(1, 1, 1, .5f);
            float impactRadius = (settings != null ? settings.TrajectoryImpactDiameter : 1.1f) * .5f;
            var impacts = ecsBridge.TrajectoryPreviewImpacts;
            float totalLength = 0f;
            foreach (var segment in segments)
                if (segment.IsDotted) totalLength += math.distance(segment.Start.xz, segment.End.xz);
            // Preserve the whole path at extreme tuning/crowd sizes by reducing dot density.
            spacing = Mathf.Max(spacing, totalLength / 16000f);
            float nextDot = 0f;
            for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
            {
                var segment = segments[segmentIndex];
                if (!segment.IsDotted) continue;
                bool endpoint = segmentIndex == segments.Count - 1 || segments[segmentIndex + 1].StartsPath
                    || !segments[segmentIndex + 1].IsDotted;
                if (segment.StartsPath) nextDot = 0f;
                Vector3 start = GroundPosition(segment.Start);
                Vector3 delta = GroundPosition(segment.End) - start;
                float length = delta.magnitude;
                if (length <= .00001f) continue;
                Vector3 direction = delta / length;
                // Carry spacing through bends and time steps instead of bunching dots at every vertex.
                while (nextDot <= length && (!endpoint || nextDot < length - diameter))
                {
                    Vector3 position = start + direction * nextDot;
                    bool insideAccent = false;
                    foreach (var impact in impacts)
                        insideAccent |= math.distancesq(impact.Position.xz, new float2(position.x, position.z))
                            < (impactRadius + diameter * .5f) * (impactRadius + diameter * .5f) && impactRadius > 0f;
                    if (!insideAccent) AddDot(position, diameter * .5f, color);
                    nextDot += spacing;
                }
                nextDot -= length;
                if (endpoint)
                {
                    Vector3 end = GroundPosition(segment.End);
                    bool insideAccent = false;
                    foreach (var impact in impacts)
                        insideAccent |= math.distancesq(impact.Position.xz, new float2(end.x, end.z))
                            < (impactRadius + diameter * .5f) * (impactRadius + diameter * .5f) && impactRadius > 0f;
                    if (!insideAccent) AddDot(end, diameter * .5f, color);
                }
            }
            if (impactRadius > 0f)
                foreach (var impact in impacts)
                    AddImpactAccent(GroundPosition(impact.Position),
                        ToVector3(impact.Direction), impactRadius, color);
            dottedMesh.Clear();
            if (dotVertices.Count == 0) return;
            dottedMesh.SetVertices(dotVertices);
            dottedMesh.SetColors(dotColors);
            dottedMesh.SetTriangles(dotTriangles, 0);
            dottedMesh.RecalculateBounds();
            arrowProperties.SetFloat(OpacityProperty, 1f);
            Graphics.DrawMesh(dottedMesh, Matrix4x4.identity, lineMaterial, gameObject.layer, null, 0, arrowProperties,
                ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
        }

        private Vector3 GroundPosition(float3 position)
        {
            return new Vector3(position.x, ecsBridge.TrajectoryGroundHeight + GroundClearance, position.z);
        }

        private void AddImpactAccent(Vector3 center, Vector3 direction, float radius, Color color)
        {
            direction.y = 0f;
            Vector3 forward = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.forward;
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            int start = dotVertices.Count;
            const int corners = 16;
            for (int i = 0; i < corners; i++)
            {
                float angle = i * Mathf.PI * 2f / corners;
                Vector3 radial = forward * Mathf.Cos(angle) + right * Mathf.Sin(angle);
                // Four broad primary spikes and four shorter diagonals form a hollow comic pop.
                float outer = i % 2 != 0 ? .42f : i % 4 == 0 ? 1f : .74f;
                dotVertices.Add(center + radial * (radius * outer)); dotColors.Add(color);
                dotVertices.Add(center + radial * (radius * .2f)); dotColors.Add(color);
                int current = start + i * 2;
                int next = start + (i + 1) % corners * 2;
                dotTriangles.Add(current); dotTriangles.Add(next); dotTriangles.Add(current + 1);
                dotTriangles.Add(current + 1); dotTriangles.Add(next); dotTriangles.Add(next + 1);
            }
        }

        private void AddDot(Vector3 center, float radius, Color color)
        {
            int start = dotVertices.Count;
            dotVertices.Add(center); dotColors.Add(color);
            for (int i = 0; i < DotRim.Length; i++)
            {
                dotVertices.Add(center + DotRim[i] * radius); dotColors.Add(color);
                dotTriangles.Add(start);
                dotTriangles.Add(start + i + 1);
                dotTriangles.Add(start + (i + 1) % DotRim.Length + 1);
            }
        }

        private static Vector3[] CreateDotRim()
        {
            var rim = new Vector3[8];
            for (int i = 0; i < rim.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / rim.Length;
                rim[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
            return rim;
        }

        private void UpdateColor(Mesh mesh, Color color, ref Color previous)
        {
            if (color == previous) return;
            for (int i = 0; i < vertexColors.Length; i++) vertexColors[i] = color;
            mesh.colors = vertexColors;
            previous = color;
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
            mesh.UploadMeshData(false);
            return mesh;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
