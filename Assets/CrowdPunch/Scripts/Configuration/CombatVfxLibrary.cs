using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Combat VFX Library")]
    public sealed class CombatVfxLibrary : ScriptableObject
    {
        [Header("Arcane projectiles and detached snake droplets")]
        public ParticleSystem RangedFlight, BossFlight, SnakeFlight;
        public ParticleSystem RangedCharge;
        public ParticleSystem RangedImpact, BossImpact, SnakeSever;
        [Min(1)] public int RangedCapacity = 32, BossCapacity = 16, SnakeCapacity = 16;
        [Min(1)] public int BurstCapacity = 12;
        [Header("Crowd accents")]
        public ParticleSystem LaunchDust, NinjaSmoke;
        public Material LaunchTrail, ArcaneTrail, BossTrail;
        [Header("Batched hazard surfaces")]
        public Material SporeCloud, Pollen, DragonFlame, Shockwave;
        [Range(4, 32)] public int CloudsPerZone = 12;
        [Range(8, 64)] public int SporesPerZone = 24;
        [Min(1)] public int MaximumCloudZones = 32;
    }
}
