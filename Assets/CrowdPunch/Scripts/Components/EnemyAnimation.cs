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
        public byte WasLaunched;
        public uint LaunchSequence;
        public float FlightPhase;
        public float FlightPitch;
    }

    public struct EnemyAnimationSamples
    {
        public const int MotionCount = 10;
        public const int FlyingMotion = 9;
        public int BoneCount;
        public int FrameCount;
        // Idle, eight clockwise locomotion directions, then non-looping Flying.
        public BlobArray<float> Durations;
        public BlobArray<float3x4> Matrices;
    }
}
