using Unity.Entities;

namespace CrowdPunch.Components
{
    public enum EnemyArchetypeKind : byte
    {
        Baseline,
        Ranged,
        Explosive
    }

    /// <summary>Explicit spawn selection used without relying on prefab or presentation names.</summary>
    public struct EnemyArchetype : IComponentData
    {
        public EnemyArchetypeKind Value;
    }
}
