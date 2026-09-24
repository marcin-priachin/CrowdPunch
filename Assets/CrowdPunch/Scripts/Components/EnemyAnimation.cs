using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum EnemyAnimationProfile : byte
    {
        Standard,
        Dasher,
        Explosive,
        Ranged,
        Elite,
        Baseline,
        Armored
    }

    public struct EnemyAnimation : IComponentData
    {
        public Entity Owner;
        public BlobAssetReference<EnemyAnimationSamples> Samples;
        public byte Profile;
        public float BlendResponse;
    }

    public struct EnemyAnimationPlayback : IComponentData
    {
        public uint ArmorHitSequence;
        public float Phase;
        public float2 Movement;
        public byte Initialized;
        public byte WasLaunched;
        public uint LaunchSequence;
        public float FlightPhase;
        public float FlightPitch;
        public byte Landing;
        public float ImpactPhase;
        public float ImpactDuration;
        public float AttackPhase;
        public byte WasAttacking;
        public float HitPhase;
        public float PreviousHealth;
        public byte HitActive;
        public byte HealthInitialized;
    }

    public struct EnemyAnimationSamples
    {
        public const int MotionCount = 11;
        public const int FlyingMotion = 9;
        public const int ImpactMotion = 10;
        public int BoneCount;
        public int FrameCount;
        // Idle, eight clockwise locomotion directions, Flying, Falling Flat Impact.
        public BlobArray<float> Durations;
        public BlobArray<float3x4> Matrices;
    }
}
