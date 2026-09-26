using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace CrowdPunch.Components
{
    [System.Flags]
    public enum BarricadeLaunchSources : byte
    {
        None = 0, Player = 1, Enemy = 2, Boss = 4, Unowned = 8, All = 15
    }

    public struct Barricade : IComponentData
    {
        public int HitsRemaining, RequiredHits;
        public float ReboundMultiplier, ReplenishDelay, FlashDuration, DebrisDuration;
        public BarricadeLaunchSources Sources;
        public float3 Size, ExitPosition;
        public float ExitRadius;
        public double LastHitTime;
        public uint HitSequence;
        public float3 LastHitPosition;
        public BlobAssetReference<Collider> IntactCollider, BrokenCollider;
    }

    public struct BarricadeHitHistory : IBufferElementData
    {
        public Entity Source;
        public uint LaunchSequence;
    }

    public struct BarricadeCrowdSequence : IComponentData { public Entity Barricade; }
    public struct BarricadeCrowdMember : IComponentData { public Entity Barricade; }

    public struct BarricadeRebound : IBufferElementData
    {
        public Entity Source;
        public uint LaunchSequence;
        public float3 ContactCenter, Normal, IncomingVelocity;
    }

    public struct BarricadeVisual : IComponentData
    {
        public Entity Barricade;
        public float4 Color;
        public float3 Position, DebrisDirection;
        public float Scale;
        public byte CrackStage;
    }
}
