using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Combat Feedback")]
    public sealed class CombatFeedbackSettings : ScriptableObject
    {
        [Header("Time (real seconds)")]
        [Range(0f, .1f)] public float PunchHitStop = 0.055f;
        [Range(0f, .1f)] public float DamageHitStop = 0.065f;
        public bool ExceptionalSlowMotion = true;
        [Min(2)] public int ExceptionalChainDepth = 4;
        [Range(.05f, 1f)] public float SlowMotionScale = 0.22f;
        [Range(0f, .2f)] public float SlowMotionDuration = 0.095f;
        [Min(.1f)] public float ExceptionalCooldown = 1.5f;
        [Header("Impact significance")]
        [Min(0f)] public float MinimumCollisionSpeed = 2f;
        [Min(.1f)] public float FullCollisionSpeed = 16f;
        [Min(0f)] public float MinimumCollisionImpulse = 1.5f;
        [Min(0f)] public float EnvironmentMinimumSpeed = 4f;
        [Min(.01f)] public float ContactFeedbackInterval = .12f;
        [Range(.1f, 1f)] public float SingleImpactIntensity = .7f;
        [Min(0f)] public float ChainGainPerDepth = 0.18f;
        [Range(1f, 3f)] public float MaximumChainMultiplier = 1.8f;
        [Header("Camera (world metres)")]
        [Min(0f)] public float PunchKick = 0.22f;
        [Min(0f)] public float DamageKick = 0.28f;
        [Min(0f)] public float CollisionKick = 0.085f;
        [Min(0f)] public float ExceptionalKick = 0.22f;
        [Min(0f)] public float Shake = 0.075f;
        [Min(.01f)] public float CameraRecovery = 0.1f;
        [Min(0f)] public float MaximumCameraOffset = 0.34f;
        [Range(0f, 1f)] public float CollisionCameraThreshold = .55f;
        [Min(.01f)] public float CollisionCameraInterval = 0.22f;
        [Min(1f)] public float CameraImpactRange = 25f;
        [Header("Comfort")]
        [Tooltip("Scales all impact camera motion. Zero disables it; aiming rotation is never shaken.")]
        [Range(0f, 1f)] public float CameraMotionScale = .75f;
        [Tooltip("Disable body flashes and use saturated impact centres instead of pale flashes.")]
        public bool ReduceFlashes;
        [Header("Comic impact accents")]
        [Tooltip("Contact starbursts, expanding rings and tumbling stars. Zero disables this layer.")]
        [Range(0f, 2f)] public float ComicImpactScale = 1f;
        [Header("Particles (optional prefabs; built-in placeholders when empty)")]
        public ParticleSystem PunchParticles, EnemyParticles, PlayerDamageParticles, EnvironmentParticles;
        public ParticleSystem DashStartParticles, DashMovementParticles, DashEndParticles;
        public ParticleSystem DasherTelegraphParticles;
        public ParticleSystem ExplosionParticles;
        [Min(1)] public int ParticlePoolSizePerEffect = 8;
        [Min(1)] public int MaximumImpactsPerFrame = 8;
        public Vector2 ParticleScale = new Vector2(.7f, 1.45f);
        [Range(1, 24)] public int PlaceholderParticleCount = 10;
        [Min(.01f)] public float DashMovementInterval = 0.055f;
        [Header("Launched trails")]
        [Min(0)] public int MaximumTrails = 48;
        [Min(0f)] public float TrailMinimumSpeed = 4f;
        [Min(.1f)] public float TrailLengthInModels = 2.5f;
        [Min(.1f)] public float TrailWidthInModels = 1f;
        public Color TrailColor = new Color(.97f, .99f, 1f, 1f);
        [Header("Launch speed lines")]
        [Tooltip("0 is transparent; 1 is fully opaque. Multiplies Trail Color alpha.")]
        [Range(0f, 1f)] public float TrailLineOpacity = 1f;
        [Range(1, 12)] public int TrailLineCount = 5;
        [Tooltip("Individual stroke thickness as a fraction of the full model-width trail.")]
        [Range(.001f, .2f)] public float TrailLineWidth = .0424f;
        [Tooltip("0 gives crisp edges; 1 gives a fully blurred Gaussian stroke.")]
        [Range(0f, 1f)] public float TrailLineBlur = 1f;
        [Tooltip("Span between the outer line centres, as a fraction of full trail width.")]
        [Range(0f, 1f)] public float TrailLineSpacing = .92f;
        [Range(0f, 2f)] public float TrailLineBrightness = 1f;
        [Header("Speed line profiles (0 = oldest tail, 1 = enemy)")]
        [Tooltip("Opacity multiplier along the trail. A flat value of 1 preserves uniform opacity.")]
        public AnimationCurve TrailOpacityAlongLength = AnimationCurve.Linear(0, 1, 1, 1);
        [Tooltip("Stroke thickness multiplier along the trail. Does not change the model-width envelope.")]
        public AnimationCurve TrailLineWidthAlongLength = AnimationCurve.Linear(0, 1, 1, 1);
        [Tooltip("Blur multiplier along the trail.")]
        public AnimationCurve TrailLineBlurAlongLength = AnimationCurve.Linear(0, 1, 1, 1);
        [Tooltip("Line spacing multiplier along the trail.")]
        public AnimationCurve TrailLineSpacingAlongLength = AnimationCurve.Linear(0, 1, 1, 1);
        [Tooltip("Colour and alpha multiplier along the trail. White leaves Trail Color unchanged.")]
        public Gradient TrailTintAlongLength = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            alphaKeys = new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) }
        };
        private int trailStyleVersion;
        internal int TrailStyleVersion => trailStyleVersion;
        private void OnValidate() => RefreshTrailStyle();
        /// <summary>Refresh GPU profiles after changing trail curves or line controls from code.</summary>
        public void RefreshTrailStyle() { unchecked { trailStyleVersion++; } }
        [Header("Visual-only enemy deformation / flash")]
        [Range(0f, .3f)] public float SquashAmount = 0.27f;
        [Range(.02f, .3f)] public float SquashDuration = 0.16f;
        [Range(0f, 1f)] public float DeformationThreshold = .12f;
        [Range(.01f, .15f)] public float FlashDuration = 0.065f;
        [Range(0f, 1f)] public float EnemyFlashIntensity = 0.9f;
        [Range(0f, 1f)] public float PlayerFlashIntensity = 0.9f;
        public Color PlayerFlashColor = new Color(1f, .48f, .22f);
        [Header("Dash camera")]
        [Tooltip("Keep at zero for stable hard shadows: changing FOV resizes URP's directional shadow projection.")]
        [Range(0f, 10f)] public float DashFovDelta = 0f;
        [Min(.01f)] public float DashFovTransition = 0.075f;

        public float Intensity(float speed) => Mathf.InverseLerp(MinimumCollisionSpeed,
            Mathf.Max(MinimumCollisionSpeed + .01f, FullCollisionSpeed), speed);
        public float ChainMultiplier(int depth) => Mathf.Min(MaximumChainMultiplier,
            1f + Mathf.Max(0, depth - 1) * ChainGainPerDepth);
    }
}
