using UnityEngine;
using UnityEngine.InputSystem;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(fileName = "PlayerPunchSettings", menuName = "Crowd Punch/Player Punch Settings")]
    public sealed class PlayerPunchSettings : ScriptableObject
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string attackActionName = "Player/Attack";
        [Header("Punch")]
        [SerializeField, Min(0f)] private float radius = 2f;
        [SerializeField, Min(0f)] private float range = 3f;
        [SerializeField, Min(0f)] private float strength = 12f;
        [SerializeField, Min(0f), Tooltip("Multiplier applied to player-punch knockback only when the target is an Elite. A value of 1 preserves the normal punch strength.")]
        private float eliteKnockbackMultiplier = 1f;
        [SerializeField, Min(0f)] private float damage = 10f;
        [SerializeField, Min(0f)] private float cooldown = 0.5f;
        [SerializeField, Min(0f), Tooltip("Cooldown after a punch misses every eligible enemy and intact barricade. Set to 0 for immediate retry.")]
        private float missCooldown = 0.25f;
        [SerializeField, Range(0f, 1f)] private float directionPositionWeight = 1f;
        [SerializeField, Min(0f), Tooltip("Maximum distance from a punched enemy to another enemy that aim assist can target. Set to 0 to disable aim assist.")]
        private float aimAssistRange = 22f;
        [SerializeField, Range(0f, 180f), Tooltip("Maximum horizontal angle in degrees from player facing to an aim-assist target.")]
        private float aimAssistMaximumAngleDegrees = 12f;
        [SerializeField, Range(0f, 180f), Tooltip("Retain a valid lock within this wider cone to avoid boundary flicker.")]
        private float aimAssistReleaseAngleDegrees = 16f;
        [SerializeField, Min(0f), Tooltip("Seconds a challenger must stay preferred before replacing a valid lock.")]
        private float aimAssistSwitchDelay = 0.12f;
        [SerializeField, Range(0f, 1f), Tooltip("Distance penalty relative to angle in fallback selection. Zero selects by alignment alone.")]
        private float aimAssistDistanceWeight = 0.35f;

        [Header("Direct Launch Arrow")]
        [SerializeField, Min(0f), Tooltip("Arrow length in world metres. Zero hides this arrow type.")]
        private float launchArrowLength = 5f;
        [SerializeField, Min(0.01f), Tooltip("Shaft width in world metres; the head scales with it.")]
        private float launchArrowWidth = 0.36f;
        [SerializeField] private Color launchArrowColor = new Color(1f, 1f, 1f, 0.5f);

        [Header("Predicted Collision Arrow")]
        [SerializeField, Min(0f), Tooltip("Arrow length in world metres at the first enemy hit by a launched body. Zero hides this arrow type.")]
        private float collisionArrowLength = 3f;
        [SerializeField, Min(0.01f)] private float collisionArrowWidth = 0.3f;
        [SerializeField] private Color collisionArrowColor = new Color(1f, 0.7f, 0.15f, 0.5f);
        [SerializeField, Min(0f), Tooltip("How far to sweep each punched body's collider, in world metres. Independent of arrow size and aim-assist range. Predicts the first collision only; moving crowds can change the outcome.")]
        private float collisionPredictionDistance = 18f;

        [Header("Cooldown Feedback")]
        [SerializeField, Tooltip("Name of the player-model bone that carries punch cooldown feedback.")]
        private string cooldownHandBoneName = "hand_r";
        [SerializeField, Min(0.01f), Tooltip("Hand scale multiplier immediately after a punch starts cooldown.")]
        private float cooldownHandScale = 0.65f;
        [SerializeField, Min(0.01f), Tooltip("Hand scale multiplier when the punch is ready.")]
        private float readyHandScale = 1.15f;
        [SerializeField, Tooltip("Aura material and fallback for the ready burst.")]
        private Material cooldownParticleMaterial;

        [Header("Ready Pop")]
        [SerializeField, Min(0f), Tooltip("Duration of the presentation-only fist bounce when a nonzero cooldown finishes. Zero disables it.")]
        private float readyPopDuration = 0.32f;
        [SerializeField, Tooltip("Hand scale relative to its ready size: quick overshoot, small recoil, then rest at 1.")]
        private AnimationCurve readyPopScale = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.2f, 1.35f),
            new Keyframe(0.55f, 0.94f), new Keyframe(1f, 1f));

        [Header("Cooldown Aura")]
        [SerializeField, Tooltip("Show the continuous blue/orange particles around the fist. The ready pop and star burst remain active when disabled.")]
        private bool enableCooldownAura = true;
        [SerializeField] private Color cooldownAuraColor = new Color(0.2f, 0.45f, 1f, 0.7f);
        [SerializeField] private Color readyAuraColor = new Color(1f, 0.55f, 0.08f, 0.9f);
        [SerializeField, Min(0f)] private float cooldownAuraMinimumEmission = 4f;
        [SerializeField, Min(0f)] private float cooldownAuraMaximumEmission = 32f;
        [SerializeField, Min(0.01f)] private float cooldownAuraLifetime = 0.35f;
        [SerializeField, Min(0.001f)] private float cooldownAuraParticleSize = 0.09f;
        [SerializeField, Min(0f)] private float cooldownAuraRadius = 0.12f;

        [Header("Ready Flash")]
        [SerializeField, Tooltip("Optional cartoon sparkle material for the ready burst. Falls back to the cooldown material.")]
        private Material readyFlashMaterial;
        [SerializeField] private Color readyFlashColor = new Color(1f, 0.8f, 0.18f, 1f);
        [SerializeField, Range(1, 64)] private int readyFlashParticleCount = 20;
        [SerializeField, Min(0.01f)] private float readyFlashLifetime = 0.22f;
        [SerializeField, Min(0.001f)] private float readyFlashParticleSize = 0.14f;
        [SerializeField, Min(0f)] private float readyFlashSpeed = 0.7f;

        public float Radius => radius;
        public float Range => range;
        public float Strength => strength;
        public float EliteKnockbackMultiplier => eliteKnockbackMultiplier;
        public float Damage => damage;
        public float Cooldown => cooldown;
        public float MissCooldown => missCooldown;
        public float DirectionPositionWeight => directionPositionWeight;
        public float AimAssistRange => aimAssistRange;
        public float AimAssistMaximumAngleDegrees => aimAssistMaximumAngleDegrees;
        public float AimAssistReleaseAngleDegrees => Mathf.Max(aimAssistMaximumAngleDegrees, aimAssistReleaseAngleDegrees);
        public float AimAssistSwitchDelay => Mathf.Max(0f, aimAssistSwitchDelay);
        public float AimAssistDistanceWeight => aimAssistDistanceWeight;
        public float LaunchArrowLength => Mathf.Max(0f, launchArrowLength);
        public float LaunchArrowWidth => Mathf.Max(0.01f, launchArrowWidth);
        public Color LaunchArrowColor => launchArrowColor;
        public float CollisionArrowLength => Mathf.Max(0f, collisionArrowLength);
        public float CollisionArrowWidth => Mathf.Max(0.01f, collisionArrowWidth);
        public Color CollisionArrowColor => collisionArrowColor;
        public float CollisionPredictionDistance => Mathf.Max(0f, collisionPredictionDistance);
        public string CooldownHandBoneName => cooldownHandBoneName;
        public float CooldownHandScale => cooldownHandScale;
        public float ReadyHandScale => readyHandScale;
        public Material CooldownParticleMaterial => cooldownParticleMaterial;
        public float ReadyPopDuration => readyPopDuration;
        public AnimationCurve ReadyPopScale => readyPopScale;
        public Material ReadyFlashMaterial => readyFlashMaterial != null ? readyFlashMaterial : cooldownParticleMaterial;
        public bool EnableCooldownAura => enableCooldownAura;
        public Color CooldownAuraColor => cooldownAuraColor;
        public Color ReadyAuraColor => readyAuraColor;
        public float CooldownAuraMinimumEmission => cooldownAuraMinimumEmission;
        public float CooldownAuraMaximumEmission => cooldownAuraMaximumEmission;
        public float CooldownAuraLifetime => cooldownAuraLifetime;
        public float CooldownAuraParticleSize => cooldownAuraParticleSize;
        public float CooldownAuraRadius => cooldownAuraRadius;
        public Color ReadyFlashColor => readyFlashColor;
        public int ReadyFlashParticleCount => readyFlashParticleCount;
        public float ReadyFlashLifetime => readyFlashLifetime;
        public float ReadyFlashParticleSize => readyFlashParticleSize;
        public float ReadyFlashSpeed => readyFlashSpeed;

        public InputAction FindAttackAction()
        {
            return inputActions == null ? null : inputActions.FindAction(attackActionName);
        }
    }

}
