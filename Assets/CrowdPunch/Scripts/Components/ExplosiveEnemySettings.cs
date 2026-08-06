using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>Per-archetype explosion tuning. Knockback tiers remain explicit for future target kinds.</summary>
    public struct ExplosiveEnemySettings : IComponentData
    {
        public float Radius;
        public float Damage;
        public float NormalEnemyKnockbackForce;
        public float PlayerEliteKnockbackForce;
        public float BossKnockbackForce;
        public float PlayerInvincibilitySeconds;
        public float VisualDuration;
        public float VisualSizeMultiplier;
    }
}
