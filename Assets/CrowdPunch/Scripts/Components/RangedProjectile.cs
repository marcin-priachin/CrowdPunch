using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    /// <summary>Fixed fire-time trajectory and one-shot hit state for a ranged projectile.</summary>
    public struct RangedProjectile : IComponentData
    {
        public float3 Start;
        public float3 Target;
        public float TravelDuration;
        public float ElapsedSeconds;
        public float ArcHeight;
        public float MinimumAltitude;
        public float Lifetime;
        public float Radius;
        public float Damage;
        public float PlayerInvincibilitySeconds;
        public uint PlayerCollisionLayers;
        public byte HasAppliedDamage;
    }
}
