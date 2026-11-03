using CrowdPunch.Systems.Combat;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Systems.Presentation
{
    // DRAGON-002: a mouth-attached volume, rebuilt from solid-cover reaches rather than free particles.
    internal sealed class DragonFlameStreamMesh
    {
        internal const int AngularSteps = 80;
        private const int LengthSteps = 16;
        private readonly Mesh mesh = new Mesh { name = "Connected cover-clipped dragon fire", indexFormat = IndexFormat.UInt32 };
        private NativeList<float3> vertices = new NativeList<float3>(8192, Allocator.Persistent);
        private NativeList<float2> uv = new NativeList<float2>(8192, Allocator.Persistent);
        private NativeList<int> triangles = new NativeList<int>(32768, Allocator.Persistent);

        internal DragonFlameStreamMesh() => mesh.MarkDynamic();

        internal void Clear()
        {
            vertices.Clear(); uv.Clear();
            mesh.Clear();
        }

        internal void Append(in DragonCone cone, NativeArray<float> reaches, NativeArray<float2> directions, float time)
        {
            if (cone.Profile.Length <= .001f) return;
            int first = vertices.Length;
            int count = AngularSteps * 2 * (LengthSteps + 1) * 2;
            vertices.ResizeUninitialized(first + count); uv.ResizeUninitialized(first + count);
            new BuildStreamJob {
                Cone = cone, Reaches = reaches, Directions = directions, Time = time, First = first,
                Vertices = vertices.AsArray(), Uv = uv.AsArray()
            }.Run();
        }

        [BurstCompile]
        private struct BuildStreamJob : IJob
        {
            public DragonCone Cone;
            [ReadOnly] public NativeArray<float> Reaches;
            [ReadOnly] public NativeArray<float2> Directions;
            public float Time;
            public int First;
            public NativeArray<float3> Vertices;
            public NativeArray<float2> Uv;

            public void Execute()
            {
                float length = Cone.Profile.Length;
                int vertex = First;
                for (int cell = 0; cell < AngularSteps; cell++)
                {
                    // Sample both edges and the midpoint. Taking the shortest reach keeps each
                    // strip in front of cover; interpolating a near and far endpoint could bridge a wall edge.
                    int ray = cell * 2;
                    float reach = math.min(Reaches[ray], math.min(Reaches[ray + 1], Reaches[ray + 2]));
                    for (int shell = 0; shell < 2; shell++)
                    {
                        for (int step = 0; step <= LengthSteps; step++)
                        {
                            // Fixed distance stations keep adjacent strips joined before a wall.
                            // UVs use the full attack length, so cover cuts the stream rather than compressing it.
                            float distance = math.min(length * step / LengthSteps, reach);
                            float along = distance / length;
                            for (int side = 0; side < 2; side++)
                            {
                                float across = (cell + side) / (float)AngularSteps;
                                float lateral = across * 2 - 1;
                                float arch = math.sqrt(math.max(0, 1 - lateral * lateral));
                                float opening = math.smoothstep(0, .08f, along);
                                // Roll the volume vertically only. Horizontal distortion would leak past
                                // the authoritative cone/cover boundary as the dragon turns or flies.
                                float roll = .85f + .15f * math.sin(along * 19 - Time * 8 + lateral * 2);
                                float thickness = (.18f + 1.7f * math.sin(along * math.PI * .9f)) * opening * arch * roll;
                                float centre = math.lerp(Cone.Mouth.y, math.max(Cone.Tuning.GroundY + .5f, Cone.Mouth.y - .9f), along);
                                float height = centre + thickness * (shell == 0 ? 1 : -.55f);
                                float2 point = Cone.Mouth.xz + Directions[ray + side * 2] * distance;
                                Vertices[vertex] = new float3(point.x, math.max(Cone.Tuning.GroundY + .06f, height), point.y);
                                Uv[vertex++] = new float2(across, along);
                            }
                        }
                    }
                }
            }
        }

        internal void Draw(Material material)
        {
            if (vertices.Length == 0 || material == null) return;
            int stripVertices = (LengthSteps + 1) * 2;
            int cellVertices = stripVertices * 2;
            int cellIndices = (LengthSteps * 2 + 1) * 6;
            int indexCount = vertices.Length / cellVertices * cellIndices;
            // Topology is identical for every source, even when cover collapses distant rows.
            // Grow once and reuse the index prefix as the number of emitting Dragons changes.
            for (int first = triangles.Length / cellIndices * cellVertices; first < vertices.Length; first += cellVertices)
            {
                for (int shell = 0; shell < 2; shell++)
                    for (int step = 0; step < LengthSteps; step++)
                    {
                        int a = first + shell * stripVertices + step * 2;
                        AddQuad(a, a + 1, a + 3, a + 2);
                    }
                // Close the exposed cross section where cover shortens a strip.
                int tip = first + LengthSteps * 2;
                AddQuad(tip, tip + 1, tip + stripVertices + 1, tip + stripVertices);
            }
            mesh.SetVertices(vertices.AsArray()); mesh.SetUVs(0, uv.AsArray());
            mesh.SetIndices(triangles.AsArray().GetSubArray(0, indexCount), MeshTopology.Triangles, 0, false);
            mesh.RecalculateBounds();
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0, null, 0, null, ShadowCastingMode.Off, false);
        }

        private void AddQuad(int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        internal void Dispose()
        {
            vertices.Dispose(); uv.Dispose(); triangles.Dispose();
            if (Application.isPlaying) Object.Destroy(mesh);
            else Object.DestroyImmediate(mesh);
        }
    }
}
