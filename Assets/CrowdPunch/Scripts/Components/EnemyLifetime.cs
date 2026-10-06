using Unity.Entities;

namespace CrowdPunch.Components
{
    // Entity version distinguishes destruction; this distinguishes reuse of the same pooled entity.
    public struct EnemyLifetime : IComponentData { public uint Generation; }
}
