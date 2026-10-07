using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum ChickenPhase : byte { Pause, WindUp, Rush, Stagger, Defeated }
    public enum ChickenPattern : byte { Single, Paired }
    public enum ChickenPauseResponse : byte { InterruptAndFlee, FinishPause }
    public enum ChickenShotAim : byte { PlayerPosition, MovementLead }

    // CHICKEN-001..006: encounter state stays separate from ordinary Enemy lifecycle.
    public struct ChickenBoss : IComponentData
    {
        public ChickenPhase Phase;
        public int Stage, ShotsRemaining;
        public float Remaining, SinceShot;
        public float3 Destination, PreviousPosition;
        public quaternion Facing;
        public double InvulnerableUntil;
        public uint RushSequence, AcceptedHits;
        public byte RushHitPlayer, AlternateSide;
    }

    public struct ChickenTuning : IComponentData
    {
        public Entity ProjectilePrefab;
        public float Health, StageTwoThreshold, StageThreeThreshold;
        public ChickenPattern StageOnePattern, StageTwoPattern, StageThreePattern;
        public ChickenPauseResponse PauseResponse;
        public ChickenShotAim ShotAim;
        public float OpeningPause, WindUp, ShotSpacing, Pause, FinalPauseMultiplier;
        public float RushSpeed, FinalRushMultiplier, RushDistance, SidewaysWeight, ProximityDistance;
        public float Stagger, Invulnerability, BodyRadius, BodyHeight;
        public float PlayerDamage, PlayerProtection, PlayerPush;
        public float ProjectileRadius, ProjectileHeight, FireSpeed, ReturnSpeed, Lifetime;
        public int BounceLimit;
        public float ProjectileDamage, ProjectileLaunchSpeed, ReturnedBossDamage, BodyDamageMultiplier;
        public float3 InitialPosition;
        public quaternion InitialRotation;
        public float2 ArenaCenter, ArenaHalfSize;
    }

    // Collect contacts and blasts before resolving their maximum once in the same physics step.
    public struct ChickenHit : IBufferElementData
    {
        public Entity Source;
        public uint Launch, Lifetime;
        public float Damage;
    }
    public struct ChickenHitHistory : IBufferElementData
    {
        public Entity Source;
        public uint Launch, Lifetime;
    }
    public struct ChickenVisualOwner : IComponentData { public Entity Value; }
}
