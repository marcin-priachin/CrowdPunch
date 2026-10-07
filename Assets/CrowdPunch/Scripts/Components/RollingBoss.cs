using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum RollingPhase : byte { Pause, WindUp, Roll, Defeated }
    public enum RollingAim : byte { Reflect, ReaimOnCollision }
    public enum RollingEnd : byte { Duration, BounceCount }
    public enum RollingBodyOwnership : byte { PlayerOnly, Any }
    public enum RollingContactDanger : byte { RollingOnly, AllLivingStates }
    public enum RollingTargeting : byte { EveryLivingState, VulnerableStates }
    public enum RollingCrowdDirection : byte { RollDirection, Outward }

    // ROLL-001..006: health stage and committed cycle stage are intentionally independent.
    public struct RollingBoss : IComponentData
    {
        public RollingPhase Phase;
        public int Stage, CycleStage, Bounces;
        public float Remaining, Speed;
        public float3 Direction, PreviousPosition;
        public double InvulnerableUntil, NextPlayerContact;
        public uint RollSequence, AcceptedHits;
    }

    public struct RollingTuning : IComponentData
    {
        public float Health, StageTwoThreshold, StageThreeThreshold;
        public float OpeningPause, WindUp, RollDuration, MaximumRollDuration;
        public float3 PauseDurations, RollSpeeds;
        public int BounceLimit;
        public float Invulnerability, BodyRadius, BodyHeight, Mass;
        public float PlayerDamage, PlayerProtection, PlayerPush, ContactInterval;
        public float CrowdDamage, LaunchSpeed, ResistantPush, BodyDamageMultiplier;
        public float3 PauseColor, WindUpColor, RollColor, ProtectionColor;
        public float ProtectionPulse, VisualRollDegreesPerSecond;
        public RollingAim Aim;
        public RollingEnd End;
        public RollingBodyOwnership Ownership;
        public RollingContactDanger ContactDanger;
        public RollingTargeting Targeting;
        public RollingCrowdDirection CrowdDirection;
        public float3 InitialPosition;
        public quaternion InitialRotation;
    }

    public struct RollingHit : IBufferElementData
    {
        public Entity Source;
        public uint Launch, Lifetime;
        public float Damage;
    }
    public struct RollingHitHistory : IBufferElementData
    {
        public Entity Source;
        public uint Launch, Lifetime;
    }
    public struct RollingCrowdContact : IBufferElementData
    {
        public Entity Body;
        public uint Lifetime, Roll;
        public double LastSeen;
    }
    public struct RollingVisualOwner : IComponentData { public Entity Value; }
    public struct RollingAnimationPivot : IComponentData { public float3 Center; public float Angle; }
}
