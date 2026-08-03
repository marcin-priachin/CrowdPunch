using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Enemy spawning configuration baked from a GameObject spawner.
    /// </summary>
    public struct SpawnSettings : IComponentData
    {
        public Entity EnemyPrefab;
        public Entity RangedProjectilePrefab;
        public EnemyArchetypeKind Archetype;
        public int InitialCount;
        public float SpawnRadius;
        public float3 Center;
        public RangedEnemySettings RangedSettings;
    }
}
