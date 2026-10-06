using Unity.Entities;

namespace CrowdPunch.Components
{
    public struct TrailSource : IComponentData
    {
        public Entity Enemy, SceneOwner, Sequence;
        public uint Lifetime, RunGeneration;
        public int WaveIndex;
        public double ExpiresAt;
    }

    [InternalBufferCapacity(0)]
    public struct TrailDamageTarget : IBufferElementData
    {
        public Entity Target;
        public uint Lifetime;
        public double NextHitAt;
    }
}
