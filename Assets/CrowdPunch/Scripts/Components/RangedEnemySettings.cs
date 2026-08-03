using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>Provisional distance-keeping and attack tuning for a ranged enemy.</summary>
    public struct RangedEnemySettings : IComponentData
    {
        public Entity ProjectilePrefab;
        public float PreferredMinimumDistance;
        public float PreferredMaximumDistance;
        public float EngagementRange;
        public float RetreatSpeed;
        public float ApproachSpeed;
        public float InitialAttackDelay;
        public float InitialDelayVariation;
        public float WindUpDuration;
        public float Cooldown;
        public float ProjectileDamage;
        public float PlayerInvincibilitySeconds;
        public float ProjectileTravelDuration;
        public float ProjectileArcHeight;
        public float ProjectileLifetime;
        public float ProjectileRadius;
        public uint ProjectilePlayerLayers;
    }
}
