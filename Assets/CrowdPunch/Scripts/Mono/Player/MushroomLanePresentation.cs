using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Mono.Player
{
    // Batch the floor warnings and the active beams in one depth-tested mesh.
    internal sealed class MushroomLanePresentation : IDisposable
    {
        private static readonly Color WarningColor = new Color(.74f, .33f, 1f, .24f);
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly Material material;
        private readonly List<Vector3> vertices = new List<Vector3>(256);
        private readonly List<Color> colors = new List<Color>(256);
        private readonly List<int> indices = new List<int>(384);

        internal MushroomLanePresentation(Transform parent)
        {
            var go = new GameObject("Mushroom Shooter beams and warnings");
            go.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Mushroom beams and warnings", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>();
            var shader = Resources.Load<Shader>("Shaders/CombatFeedback");
            if (shader != null) renderer.sharedMaterial = material = new Material(shader);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
        }

        internal void AddBeam(in ProjectileVisualSample beam, Color ownership)
        {
            Vector3 start = beam.Position, direction = beam.Velocity;
            Vector3 end = start + direction * beam.LaneLength;
            Vector3 horizontal = Vector3.Cross(Vector3.up, direction) * beam.Radius;
            Vector3 vertical = Vector3.up * beam.Radius;
            Color edge = ownership;
            edge.a = .65f;
            Color core = Color.Lerp(ownership, Color.white, .65f) * 2f;
            core.a = 1f;
            Strip(start, end, horizontal, edge);
            Strip(start, end, vertical, edge);
            Strip(start, end, horizontal * .23f, core);
            Strip(start, end, vertical * .23f, core);
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
            Strip(start, end, half, WarningColor);
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
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(mesh);
                UnityEngine.Object.Destroy(material);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }
    }
}
