using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    // A reusable world-space quad batch for bounded, presentation-only hazard particles.
    internal sealed class CombatSpriteMesh
    {
        private readonly Mesh mesh;
        private readonly List<Vector3> vertices = new List<Vector3>(4096);
        private readonly List<Vector2> uv = new List<Vector2>(4096);
        private readonly List<Vector4> circles = new List<Vector4>(4096);
        private readonly List<Color> colors = new List<Color>(4096);
        private readonly List<int> indices = new List<int>(6144);
        internal int QuadCount => vertices.Count / 4;
        public CombatSpriteMesh(string name)
        {
            mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
        }
        public void Clear() { vertices.Clear(); uv.Clear(); circles.Clear(); colors.Clear(); indices.Clear(); }
        public void Billboard(Vector3 centre, Vector3 right, Vector3 up, float radius, Color color, Vector4 clip = default)
            => Quad(centre - right * radius - up * radius, centre + right * radius - up * radius,
                centre + right * radius + up * radius, centre - right * radius + up * radius, color, clip);
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, Vector4 clip = default)
        {
            if (QuadCount >= 4096) return;
            int n = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            uv.Add(new Vector2(0,0)); uv.Add(new Vector2(1,0)); uv.Add(new Vector2(1,1)); uv.Add(new Vector2(0,1));
            for (int i=0;i<4;i++) { colors.Add(color); circles.Add(clip); }
            indices.Add(n); indices.Add(n+1); indices.Add(n+2); indices.Add(n); indices.Add(n+2); indices.Add(n+3);
        }
        public void Draw(Material material)
        {
            if (vertices.Count == 0 || material == null) return;
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetUVs(1,circles);
            mesh.SetColors(colors); mesh.SetTriangles(indices,0); mesh.RecalculateBounds();
            Graphics.DrawMesh(mesh,Matrix4x4.identity,material,0,null,0,null,ShadowCastingMode.Off,false);
        }
        public void Dispose() { if (mesh != null) Object.Destroy(mesh); }
    }
}
