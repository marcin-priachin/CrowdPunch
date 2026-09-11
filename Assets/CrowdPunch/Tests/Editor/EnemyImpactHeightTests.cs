using System.IO;
using CrowdPunch.Components;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class EnemyImpactHeightTests
    {
        [Test]
        public void Info004SampledImpactDropsToGround()
        {
            var bytes = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/CrowdPunch/Animation/EnemyMovement.bytes");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CrowdPunch/Animation/EnemySkinningMesh.asset");
            using var reader = new BinaryReader(new MemoryStream(bytes.bytes));
            Assert.That(reader.ReadInt32(), Is.EqualTo(0x43504133));
            reader.ReadString();
            int bones = reader.ReadInt32();
            int frames = reader.ReadInt32();
            for (int i = 0; i < bones; i++) reader.ReadString();
            for (int i = 0; i < EnemyAnimationSamples.MotionCount; i++) reader.ReadSingle();
            reader.BaseStream.Position += EnemyAnimationSamples.ImpactMotion * frames * bones * 48;
            var matrices = new Matrix4x4[bones];
            Vector3[] vertices = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            float startTop = 0f;
            float endTop = 0f;
            for (int frame = 0; frame < frames; frame++)
            {
                for (int bone = 0; bone < bones; bone++)
                    for (int column = 0; column < 4; column++)
                        for (int row = 0; row < 3; row++) matrices[bone][row, column] = reader.ReadSingle();
                float bottom = float.MaxValue;
                float top = float.MinValue;
                for (int vertex = 0; vertex < vertices.Length; vertex++)
                {
                    BoneWeight w = weights[vertex];
                    Vector3 p = matrices[w.boneIndex0].MultiplyPoint3x4(vertices[vertex]) * w.weight0
                        + matrices[w.boneIndex1].MultiplyPoint3x4(vertices[vertex]) * w.weight1
                        + matrices[w.boneIndex2].MultiplyPoint3x4(vertices[vertex]) * w.weight2
                        + matrices[w.boneIndex3].MultiplyPoint3x4(vertices[vertex]) * w.weight3;
                    bottom = Mathf.Min(bottom, p.y);
                    top = Mathf.Max(top, p.y);
                }
                Assert.That(bottom, Is.GreaterThanOrEqualTo(-0.001f), "Sample penetrates model ground at frame " + frame);
                if (frame == 0) startTop = top;
                if (frame == frames - 1)
                {
                    endTop = top;
                    Assert.That(bottom, Is.EqualTo(0f).Within(0.01f));
                }
            }
            Assert.That(startTop - endTop, Is.GreaterThan(0.8f), "Fall must visibly lower the body.");
        }
    }
}
