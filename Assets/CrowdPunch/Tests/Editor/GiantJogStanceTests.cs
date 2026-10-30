using System;
using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class GiantJogStanceTests
    {
        [TestCase(0)] [TestCase(40)] [TestCase(80)]
        public void Info004_MovingGiantFeetStaySeparatedAcrossDirectionAndStrideBlends(int level)
        {
            var scene = level == 0 ? default : EditorSceneManager.OpenPreviewScene(
                $"Assets/CrowdPunch/Scenes/Campaign/Campaign_{level}/Campaign_{level} Sub Scene.unity");
            var root = level == 0 ? PrefabUtility.LoadPrefabContents("Assets/CrowdPunch/Prefabs/EliteEnemy.prefab")
                : scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DinoBossAuthoring>()).Single().gameObject;
            try
            {
                var authoring = root.GetComponentsInChildren<EnemyAnimationAuthoring>(true).Single(a => a.JogSamples != null);
                var renderer = authoring.GetComponent<SkinnedMeshRenderer>();
                var animator = root.GetComponentInChildren<Animator>(true);
                int leftIndex = Array.IndexOf(renderer.bones, animator.GetBoneTransform(HumanBodyBones.LeftFoot));
                int rightIndex = Array.IndexOf(renderer.bones, animator.GetBoneTransform(HumanBodyBones.RightFoot));
                float4 leftBind = renderer.sharedMesh.bindposes[leftIndex].inverse.GetColumn(3);
                float4 rightBind = renderer.sharedMesh.bindposes[rightIndex].inverse.GetColumn(3);
                using var reader = new BinaryReader(new MemoryStream(authoring.JogSamples.bytes));
                Assert.That(reader.ReadInt32(), Is.EqualTo(0x43504A32), "Giants need the complete lower-body reference");
                reader.ReadString(); reader.ReadInt32(); int frames = reader.ReadInt32(); reader.ReadInt32(); int count = reader.ReadInt32();
                for (int i = 0; i < 16; i++) reader.ReadSingle();
                var bones = Enumerable.Range(0, count).Select(_ => reader.ReadInt32()).ToArray();
                for (int i = 0; i < 8; i++) reader.ReadSingle();
                var legs = new float3x4[8 * frames * count];
                for (int i = 0; i < legs.Length; i++) legs[i] = ReadPose(reader);
                var hips = new float3x4[8 * frames];
                for (int i = 0; i < hips.Length; i++) hips[i] = ReadPose(reader);
                for (int i = 0; i < 8; i++) Assert.That(reader.ReadSingle(), Is.GreaterThan(.01f), "Retargeted stride must remain measurable");
                int left = Array.IndexOf(bones, leftIndex), right = Array.IndexOf(bones, rightIndex);
                float minimum = float.MaxValue;
                for (int direction = 0; direction < 32; direction++)
                for (int frame = 0; frame < frames * 2; frame++)
                {
                    int first = direction / 4, second = (first + 1) % 8;
                    float mix = direction % 4 / 4f, fraction = frame % 2 * .5f;
                    int from = frame / 2, to = (from + 1) % frames;
                    var hip = Mix(hips[first * frames + from], hips[first * frames + to], hips[second * frames + from], hips[second * frames + to]);
                    var l = Mix(legs[(first * frames + from) * count + left], legs[(first * frames + to) * count + left],
                        legs[(second * frames + from) * count + left], legs[(second * frames + to) * count + left]);
                    var r = Mix(legs[(first * frames + from) * count + right], legs[(first * frames + to) * count + right],
                        legs[(second * frames + from) * count + right], legs[(second * frames + to) * count + right]);
                    float gap = math.mul(hip, new float4(math.mul(r, rightBind) - math.mul(l, leftBind), 0)).x;
                    minimum = math.min(minimum, gap);
                    float3x4 Mix(float3x4 a, float3x4 b, float3x4 c, float3x4 d) => ChickenSkinPose.Blend(
                        ChickenSkinPose.Blend(a, b, fraction), ChickenSkinPose.Blend(c, d, fraction), mix);
                }
                Assert.That(minimum, Is.GreaterThan(.35f), "The wide Giant must not inherit a narrow/crossed human stance");
            }
            finally
            {
                if (level == 0) PrefabUtility.UnloadPrefabContents(root);
                else EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static float3x4 ReadPose(BinaryReader reader) => new float3x4(Read(reader), Read(reader), Read(reader), Read(reader));
        private static float3 Read(BinaryReader reader) => new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }
}
