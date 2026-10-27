using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    // VISION-006: faceted soil and cartoon dust. A single immutable mesh serves the crowd.
    internal sealed class EnemySpawnBurrowMesh
    {
        private const int Segments = 16;
        private const int BatchSize = 1023;
        private static readonly int DiggingId = Shader.PropertyToID("_Digging");
        private static readonly Color Edge = new Color(.19f, .105f, .055f);
        private static readonly Color Soil = new Color(.48f, .27f, .105f);
        private static readonly Color Light = new Color(.70f, .45f, .20f);
        private static readonly Color Crack = new Color(.12f, .065f, .035f);
        private readonly Mesh mesh;
        private readonly List<Matrix4x4> transforms = new List<Matrix4x4>(512);
        private readonly List<Vector4> digging = new List<Vector4>(512);
        private readonly Matrix4x4[] batchTransforms = new Matrix4x4[BatchSize];
        private readonly Vector4[] batchDigging = new Vector4[BatchSize];
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly CombatSpriteMesh dust = new CombatSpriteMesh("Enemy spawn digging dust");

        public EnemySpawnBurrowMesh() => mesh = BuildSoil();

        public void Clear() { transforms.Clear(); digging.Clear(); dust.Clear(); }

        public void Add(Vector3 center, float radius, float age, uint seed, Vector3 right, Vector3 up)
        {
            float phase = (seed % 1024) * .006135923f;
            float appear = Mathf.SmoothStep(0f, 1f, age / .12f);
            if (appear <= 0f) return;
            float r = Mathf.Max(.35f, radius) * appear;
            transforms.Add(Matrix4x4.TRS(center, Quaternion.Euler(0, -phase * Mathf.Rad2Deg, 0), Vector3.one * r));
            digging.Add(new Vector4(age, phase, 0, 0));
            for (int i = 0; i < 6; i++)
            {
                float puff = Mathf.Repeat(age * 2.2f + i * .167f, 1f);
                Vector3 direction = Direction(phase + i * Mathf.PI / 3f);
                Vector3 position = center + direction * (r * (.65f + puff * .85f))
                    + Vector3.up * (r * (.1f + puff * .4f));
                dust.Billboard(position, right, up, r * (.12f + puff * .22f),
                    new Color(.68f, .51f, .31f, Mathf.Sin(puff * Mathf.PI) * .42f * appear));
            }
        }

        public void Draw(Material soil, Material smoke)
        {
            for (int offset = 0; offset < transforms.Count; offset += BatchSize)
            {
                int count = Mathf.Min(BatchSize, transforms.Count - offset);
                transforms.CopyTo(offset, batchTransforms, 0, count);
                digging.CopyTo(offset, batchDigging, 0, count);
                properties.SetVectorArray(DiggingId, batchDigging);
                Graphics.DrawMeshInstanced(mesh, 0, soil, batchTransforms, count, properties,
                    ShadowCastingMode.Off, false, 0, null, LightProbeUsage.Off);
            }
            dust.Draw(smoke);
        }

        private static Mesh BuildSoil()
        {
            var vertices = new List<Vector3>(512);
            var colors = new List<Color>(512);
            var animation = new List<Vector2>(512);
            var triangles = new List<int>(512);
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                float next = (i + 1) * Mathf.PI * 2f / Segments;
                float a = 1f + .07f * Mathf.Sin(i * 5.7f);
                float b = 1f + .07f * Mathf.Sin(((i + 1) % Segments) * 5.7f);
                Vector3 outerA = Direction(angle) * a, outerB = Direction(next) * b;
                Vector3 rimA = outerA * .88f + Vector3.up * .13f;
                Vector3 rimB = outerB * .88f + Vector3.up * .13f;
                Vector3 innerA = Direction(angle) * .43f + Vector3.up * .77f;
                Vector3 innerB = Direction(next) * .43f + Vector3.up * .77f;
                Face(outerA, outerB, rimA, Edge);
                Face(outerB, rimB, rimA, Edge);
                Color tint = Color.Lerp(Soil, Light, .28f + .24f * Mathf.Cos(angle + .8f));
                Face(rimA, rimB, innerA, tint);
                Face(rimB, innerB, innerA, tint);
                Face(innerA, innerB, Vector3.up, Color.Lerp(tint, Light, .18f));
            }
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2f / 5;
                Vector3 direction = Direction(angle), side = Direction(angle + Mathf.PI * .5f);
                Vector3 previous = Vector3.up * .936f + direction * .12f;
                for (int step = 1; step <= 3; step++)
                {
                    float distance = .12f + step * .235f;
                    float y = distance < .43f ? Mathf.Lerp(1f, .77f, distance / .43f)
                        : Mathf.Lerp(.77f, .13f, (distance - .43f) / .45f);
                    Vector3 point = direction * distance + side * (step % 2 == 0 ? -.055f : .055f) + Vector3.up * y;
                    float width = .025f * (1f - distance * .5f);
                    Face(previous - side * width, previous + side * width, point - side * width, Crack, 2);
                    Face(previous + side * width, point + side * width, point - side * width, Crack, 2);
                    previous = point;
                }
            }
            for (int i = 0; i < 6; i++)
            {
                Vector3 x = Vector3.right, z = Vector3.forward * .75f;
                Vector3 top = Vector3.up, bottom = Vector3.down * .65f;
                Face(top, x, z, Light, 1, i); Face(top, z, -x, Soil, 1, i);
                Face(top, -x, -z, Soil, 1, i); Face(top, -z, x, Light, 1, i);
                Face(bottom, z, x, Edge, 1, i); Face(bottom, -x, z, Edge, 1, i);
                Face(bottom, -z, -x, Soil, 1, i); Face(bottom, x, -z, Soil, 1, i);
            }
            var result = new Mesh { name = "Shared cartoon burrow soil" };
            result.SetVertices(vertices); result.SetColors(colors); result.SetUVs(0, animation);
            result.SetTriangles(triangles, 0);
            // Include shader-driven heave and clod arcs in instance culling bounds.
            result.bounds = new Bounds(new Vector3(0, .5f, 0), new Vector3(3, 2, 3));
            result.UploadMeshData(true);
            return result;

            void Face(Vector3 a, Vector3 b, Vector3 c, Color color, float kind = 0, float lane = 0)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                for (int k = 0; k < 3; k++) { colors.Add(color); animation.Add(new Vector2(kind, lane)); triangles.Add(start + k); }
            }
        }

        private static Vector3 Direction(float angle) => new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));

        public void Dispose()
        {
            if (Application.isPlaying) Object.Destroy(mesh);
            else Object.DestroyImmediate(mesh);
            dust.Dispose();
        }
    }
}
