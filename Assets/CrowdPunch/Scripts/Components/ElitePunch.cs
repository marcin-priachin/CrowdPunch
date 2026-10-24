using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum ElitePunchPhase : byte { InitialDelay, SelectingTarget, Repositioning, WindUp, Cooldown, CommittedSweep }
    public enum ElitePunchTactic : byte { ClearPath, CrowdShot }
    public enum ElitePunchInteractionMode : byte { SelectedTargetOnly, AllValidEnemiesInPunchVolume }

    public struct ElitePunchSettings : IComponentData
    {
        public float InitialDelay, Cooldown, CooldownVariation, MaximumSetupDuration, RetargetInterval;
        public float MaximumSearchRange, MinimumTargetPlayerDistance, MaximumTargetPlayerDistance;
        public byte AllowActiveTargets, AllowRecoveringTargets, AllowLaunchedTargets, AllowSharedTargets;
        public float ClearPathTacticProbability;
        public int MaximumEvaluatedCandidates;
        public float ClearPathAlignmentWeight, ClearPathRepositionWeight, ClearPathDistanceWeight;
        public float CrowdCorridorRadius, CrowdDistanceBeyondPlayer, CrowdNearPlayerWeight, MinimumCrowdScore;
        public float DesiredPunchDistance, PositionTolerance, AimAngleToleranceDegrees;
        public float PlayerMovementInvalidationDistance, TargetMovementInvalidationDistance, SetupMovementSpeedMultiplier;
        public byte ApplySeparationDuringSetup;
        public float PunchRange, PunchRadius, LaunchForce, PunchDamage, PushDirectionPositionWeight;
        public ElitePunchInteractionMode InteractionMode;
        public byte ProjectileReceivesDamage, AffectActive, AffectRecovering, AffectLaunched;
        public byte CanDirectlyHitPlayer;
        public float DirectPlayerDamage, PlayerPush, PlayerInvincibilityDuration;
        public float WindUpDuration;
        public float LaunchAnimationProgress, SweepFollowThroughDuration;
        public byte EnableTelegraph;
        public float TelegraphDuration;
    }

    public struct ElitePunchState : IComponentData
    {
        public ElitePunchPhase Phase;
        public ElitePunchTactic Tactic;
        public Entity Target;
        public float SecondsRemaining, SetupSeconds, RetargetSeconds;
        public float3 ValidatedPlayerPosition, ValidatedTargetPosition;
        public uint RandomState, AttackSequence;
        public byte TelegraphActive;
        public float SweepElapsed, SweepProgress;
        public float3 CommittedDirection;
        public byte SweepLaunched;
    }

    public struct ElitePunchReservation : IComponentData
    {
        public Entity Owner;
        public uint OwnerAttackSequence;
        public byte IsStaged;
    }
}
