using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct VillageGatePiece : IComponentData
    {
        public Entity Gate;
        public float3 Position, Velocity, Spin;
        public quaternion Rotation;
        public float Scale, Duration, RestingHeight, Seed;
        public byte Kind, DamageStage, Initialized, Settled;
        public uint LastSequence;
    }
}
