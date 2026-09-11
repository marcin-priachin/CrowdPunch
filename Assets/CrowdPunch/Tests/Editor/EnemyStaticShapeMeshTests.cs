using CrowdPunch.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class EnemyStaticShapeMeshTests
    {
        [Test]
        public void Info004StaticProportionsMatchOriginalSkinnedModel()
        {
            GameObject root = PrefabUtility.LoadPrefabContents("Assets/CrowdPunch/Models/BaseEnemy/BaseEnemy.prefab");
            var before = new Mesh();
            var after = new Mesh();
            Mesh generated = null;
            try
            {
                var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
                renderer.quality = SkinQuality.Bone4;
                Mesh source = renderer.sharedMesh;
                renderer.BakeMesh(before);
                generated = EnemyStaticShapeMesh.Create(renderer);
                Assert.That(generated.blendShapeCount, Is.Zero);
                Assert.That(generated.bindposes, Is.EqualTo(source.bindposes));
                Assert.That(generated.boneWeights, Is.EqualTo(source.boneWeights));
                Assert.That(generated.triangles, Is.EqualTo(source.triangles));
                renderer.sharedMesh = generated;
                renderer.BakeMesh(after);
                Vector3[] expected = before.vertices;
                Vector3[] actual = after.vertices;
                Vector3[] expectedNormals = before.normals;
                Vector3[] actualNormals = after.normals;
                Assert.That(actual.Length, Is.EqualTo(expected.Length));
                float maxPositionError = 0f;
                float maxNormalError = 0f;
                for (int i = 0; i < actual.Length; i++)
                {
                    maxPositionError = Mathf.Max(maxPositionError, Vector3.Distance(expected[i], actual[i]));
                    maxNormalError = Mathf.Max(maxNormalError, Vector3.Distance(expectedNormals[i], actualNormals[i]));
                }
                Assert.That(maxPositionError, Is.LessThan(0.0001f));
                Assert.That(maxNormalError, Is.LessThan(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(before);
                Object.DestroyImmediate(after);
                if (generated != null) Object.DestroyImmediate(generated);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
