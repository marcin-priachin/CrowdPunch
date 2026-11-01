using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using CrowdPunch.Components;

namespace CrowdPunch.Utilities
{
    /// <summary>Builds static collision surfaces from an obstacle's rendered child meshes.</summary>
    public static class SolidObstacleGeometry
    {
        public static NavigationRectangle ExpandNavigationFootprint(NavigationRectangle footprint, MeshFilter surface, float minimumY, float maximumY)
        {
            var vertices = surface.sharedMesh.vertices;
            var indices = surface.sharedMesh.triangles;
            var matrix = surface.transform.localToWorldMatrix;
            for (int i = 0; i < indices.Length; i += 3)
            {
                float3 a = matrix.MultiplyPoint3x4(vertices[indices[i]]);
                float3 b = matrix.MultiplyPoint3x4(vertices[indices[i + 1]]);
                float3 c = matrix.MultiplyPoint3x4(vertices[indices[i + 2]]);
                IncludeEdge(a, b, minimumY, maximumY, ref footprint);
                IncludeEdge(b, c, minimumY, maximumY, ref footprint);
                IncludeEdge(c, a, minimumY, maximumY, ref footprint);
            }
            return footprint;
        }

        private static void IncludeEdge(float3 a, float3 b, float low, float high, ref NavigationRectangle footprint)
        {
            float from = 0, to = 1, dy = b.y - a.y;
            if (math.abs(dy) < 1e-6f) { if (a.y < low || a.y > high) return; }
            else
            {
                float t0 = (low - a.y) / dy, t1 = (high - a.y) / dy;
                from = math.max(0, math.min(t0, t1)); to = math.min(1, math.max(t0, t1));
                if (from > to) return;
            }
            float2 p = math.lerp(a, b, from).xz, q = math.lerp(a, b, to).xz;
            footprint.Minimum = math.min(footprint.Minimum, math.min(p, q));
            footprint.Maximum = math.max(footprint.Maximum, math.max(p, q));
        }

        public static BlobAssetReference<Unity.Physics.Collider> CreateCollider(Transform root, MeshFilter[] surfaces)
        {
            using var vertices = new NativeList<float3>(Allocator.Temp);
            using var triangles = new NativeList<int3>(Allocator.Temp);
            foreach (var surface in surfaces)
            {
                var mesh = surface.sharedMesh;
                if (mesh == null) continue;
                var transform = root.worldToLocalMatrix * surface.transform.localToWorldMatrix;
                int offset = vertices.Length;
                foreach (var vertex in mesh.vertices) vertices.Add(transform.MultiplyPoint3x4(vertex));
                var indices = mesh.triangles;
                bool mirrored = transform.determinant < 0;
                for (int i = 0; i < indices.Length; i += 3)
                    triangles.Add(new int3(indices[i], indices[i + (mirrored ? 2 : 1)], indices[i + (mirrored ? 1 : 2)]) + offset);
            }
            if (triangles.Length == 0)
                throw new InvalidOperationException($"Solid obstacle '{root.name}' requires a visible triangle mesh.");

            // A convex hull or bounding box would fill the visible notches and spaces between meshes.
            return Unity.Physics.MeshCollider.Create(vertices.AsArray(), triangles.AsArray(),
                new CollisionFilter { BelongsTo = 1u, CollidesWith = uint.MaxValue }, Unity.Physics.Material.Default);
        }
    }
}
