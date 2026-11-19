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
        public float FlightLimit, SettlePeriod, RecoveryDuration, SnagBraking;
    }

    public struct LinkedPairEndpoint : IComponentData { public Entity Pair; }

    public struct LinkedPairFlight
    {
        public uint Sequence;
        public float Remaining;
    }

    // The record tracks each endpoint's flight and the tether's shared hit clocks.
    public struct LinkedPair : IComponentData
    {
        public Entity A, B, Leader, Sequence;
        public uint RunGeneration, LifetimeA, LifetimeB;
        public float3 PreviousA, PreviousB;
        public LinkedPairFlight FlightA, FlightB;
        public EnemyLaunchPhase Phase;
        public byte HasPrevious;
    }

    public struct LinkedPairClock : IBufferElementData
    {
        public Entity Victim;
        public uint Lifetime;
        public double NextHitAt;
    }

    public struct LinkedPairPlayerHit : IBufferElementData
    {
        public float Damage;
        public float3 Position;
    }

    public struct LinkedPairEnemyShock : IComponentData
    {
        public float3 Position;
        public byte Pending;
    }
}
