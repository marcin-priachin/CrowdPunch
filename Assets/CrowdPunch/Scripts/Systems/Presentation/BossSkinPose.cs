using CrowdPunch.Components;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Systems.Presentation
{
    internal static class BossSkinPose
    {
        public static void Sample(ref EnemyAnimationSamples data, ref DynamicBuffer<SkinMatrix> skin,
            int motion, float phase, bool loop, float3 anchor, float3 scale, quaternion rotation,
            float blend)
        {
            float frame = phase * (loop ? data.FrameCount : data.FrameCount - 1);
            int a = math.min((int)frame, data.FrameCount - 1);
            int b = loop ? (a + 1) % data.FrameCount : math.min(a + 1, data.FrameCount - 1);
            float fraction = math.frac(frame);
            var accent = math.mul(new float3x3(rotation), float3x3.Scale(scale));
            for (int bone = 0; bone < skin.Length; bone++)
            {
                var from = data.Matrices[(motion * data.FrameCount + a) * data.BoneCount + bone];
                var to = data.Matrices[(motion * data.FrameCount + b) * data.BoneCount + bone];
                var target = new float3x4(
                    math.mul(accent, math.lerp(from.c0, to.c0, fraction)),
                    math.mul(accent, math.lerp(from.c1, to.c1, fraction)),
                    math.mul(accent, math.lerp(from.c2, to.c2, fraction)),
                    anchor + math.mul(accent, math.lerp(from.c3, to.c3, fraction) - anchor));
                var previous = skin[bone].Value;
                skin[bone] = new SkinMatrix { Value = new float3x4(
                    math.lerp(previous.c0, target.c0, blend), math.lerp(previous.c1, target.c1, blend),
                    math.lerp(previous.c2, target.c2, blend), math.lerp(previous.c3, target.c3, blend)) };
            }
        }
    }
}
