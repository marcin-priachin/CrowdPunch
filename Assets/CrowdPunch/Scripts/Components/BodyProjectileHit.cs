using Unity.Entities;

namespace CrowdPunch.Components
{
    [InternalBufferCapacity(4)]
    public struct BodyProjectileHit : IBufferElementData
    {
        public Entity Enemy;
        public uint Lifetime;
    }
}
