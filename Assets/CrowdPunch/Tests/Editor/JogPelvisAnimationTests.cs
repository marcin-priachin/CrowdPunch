using CrowdPunch.Components;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Tests
{
    public sealed class JogPelvisAnimationTests
    {
        [TestCase(0f)] [TestCase(45f)] [TestCase(90f)] [TestCase(180f)]
        public void Info004_ActionHipTwistCannotCrossMovingFeet(float actionYaw)
        {
            using var world = new World("Moving pelvis regression");
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var data = ref builder.ConstructRoot<EnemyJogSamples>();
            data.BoneCount = 4; data.FrameCount = 2; data.HipBoneIndex = 0;
            data.HipBind = data.HipBindInverse = float4x4.identity;
            var mask = builder.Allocate(ref data.LegBones, 2); mask[0] = 1; mask[1] = 2;
            var durations = builder.Allocate(ref data.Durations, 8);
            var distances = builder.Allocate(ref data.CycleDistances, 8);
            var legs = builder.Allocate(ref data.Matrices, 32);
            var hips = builder.Allocate(ref data.HipMatrices, 16);
            for (int direction = 0; direction < 8; direction++)
            {
                durations[direction] = 1; distances[direction] = 2;
                for (int frame = 0; frame < 2; frame++)
                {
                    hips[direction * 2 + frame] = Pose(float4x4.identity);
                    legs[(direction * 2 + frame) * 2] = Pose(float4x4.Translate(new float3(-.25f, -1, .4f)));
                    legs[(direction * 2 + frame) * 2 + 1] = Pose(float4x4.Translate(new float3(.25f, -1, -.4f)));
                }
            }
            using var samples = builder.CreateBlobAssetReference<EnemyJogSamples>(Allocator.Persistent);
            var entity = world.EntityManager.CreateEntity();
            var skin = world.EntityManager.AddBuffer<SkinMatrix>(entity);
            var hip = float4x4.TRS(new float3(3, 4, 5), quaternion.RotateY(math.radians(actionYaw)), new float3(2));
            var upper = Pose(float4x4.TRS(new float3(3, 8, 5), quaternion.RotateX(.3f), new float3(1.5f)));
            skin.Add(new SkinMatrix { Value = Pose(hip) });
            skin.Add(new SkinMatrix()); skin.Add(new SkinMatrix()); skin.Add(new SkinMatrix { Value = upper });
            var playback = new EnemyAnimationPlayback { Movement = new float2(0, 1) };
            ChickenSkinPose.Jog(ref samples.Value, ref skin, ref playback, .02f, 2);
            Assert.That(skin[2].Value.c3.x - skin[1].Value.c3.x, Is.EqualTo(1f).Within(.001f),
                "Action/Sweep pelvis yaw must not rotate the feet across one another");
            Assert.That(skin[0].Value.c3, Is.EqualTo(hip.c3.xyz), "Keep the authored pelvis position");
            Assert.That(math.length(skin[0].Value.c0), Is.EqualTo(2f).Within(.001f));
            Assert.That(math.length(skin[0].Value.c1), Is.EqualTo(2f).Within(.001f));
            Assert.That(math.length(skin[0].Value.c2), Is.EqualTo(2f).Within(.001f));
            Assert.That(skin[3].Value, Is.EqualTo(upper), "Keep the authored upper body independent");
        }

        private static float3x4 Pose(float4x4 p) => new float3x4(p.c0.xyz, p.c1.xyz, p.c2.xyz, p.c3.xyz);
    }
}
