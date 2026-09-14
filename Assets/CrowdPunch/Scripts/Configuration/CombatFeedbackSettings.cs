using UnityEngine;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(menuName = "Crowd Punch/Combat Feedback")]
    public sealed class CombatFeedbackSettings : ScriptableObject
    {
        [Header("Time (real seconds)")]
        [Range(0f, .1f)] public float PunchHitStop = 0.045f;
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
        [Min(0f)] public float PunchKick = 0.17f;
        [Min(0f)] public float DamageKick = 0.28f;
        [Min(0f)] public float CollisionKick = 0.085f;
        [Min(0f)] public float ExceptionalKick = 0.22f;
        [Min(0f)] public float Shake = 0.045f;
        [Min(.01f)] public float CameraRecovery = 0.1f;
        [Min(0f)] public float MaximumCameraOffset = 0.34f;
        [Range(0f, 1f)] public float CollisionCameraThreshold = .55f;
        [Min(.01f)] public float CollisionCameraInterval = 0.22f;
        [Min(1f)] public float CameraImpactRange = 25f;
        [Header("Particles (optional prefabs; built-in placeholders when empty)")]
        public ParticleSystem PunchParticles, EnemyParticles, PlayerDamageParticles, EnvironmentParticles;
        public ParticleSystem DashStartParticles, DashMovementParticles, DashEndParticles;
        [Min(1)] public int ParticlePoolSizePerEffect = 8;
        [Min(1)] public int MaximumImpactsPerFrame = 8;
        public Vector2 ParticleScale = new Vector2(.7f, 1.45f);
        [Range(1, 24)] public int PlaceholderParticleCount = 10;
        [Min(.01f)] public float DashMovementInterval = 0.055f;
        [Header("Launched trails")]
        [Min(0)] public int MaximumTrails = 48;
        [Min(0f)] public float TrailMinimumSpeed = 4f;
        [Range(.02f, .3f)] public float TrailDuration = 0.14f;
        [Min(0f)] public float TrailWidth = 0.24f;
        public Color TrailColor = new Color(.72f, .94f, 1f, .58f);
        [Header("Visual-only enemy deformation / flash")]
        [Range(0f, .3f)] public float SquashAmount = 0.27f;
        [Range(.02f, .3f)] public float SquashDuration = 0.16f;
        [Range(0f, 1f)] public float DeformationThreshold = .12f;
        [Range(.01f, .15f)] public float FlashDuration = 0.065f;
        [Range(0f, 1f)] public float EnemyFlashIntensity = 0.9f;
        [Range(0f, 1f)] public float PlayerFlashIntensity = 0.9f;
        public Color PlayerFlashColor = new Color(1f, .48f, .22f);
        [Header("Dash camera")]
        [Range(0f, 10f)] public float DashFovDelta = 5.5f;
        [Min(.01f)] public float DashFovTransition = 0.075f;

        public float Intensity(float speed) => Mathf.InverseLerp(MinimumCollisionSpeed,
            Mathf.Max(MinimumCollisionSpeed + .01f, FullCollisionSpeed), speed);
        public float ChainMultiplier(int depth) => Mathf.Min(MaximumChainMultiplier,
            1f + Mathf.Max(0, depth - 1) * ChainGainPerDepth);
    }
}
