using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct EnemyAnimation : IComponentData
    {
        public Entity Owner;
        public BlobAssetReference<EnemyAnimationSamples> Samples;
        public float BlendResponse;
    }

    public struct EnemyAnimationPlayback : IComponentData
    {
        public float Phase;
        public float2 Movement;
        public byte Initialized;
    }

    public struct EnemyAnimationSamples
    {
        public int BoneCount;
        public int FrameCount;
        // Idle, then clockwise from forward in 45-degree steps.
        public BlobArray<float> Durations;
        public BlobArray<float3x4> Matrices;
    }
}
