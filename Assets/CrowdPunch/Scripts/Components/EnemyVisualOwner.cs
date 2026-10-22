using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>Connects a baked renderer (including elite child meshes) to its ECS gameplay state.</summary>
    public struct EnemyVisualOwner : IComponentData
    {
        public Entity Value;
        // Textured creatures retain their atlas details; primitive fallback meshes use role tints.
        public byte UsesAtlas;
        public byte AnimationDriven;
    }
}
