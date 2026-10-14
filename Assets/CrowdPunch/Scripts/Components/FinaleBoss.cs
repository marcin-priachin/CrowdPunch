using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum FinalePhase : byte { Flying, Falling, Downed, TakingOff, Defeated }
    public enum FinaleAttack : byte { None, Strike, Sweep, Slam, Shockwave, Landing }

    public struct FinaleBoss : IComponentData
    {
        public FinalePhase Phase;
        public FinaleAttack Attack;
        public float Remaining, AttackRemaining, Health, Flash;
        public float3 Landing, TransitionStart, WarningCenter, WarningDirection, TetherOrigin;
        public int Stage, SpentCount, AttackIndex, GroundAttackIndex;
        public uint AttackSequence;
        public byte Neutralized;
    }

    public struct FinaleTuning : IComponentData
    {
        public float Health, FlightHeight, FlightOffset, FlightSpeed, TurnSpeed, GroundHeight;
        public float FallSeconds, DownedSeconds, StunSeconds, TakeoffSeconds, VictorySeconds;
        public float StageTwo, StageThree, ReboundSafetyDegrees;
        public float3 AirIntervals, WarningSeconds;
        public float StrikeRadius, SweepLength, SweepWidth, SlamLength, SlamWidth, ShockwaveRadius, LandingRadius;
        public float PlayerDamage, EnemyDamage, PushSpeed, GroundInterval;
    }

    public struct FinaleAnchor : IComponentData
    {
        public Entity Boss;
        public float3 Landing;
        public byte Spent;
    }

    public struct FinaleHit : IBufferElementData
    {
        public Entity Source;
        public uint Sequence;
        public byte Explosion;
    }
}
