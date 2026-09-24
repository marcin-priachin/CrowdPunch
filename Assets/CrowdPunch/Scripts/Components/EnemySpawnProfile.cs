using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>Reusable baked enemy profile shared by random and authored initial spawning.</summary>
    public struct EnemySpawnProfile
    {
        public Entity EnemyPrefab;
        public EnemyArchetypeKind Archetype;
        public byte RespawnEnabled;
        public float SpawnClearance;
        public float NavigationRadius;
        public EnemyMovementSettings MovementSettings;
        public EnemyArchetypeSeparationSettings ArchetypeSeparationSettings;
        public EnemyArmorSettings ArmorSettings;
        public Health Health;
        public EnemyContactDamageSettings ContactDamageSettings;
        public RangedEnemySettings RangedSettings;
        public ExplosiveEnemySettings ExplosiveSettings;
        public DasherSettings DasherSettings;
        public ElitePunchSettings ElitePunchSettings;
    }
}
