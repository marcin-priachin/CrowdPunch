using System;
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
        [Header("Ownership Colors")]
        [Tooltip("Accent for player-owned launches and effects triggered by the player.")]
        [ColorUsage(false)] public Color PlayerOwnershipColor = DefaultPlayerOwnershipColor;
        [Tooltip("Shared accent for enemy-owned and boss-owned launches and effects.")]
        [ColorUsage(false)] public Color EnemyOwnershipColor = DefaultEnemyOwnershipColor;

        [Header("Batched hazard surfaces")]
        public Material SporeCloud, Pollen, DragonFlame, Shockwave;

        [Header("Wizard Spore Emission")]
        [Range(4, 32)] public int CloudsPerZone = 12;
        [Tooltip("Pollen count for a Wizard zone with a 6 m radius. Other radii scale by area.")]
        [Range(8, 64)] public int SporesPerZone = 24;
        [Tooltip("Furthest pollen can spread, as a fraction of the zone radius.")]
        [Range(0f, 1f)] public float SporeSpreadFraction = .96f;
        [Tooltip("Lifetime in seconds for a spore emitted from Bud's crown.")]
        [Min(.05f)] public float SporeFlightSeconds = 1.35f;
        [Tooltip("Fraction of the spore lifetime at which it reaches its full spread.")]
        [Range(.01f, 1f)] public float SporeSpreadPhase = .62f;
        [Min(1)] public int MaximumCloudZones = 32;

        [Header("Wizard Ground Indicator")]
        public WizardIndicatorStyle WizardWarningIndicator = WizardIndicatorStyle.Warning;
        public WizardIndicatorStyle WizardActiveIndicator = WizardIndicatorStyle.Active;
        [Tooltip("Width measured inward from the attack boundary, as a fraction of the zone radius. Zero hides the ring.")]
        [Range(0f, 1f)] public float WizardRingThickness = DefaultWizardRingThickness;

        internal static Color DefaultPlayerOwnershipColor => new(1f, .55f, .16f);
        internal static Color DefaultEnemyOwnershipColor => new(.78f, .35f, 1f);

        internal const float DefaultWizardRingThickness = .06f;
    }

    [Serializable]
    public struct WizardIndicatorStyle
    {
        [Tooltip("Ground disc tint. Alpha controls the fill opacity.")]
        public Color FillColor;
        [ColorUsage(false)] public Color RingColor;
        [Range(0f, 1f)] public float RingOpacity;

        internal static WizardIndicatorStyle Warning => new WizardIndicatorStyle
        {
            FillColor = new Color(.64f, .76f, .24f, .16f),
            RingColor = new Color(.64f, .76f, .24f),
            RingOpacity = .325f
        };

        internal static WizardIndicatorStyle Active => new WizardIndicatorStyle
        {
            FillColor = new Color(.58f, .7f, .12f, .32f),
            RingColor = new Color(.58f, .7f, .12f),
            RingOpacity = .5525f
        };
    }
}
