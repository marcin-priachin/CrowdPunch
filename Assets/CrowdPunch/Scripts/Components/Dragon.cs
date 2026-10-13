using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public enum DragonEnemyFlameMode : byte { PlayerLaunches, AnyLaunch, BurstsAndLaunches }
    public enum DragonImmunityMode : byte { OwnFlame, AllDragonFlame }
    public enum DragonAvoidanceMode : byte { DamagingFlame, None }
    public enum DragonFlamePhase : byte { Cooldown, Warning, Active }

    [System.Serializable]
    public struct DragonFlameProfile
    {
        public float Length, AngleDegrees, PlayerDamage, EnemyDamage, PlayerPush, EnemyPush;
    }

    public struct DragonTuning : IComponentData
    {
        public DragonEnemyFlameMode EnemyMode;
        public DragonImmunityMode Immunity;
        public DragonAvoidanceMode Avoidance;
        public float WarningDuration, ActiveDuration, CooldownDuration, InitialDelay, InitialVariation;
        public float EngagementRange, PreferredMinimum, PreferredMaximum, ApproachSpeed, RetreatSpeed, TurnDegreesPerSecond, HitInterval;
        public float3 MouthOffset;
        public float GroundY, IdleDuration, MoveDuration;
        public DragonFlameProfile Normal, Launched;
    }

    public struct DragonFlameState : IComponentData
    {
        public DragonFlamePhase Phase;
        public float Remaining, AnimationPhase;
        public float3 MouthOffset;
        public int Motion;
        public uint Generation, ObservedLaunchSequence;
        public byte Initialized, WasInactive;
    }

    [InternalBufferCapacity(0)]
    public struct DragonFlameClock : IBufferElementData
    {
        public Entity Target;
        public uint Generation;
        public double NextHitAt;
    }

    public struct DragonMouthFrame : IBufferElementData { public float3 Position; }

    public struct DragonPlayerHit : IBufferElementData
    {
        public float Damage;
        public float3 Push;
    }
}
