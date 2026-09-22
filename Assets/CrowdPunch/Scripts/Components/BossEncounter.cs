using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum BossCycle : byte { Opening, Attacking, Relocating, Transition, Defeated }

    // The head owns encounter health and coordination. No Enemy or EnemyLaunchState is added.
    public struct BossEncounter : IComponentData
    {
        public Entity LeftHand, RightHand;
        public int Stage, AttackIndex, NextHand;
        public BossCycle Cycle;
        public float Remaining, RouteDistance, TravelRemaining;
        public double InvulnerableUntil;
        public uint TransitionCount, AcceptedHits;
        public byte SecondAttackPending;
    }

    public struct BossTuning : IComponentData
    {
        public float Health, Invulnerability, StageTwoThreshold, StageThreeThreshold, TransitionDuration;
        public float2 Center, RouteExtents, BoundsExtents;
        public float RouteCornerRadius, MoveSpeed, MoveDistance, HeadHeight, HandHeight;
        public float HandSpeed, HandAcceleration, OpenOffset, ShieldOffset, ShieldForward;
        public float OpeningDuration, StageThreeTiming, CoordinationDelay;
        public float StaggerDuration, StaggerProtection, StaggerMinimumImpulse;
        public float PlayerDamage, PlayerInvulnerability, PlayerKnockback, ScatterDamage, ScatterSpeed;
        public BossAttackTuning Slam, Lunge, Sweep;
    }

    [System.Serializable]
    public struct BossAttackTuning
    {
        public float Anticipation, Active, Recovery, Reach, Width;
    }
}
