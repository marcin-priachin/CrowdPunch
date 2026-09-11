using System;
using UnityEngine;

namespace CrowdPunch.Editor
{
    // Enemy proportions are fixed. Resolve them once, leaving only bone skinning on the GPU.
    public static class EnemyStaticShapeMesh
    {
        public static Mesh Create(SkinnedMeshRenderer renderer)
        {
            Mesh source = renderer.sharedMesh;
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            Vector4[] tangents = source.tangents;
            var positions = new Vector3[source.vertexCount];
            var normalDeltas = new Vector3[source.vertexCount];
            var tangentDeltas = new Vector3[source.vertexCount];
            for (int shape = 0; shape < source.blendShapeCount; shape++)
            {
                float weight = renderer.GetBlendShapeWeight(shape);
                if (weight == 0f) continue;
                // The supplied Sidekick model has explicit 0 and 100 frames.
                // Reject a changed format rather than silently producing different proportions.
                if (source.GetBlendShapeFrameCount(shape) != 2
                    || source.GetBlendShapeFrameWeight(shape, 0) != 0f
                    || source.GetBlendShapeFrameWeight(shape, 1) != 100f
                    || weight < 0f || weight > 100f)
                    throw new InvalidOperationException("Expected enemy shape frames at 0 and 100, with weights in that range.");
                for (int frame = 0; frame < 2; frame++)
                {
                    source.GetBlendShapeFrameVertices(shape, frame, positions, normalDeltas, tangentDeltas);
                    float factor = frame == 0 ? 1f - weight * 0.01f : weight * 0.01f;
                    for (int vertex = 0; vertex < vertices.Length; vertex++)
                    {
                        vertices[vertex] += positions[vertex] * factor;
                        if (normals.Length != 0) normals[vertex] += normalDeltas[vertex] * factor;
                        if (tangents.Length != 0)
                        {
                            Vector3 delta = tangentDeltas[vertex] * factor;
                            tangents[vertex] += new Vector4(delta.x, delta.y, delta.z, 0f);
                        }
                    }
                }
            }
            Mesh mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = "EnemySkinningMesh";
            mesh.ClearBlendShapes();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
