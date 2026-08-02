using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Records the source launches that have already damaged this enemy.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct CollisionDamageHistory : IBufferElementData
    {
        public Entity Source;
        public uint SourceLaunchSequence;
    }
}
