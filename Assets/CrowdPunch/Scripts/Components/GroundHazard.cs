using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum GroundHazardShape : byte { Rectangle, Circle }
    public enum GroundHazardOperation : byte { AlwaysActive, Periodic }
    public enum GroundHazardPhase : byte { Inactive, Warning, Active }
    public enum GroundHazardAvoidance : byte { ActiveOnly, WarningAndActive }
    public enum GroundHazardFallback : byte { WaitSafely, CrossAsLastResort }

    public struct GroundHazard : IComponentData
    {
        public Entity Sequence;
        public float3 Position;
        public float2 HalfSize;
        public float Angle, Radius, Damage, DamageInterval;
        public float InactiveDuration, WarningDuration, ActiveDuration, CycleOffset;
        public int FirstWave;
        public GroundHazardShape Shape;
        public GroundHazardOperation Operation;
    }
    public struct GroundHazardState : IComponentData
    {
        public double WaveStartedAt;
        public uint RunGeneration;
        public int WaveIndex;
        public byte Initialized, Introduced;
        public GroundHazardPhase Phase;
    }
    public struct GroundHazardPolicy : IComponentData
    {
        public GroundHazardAvoidance Avoidance;
        public GroundHazardFallback Fallback;
    }
    // One clock on each victim, shared by every patch; pooling changes Lifetime.
    public struct GroundHazardDamageClock : IComponentData
    {
        public double NextHitAt;
        public uint Lifetime;
    }
    public struct GroundHazardPlayerClock : IComponentData
    {
        public Entity Sequence;
        public uint RunGeneration;
        public double NextHitAt;
    }
    public struct GroundHazardPlayerHit : IBufferElementData { public float Damage; }
}
