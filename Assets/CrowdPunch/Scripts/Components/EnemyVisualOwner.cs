using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>Connects a baked renderer (including elite child meshes) to its ECS gameplay state.</summary>
    public struct EnemyVisualOwner : IComponentData
    {
        public Entity Value;
    }
}
