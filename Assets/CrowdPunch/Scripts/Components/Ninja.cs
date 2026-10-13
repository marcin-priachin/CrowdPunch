using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum NinjaTriggerMode : byte { Periodic = 1, Proximity = 2, Both = 3 }
    public enum NinjaPhase : byte { Positioning, Preparing, TeleportPending, ArrivalWarning, Dashing, Recovering }

    public struct NinjaTuning : IComponentData
    {
        public NinjaTriggerMode TriggerMode;
        public float InitialDelay, MinimumCooldown, PeriodicInterval, PeriodicVariation;
        public float ProximityDistance, EngagementDistance, PreferredMinimumDistance, PreferredMaximumDistance;
        public float RetreatSpeed, ApproachSpeed, PreparationDuration, TeleportDistance, RearArcDegrees;
        public int RearSearchSteps, MaximumConcurrentAmbushes;
        public float ArrivalWarningDuration, DashSpeed, MaximumDashDistance, RecoveryDuration;
        public float PlayerDamage, PlayerKnockback, PlayerInvulnerability;
        public float SlotRetryMinimum, SlotRetryMaximum, DestinationRetryMinimum, DestinationRetryMaximum;
    }

    public struct NinjaState : IComponentData
    {
        public NinjaPhase Phase;
        public float SecondsRemaining, DistanceTravelled;
        public double ReadyAt, PeriodicAt, RetryAt;
        public float3 LockedDirection, PreviousPosition, DeparturePosition;
        public uint Generation, RandomState, TeleportSequence, ObservedLaunchSequence;
        public byte Initialized, WasWithinProximity, ProximityReady, WasInactive, HasHitPlayer;
        // Retained until post-physics combat, including the last truncated dash step.
        public byte HasDashSegment;
        public float3 SegmentStart, SegmentEnd;

        public readonly bool OccupiesAmbushSlot => Phase == NinjaPhase.Preparing
            || Phase == NinjaPhase.TeleportPending || Phase == NinjaPhase.ArrivalWarning || Phase == NinjaPhase.Dashing;
    }

    public struct NinjaPresentation : IComponentData { public uint TeleportSequence, Generation; }
}
