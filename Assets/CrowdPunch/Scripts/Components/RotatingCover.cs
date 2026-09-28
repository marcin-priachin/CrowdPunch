using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum CoverRotationMode : byte { Continuous, RotateAndPause, ReversePeriodically }
    public enum CoverHitResponse : byte { None, Pause, Accelerate }

    public struct RotatingCover : IComponentData
    {
        public Entity Target;
        public CoverRotationMode Mode;
        public CoverHitResponse HitResponse;
        public float Radius, Height, Thickness, OpeningRadians, InitialAngle;
        public float RadiansPerSecond, RotateSeconds, PauseSeconds, ReverseSeconds;
        public float HitPauseSeconds, SpeedIncreasePerHit, ReflectionMultiplier;
    }

    public struct RotatingCoverState : IComponentData
    {
        public float Angle, CycleTime, HitPauseRemaining;
        public uint ObservedHit;
    }

    public struct CoverEnclosure : IComponentData { }
    public struct CoverPanel : IComponentData { public Entity Cover; public int Index; }
    public struct CoverReflection : IBufferElementData
    {
        public Entity Source;
        public uint LaunchSequence;
        public float3 ContactCenter, Normal, IncomingVelocity, PlayerPosition;
    }
}
