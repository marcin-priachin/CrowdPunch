using CrowdPunch.Components;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Utilities
{
    public static class RangedMuzzlePose
    {
        public static float3 ReleasePosition(in RangedProjectileOrigin origin, in LocalTransform owner,
            ref EnemyAnimationSamples samples, float releaseProgress)
        {
            float frame = math.saturate(releaseProgress) * (samples.FrameCount - 1);
            int from = (int)frame;
            int to = math.min(from + 1, samples.FrameCount - 1);
            int start = 4 * samples.FrameCount * samples.BoneCount;
            float3 a = math.mul(samples.Matrices[start + from * samples.BoneCount], new float4(origin.BindPosition, 1));
            float3 b = math.mul(samples.Matrices[start + to * samples.BoneCount], new float4(origin.BindPosition, 1));
            return owner.TransformPoint(math.transform(origin.ModelToOwner, math.lerp(a, b, math.frac(frame))));
        }
    }
}
