using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct ChickenProjectile : IComponentData
    {
        public Entity Boss, HomingTarget;
        public float3 Velocity;
        public float Age;
        public int Bounces;
        public uint Launch;
        public byte Redirected;
    }
    public struct ChickenProjectileHit : IBufferElementData { public Entity Target; public uint Lifetime; }
}
