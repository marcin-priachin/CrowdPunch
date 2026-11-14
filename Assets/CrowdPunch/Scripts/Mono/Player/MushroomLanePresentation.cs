using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Mono.Player
{
    // One depth-tested mesh for all lanes; the bridge sends only geometry and lock state.
    internal sealed class MushroomLanePresentation : IDisposable
    {
        private static readonly Color Ink = new Color(.12f, .045f, .19f, .92f);
        private static readonly Color Tracking = new Color(.74f, .33f, 1f, .40f);
        private static readonly Color Locked = new Color(1f, .56f, .14f, .66f);
        private static readonly Color Mark = new Color(1f, .95f, .72f, .95f);
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly Material material;
        private readonly List<Vector3> vertices = new List<Vector3>(256);
        private readonly List<Color> colors = new List<Color>(256);
        private readonly List<int> indices = new List<int>(384);

        internal MushroomLanePresentation(Transform parent)
        {
            var go = new GameObject("Mushroom Shooter warning lanes");
            go.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Mushroom warning lanes", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>();
            var shader = Resources.Load<Shader>("Shaders/CombatFeedback");
            if (shader != null) renderer.sharedMaterial = material = new Material(shader);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
        }

        internal void Begin()
        {
            vertices.Clear();
            colors.Clear();
            indices.Clear();
        }

        internal void Add(in ProjectileVisualSample warning)
        {
            Vector3 start = warning.Position, direction = warning.Velocity;
            Vector3 end = start + direction * warning.LaneLength;
            Vector3 half = Vector3.Cross(Vector3.up, direction) * warning.Radius;
            Strip(start, end, half, Ink);
            float inset = Mathf.Min(.09f, warning.LaneLength * .2f);
            Vector3 lift = Vector3.up * .004f;
            Strip(start + direction * inset + lift, end - direction * inset + lift,
                half * .77f, warning.LaneLocked ? Locked : Tracking);
            if (!warning.LaneLocked) return;
            int marks = Mathf.Clamp(Mathf.CeilToInt(warning.LaneLength / 3.5f), 1, 20);
            float length = Mathf.Min(.42f, warning.LaneLength / marks * .35f);
            for (int i = 0; i < marks; i++)
            {
                Vector3 p = Vector3.Lerp(start, end, (i + .5f) / marks) + lift * 2;
                Vector3 tip = p + direction * (length * .5f);
                Vector3 tail = p - direction * (length * .5f);
                int first = vertices.Count;
                Vertex(tail - half * .59f, Mark); Vertex(tip, Mark);
                Vertex(tail + half * .59f, Mark); Vertex(p, Mark);
                indices.Add(first); indices.Add(first + 1); indices.Add(first + 3);
                indices.Add(first + 1); indices.Add(first + 2); indices.Add(first + 3);
            }
        }

        internal void End()
        {
            if (renderer == null) return;
            renderer.enabled = vertices.Count > 0 && material != null;
            if (!renderer.enabled) return;
            mesh.Clear(false);
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
        }

        private void Strip(Vector3 start, Vector3 end, Vector3 half, Color color)
        {
            int first = vertices.Count;
            Vertex(start - half, color); Vertex(end - half, color);
            Vertex(end + half, color); Vertex(start + half, color);
            indices.Add(first); indices.Add(first + 1); indices.Add(first + 2);
            indices.Add(first); indices.Add(first + 2); indices.Add(first + 3);
        }

        private void Vertex(Vector3 point, Color color) { vertices.Add(point); colors.Add(color); }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(mesh);
            if (material != null) UnityEngine.Object.Destroy(material);
        }
    }
}
