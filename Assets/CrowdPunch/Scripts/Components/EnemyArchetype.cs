using Unity.Entities;

namespace CrowdPunch.Components
{
    public enum EnemyArchetypeKind : byte
    {
        Baseline,
        Ranged,
        Explosive,
        Dasher,
        Elite,
        Armored = 5,
        Wizard = 6
    }

    /// <summary>Explicit spawn selection used without relying on prefab or presentation names.</summary>
    public struct EnemyArchetype : IComponentData
    {
        public EnemyArchetypeKind Value;
    }

    public enum EnemyCombatTier : byte
    {
        Normal,
        Elite,
        Boss
    }

    /// <summary>Runtime combat identity; never inferred from prefab or presentation names.</summary>
    public struct EnemyTier : IComponentData
    {
        public EnemyCombatTier Value;
    }
}
