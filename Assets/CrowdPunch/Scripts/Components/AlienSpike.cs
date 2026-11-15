using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowdPunch.Components
{
    public enum AlienSpikeVolleyMode : byte { TimerWhileMoving, MidRangePositioning }
    public enum AlienSpikeExploderMode : byte { DetonateOnHit, OrdinaryDamageOnly }
    public enum AlienSpikeAimMode : byte { ChickenStyle, DirectContactOnly }
    public enum AlienSpikeVolleyPhase : byte { Moving, Telegraph, Release, FollowThrough }

    public struct AlienSpikeTuning : IComponentData
    {
        public AlienSpikeVolleyMode VolleyMode;
        public float Cooldown, TelegraphSeconds, EngagementRadius, PreferredDistance;
        public float FollowThroughSeconds;
        public AlienSpikeShotTuning Shot;
        public BlobAssetReference<AlienSpikeColorGradient> ProjectileColor;
    }

    public struct AlienSpikeShotTuning
    {
        public AlienSpikeExploderMode ExploderMode;
        public AlienSpikeAimMode AimMode;
        public float Speed, Radius, Lifetime, NormalDamage, BurstDamage, PlayerProtection;
    }

    public struct AlienSpikeVolley : IComponentData
    {
        public AlienSpikeVolleyPhase Phase;
        public float Remaining, AnimationPhase;
    }

    [InternalBufferCapacity(0)]
    public struct AlienSpikeSocket : IBufferElementData
    {
        public float3 IdleTip, ReleaseTip;
        public float2 IdleDirection, ReleaseDirection;
    }

    [InternalBufferCapacity(0)]
    public struct AlienSpikeBurst : IBufferElementData
    {
        public LocalTransform Pose;
        public Entity Source;
        public uint LaunchSequence, PunchSequence, SourceLifetime;
        public int ChainDepth;
    }

    [InternalBufferCapacity(0)]
    public struct AlienSpikeBurstHistory : IBufferElementData
    {
        public Entity Source;
        public uint LaunchSequence, PunchSequence, SourceLifetime;
    }

    public struct AlienSpikeProjectile : IComponentData
    {
        public AlienSpikeShotTuning Tuning;
        public BlobAssetReference<AlienSpikeColorGradient> ColorGradient;
        public Entity Emitter, Source, HomingTarget;
        public EnemyWaveOwnership Wave;
        public float3 Velocity;
        public float Remaining, HomingRemaining;
        public uint LaunchSequence, PunchSequence, SourceLifetime, VisualSequence;
        public int ChainDepth;
        public byte PlayerOwned, Redirected;
    }

    public struct AlienSpikeColorGradient
    {
        public UnityEngine.Hash128 Hash;
        public BlobArray<uint> Colors;
    }
}

