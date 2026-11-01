using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

namespace CrowdPunch.Utilities
{
    /// <summary>Builds static collision surfaces from an obstacle's rendered child meshes.</summary>
    public static class SolidObstacleGeometry
    {
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
