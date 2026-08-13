using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Components
{
    public enum DasherAvoidancePolicy : byte { None, BetweenDasherAndPlayer, BetweenAndBehindPlayer }
    public enum DasherPreparationMovement : byte { ImmediateStop, BrakeToStop }
    public enum DasherPhase : byte { Positioning, Preparing, Dashing, Recovering }
    public enum KnockbackResponseTier : byte { Normal, PlayerElite, Boss }
    public struct KnockbackResponse : IComponentData { public KnockbackResponseTier Tier; }

    public struct DasherSettings : IComponentData
    {
        public float PreferredMinimumDistance, PreferredMaximumDistance;
        public float PreparationMinimumDistance, PreparationMaximumDistance;
        public float ApproachSpeed, RetreatSpeed;
        public DasherPreparationMovement PreparationMovement;
        public float TelegraphDuration, DashSpeed, MaximumDistance, RecoveryDuration;
        public DasherAvoidancePolicy AvoidancePolicy;
        public float CorridorWidth, BehindPlayerDistance;
        public float PlayerDamage, PlayerKnockback, PlayerInvincibilitySeconds;
        public float LaunchedEnemyDamage, LaunchedEnemyKnockback;
        public float LaunchedImpactPositionWeight;
        public float EliteDamage, EliteKnockback, BossDamage, BossKnockback;
        public byte PreserveMomentumAgainstElites, PreserveMomentumAgainstBosses;
    }

    public struct DasherState : IComponentData
    {
        public DasherPhase Phase;
        public float SecondsRemaining;
        public float3 LockedDirection;
        public float3 DashStartPosition;
        public uint DashSequence;
        public byte HasHitPlayer;
        public float3 PreservedLaunchedVelocity;
        public float3 PreservedLaunchedAngularVelocity;
        public float3 PreviousPosition;
        public quaternion LockedRotation;
        public byte HasLockedRotation;
        public uint NormalizedLaunchSequence;
    }

    public struct DasherColliderState : IComponentData
    {
        public CollisionFilter SolidFilter;
        public byte IsInitialized;
        public byte IsIgnoringEnemies;
    }

    [InternalBufferCapacity(8)]
    public struct DasherHitHistory : IBufferElementData
    {
        public Entity Target;
        public uint Sequence;
        public byte ActionKind;
    }
}
