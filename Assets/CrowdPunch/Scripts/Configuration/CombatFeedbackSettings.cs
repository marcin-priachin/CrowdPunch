using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Combat Feedback")]
    public sealed class CombatFeedbackSettings : ScriptableObject
    {
        [Header("Time (real seconds)")]
        [Range(0f, .1f)] public float PunchHitStop = .028f;
        public bool ExceptionalSlowMotion = true;
        [Min(2)] public int ExceptionalChainDepth = 4;
        [Range(.05f, 1f)] public float SlowMotionScale = .3f;
        [Range(0f, .2f)] public float SlowMotionDuration = .08f;
        [Min(.1f)] public float ExceptionalCooldown = 1.5f;
        [Header("Impact significance")]
        [Min(0f)] public float MinimumCollisionSpeed = 2f;
        [Min(.1f)] public float FullCollisionSpeed = 16f;
        [Min(0f)] public float MinimumCollisionImpulse = 1.5f;
        [Min(0f)] public float EnvironmentMinimumSpeed = 4f;
        [Min(.01f)] public float ContactFeedbackInterval = .12f;
        [Range(.1f, 1f)] public float SingleImpactIntensity = .7f;
        [Min(0f)] public float ChainGainPerDepth = .15f;
        [Range(1f, 3f)] public float MaximumChainMultiplier = 1.65f;
        [Header("Camera (world metres)")]
        [Min(0f)] public float PunchKick = .075f;
        [Min(0f)] public float DamageKick = .15f;
        [Min(0f)] public float CollisionKick = .045f;
        [Min(0f)] public float ExceptionalKick = .11f;
        [Min(0f)] public float Shake = .025f;
        [Min(.01f)] public float CameraRecovery = .13f;
        [Min(0f)] public float MaximumCameraOffset = .22f;
        [Range(0f, 1f)] public float CollisionCameraThreshold = .55f;
        [Min(.01f)] public float CollisionCameraInterval = .18f;
        [Min(1f)] public float CameraImpactRange = 25f;
        [Header("Particles (optional prefabs; built-in placeholders when empty)")]
        public ParticleSystem PunchParticles, EnemyParticles, PlayerDamageParticles, EnvironmentParticles;
        public ParticleSystem DashStartParticles, DashMovementParticles, DashEndParticles;
        [Min(1)] public int ParticlePoolSizePerEffect = 8;
        [Min(1)] public int MaximumImpactsPerFrame = 12;
        public Vector2 ParticleScale = new Vector2(.55f, 1.1f);
        [Range(1, 24)] public int PlaceholderParticleCount = 7;
        [Min(.01f)] public float DashMovementInterval = .045f;
        [Header("Launched trails")]
        [Min(0)] public int MaximumTrails = 48;
        [Min(0f)] public float TrailMinimumSpeed = 3f;
        [Range(.02f, .3f)] public float TrailDuration = .11f;
        [Min(0f)] public float TrailWidth = .11f;
        public Color TrailColor = new Color(.75f, .85f, .85f, .32f);
        [Header("Visual-only enemy deformation / flash")]
        [Range(0f, .3f)] public float SquashAmount = .13f;
        [Range(.02f, .3f)] public float SquashDuration = .12f;
        [Range(0f, 1f)] public float DeformationThreshold = .12f;
        [Range(.01f, .15f)] public float FlashDuration = .045f;
        [Range(0f, 1f)] public float EnemyFlashIntensity = .6f;
        [Range(0f, 1f)] public float PlayerFlashIntensity = .7f;
        public Color PlayerFlashColor = new Color(1f, .65f, .5f);
        [Header("Dash camera")]
        [Range(0f, 10f)] public float DashFovDelta = 3f;
        [Min(.01f)] public float DashFovTransition = .1f;

        public float Intensity(float speed) => Mathf.InverseLerp(MinimumCollisionSpeed,
            Mathf.Max(MinimumCollisionSpeed + .01f, FullCollisionSpeed), speed);
        public float ChainMultiplier(int depth) => Mathf.Min(MaximumChainMultiplier,
            1f + Mathf.Max(0, depth - 1) * ChainGainPerDepth);
    }
}
