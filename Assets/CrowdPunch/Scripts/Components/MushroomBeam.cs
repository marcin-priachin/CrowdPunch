using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct MushroomBeam : IComponentData
    {
        public Entity Arena, Emitter;
        public float3 Origin, Direction;
        public float Radius, Range, Length;
        public float Damage, PlayerInvincibility;
        public double ExpiresAt;
        public uint PlayerLayers, PunchSequence;
        public int ChainDepth;
        public EnemyLaunchOwner Owner;
        public bool HitPlayer;
    }

    [InternalBufferCapacity(1)]
    public struct MushroomBeamImpact : IBufferElementData
    {
        public float3 Position, Direction;
        public uint PunchSequence;
        public int ChainDepth;
    }
}
