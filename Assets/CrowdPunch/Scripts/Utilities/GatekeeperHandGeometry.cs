using System;
using System.IO;
using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CrowdPunch.Utilities
{
    // Measures the supplied skinned mesh in the same model space as the ECS animation samples.
    public static class GatekeeperHandGeometry
    {
        public static BlobAssetReference<GatekeeperHandBounds> CreateBounds(Mesh mesh, byte[] sides,
            int leftPalm, int rightPalm, TextAsset asset)
        {
            using var reader = new BinaryReader(new MemoryStream(asset.bytes));
            if (reader.ReadInt32() != 0x43504133) throw new InvalidDataException("Unsupported Gatekeeper animation samples.");
            reader.ReadString();
            int bones = reader.ReadInt32(), frames = reader.ReadInt32();
            if (bones != sides.Length || frames < 2 || frames > 120)
                throw new InvalidDataException("Gatekeeper hand bounds require matching animation bones and frames.");
            for (int i = 0; i < bones; i++) reader.ReadString();
            for (int i = 0; i < EnemyAnimationSamples.MotionCount; i++) reader.ReadSingle();
            var vertices = mesh.vertices;
            var weights = mesh.boneWeights;
            var vertexSides = new byte[vertices.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                var w = weights[i];
                byte side = sides[w.boneIndex0];
                if (w.weight1 > .00001f && sides[w.boneIndex1] != side
                    || w.weight2 > .00001f && sides[w.boneIndex2] != side
                    || w.weight3 > .00001f && sides[w.boneIndex3] != side)
                    throw new InvalidOperationException("Gatekeeper hands must be detached mesh islands with no weights shared with the head.");
                vertexSides[i] = side;
            }
            var bind = mesh.bindposes;
            float3 leftBind = bind[leftPalm].inverse.MultiplyPoint3x4(Vector3.zero);
            float3 rightBind = bind[rightPalm].inverse.MultiplyPoint3x4(Vector3.zero);
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var data = ref builder.ConstructRoot<GatekeeperHandBounds>();
            data.FrameCount = frames;
            var bounds = builder.Allocate(ref data.Frames, EnemyAnimationSamples.MotionCount * frames * 2);
            var matrices = new float3x4[bones];
            var points = new float3[vertices.Length];
            for (int frame = 0; frame < EnemyAnimationSamples.MotionCount * frames; frame++)
            {
                for (int bone = 0; bone < bones; bone++)
                    matrices[bone] = new float3x4(Column(reader), Column(reader), Column(reader), Column(reader));
                float3 lp = Transform(matrices[leftPalm], leftBind), rp = Transform(matrices[rightPalm], rightBind);
                var left = new GatekeeperHandFrame { Min = new float3(float.MaxValue), Max = new float3(float.MinValue) };
                var right = left;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (vertexSides[i] == 0) continue;
                    var w = weights[i];
                    float3 v = vertices[i];
                    float3 p = Transform(matrices[w.boneIndex0], v) * w.weight0 + Transform(matrices[w.boneIndex1], v) * w.weight1
                        + Transform(matrices[w.boneIndex2], v) * w.weight2 + Transform(matrices[w.boneIndex3], v) * w.weight3;
                    points[i] = p - (vertexSides[i] == 1 ? lp : rp);
                    if (vertexSides[i] == 1) { left.Min = math.min(left.Min, points[i]); left.Max = math.max(left.Max, points[i]); }
                    else { right.Min = math.min(right.Min, points[i]); right.Max = math.max(right.Max, points[i]); }
                }
                if (!math.all(math.isfinite(left.Min)) || math.any(left.Min > left.Max) || math.any(right.Min > right.Max))
                    throw new InvalidDataException("Both Gatekeeper hand mesh islands are required.");
                bounds[frame * 2] = left;
                bounds[frame * 2 + 1] = right;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (vertexSides[i] == 1)
                        data.LeftRadius = math.max(data.LeftRadius, math.length((points[i] - (left.Min + left.Max) * .5f).xz));
                    else if (vertexSides[i] == 2)
                        data.RightRadius = math.max(data.RightRadius, math.length((points[i] - (right.Min + right.Max) * .5f).xz));
                }
            }
            return builder.CreateBlobAssetReference<GatekeeperHandBounds>(Allocator.Persistent);
        }

        private static float3 Column(BinaryReader reader) => new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        private static float3 Transform(float3x4 matrix, float3 v) => matrix.c0 * v.x + matrix.c1 * v.y + matrix.c2 * v.z + matrix.c3;
    }
}
