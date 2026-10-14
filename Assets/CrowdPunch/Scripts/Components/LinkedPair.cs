using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum LinkedPairResponse : byte { DamageAndLaunch, DamageAndPush }

    public struct LinkedPairTuning : IComponentData
    {
        public LinkedPairResponse Response;
        public float MaximumLength, Tension, RadialDamping, MaximumTensionAcceleration;
        public float MoveSpeed, StraddleDistance, SweepSpeed, Width;
        public float PlayerDamage, EnemyDamage, TickInterval, LaunchSpeed, PushSpeed;
        public float FlightLimit, SettleSpeed, SettlePeriod, RecoveryDuration, SnagBraking;
    }

    public struct LinkedPairEndpoint : IComponentData { public Entity Pair; }

    // One record owns both endpoints, flight timing and link clocks. Endpoint health stays independent.
    public struct LinkedPair : IComponentData
    {
        public Entity A, B, Leader, Sequence;
        public uint RunGeneration, LifetimeA, LifetimeB;
        public float3 PreviousA, PreviousB;
        public float FlightRemaining, SettledFor, RecoveryRemaining;
        public EnemyLaunchPhase Phase;
        public byte HasPrevious;
        public double LastPunchAt;
        public Entity LastBlast;
        public uint LastBlastSequence;
        public uint LastBlastLifetime;
    }

    public struct LinkedPairClock : IBufferElementData
    {
        public Entity Victim;
        public uint Lifetime;
        public double NextHitAt;
    }

    public struct LinkedPairPlayerHit : IBufferElementData { public float Damage; }
}
