using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    // Reuses the equipment skin and render entities; never participates in gameplay physics.
    public struct EnemyArmorDebris : IComponentData
    {
        public Entity Root;
        public AABB MeshBounds;
        public float4x4 InitialPose;
        public float4x4 WorldPose;
        public float3 Velocity;
        public float Age;
        public uint Generation;
        public byte Armed;
        public byte Detached;
    }
}
