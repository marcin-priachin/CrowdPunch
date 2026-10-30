using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct StandingTurnAnimation : IComponentData
    {
        public BlobAssetReference<StandingTurnSamples> Samples;
        public float MinimumAngle, MinimumSpeed;
        public float BlendInDuration, BlendOutDuration, StopDelay;
    }

    public struct StandingTurnPlayback : IComponentData
    {
        public float3 PreviousForward;
        public float AccumulatedYaw, StoppedTime, Weight, Phase;
        public int Direction;
        public byte Initialized;
    }

    public struct StandingTurnSamples
    {
        public int BoneCount, FrameCount, HipBoneIndex;
        public float4x4 HipBindInverse;
        public BlobArray<int> LegBones;
        public BlobArray<float> Durations;
        // Left/right, then frames and leg bones; skin matrices relative to the source hips.
        public BlobArray<float3x4> Matrices;
    }
}
