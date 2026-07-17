using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Enemy touch damage tuning used when an ECS enemy reaches the GameObject player.
    /// </summary>
    public struct EnemyContactDamageSettings : IComponentData
    {
        public float DamagePercent;
        public float PushStrength;
        public float PlayerInvincibilitySeconds;
        public float ContactRadius;
    }
}
