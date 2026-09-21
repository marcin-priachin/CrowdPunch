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
        [SerializeField, Range(0f, 1f)] private float directionPositionWeight = 1f;
        [SerializeField, Min(0f), Tooltip("Maximum distance from a punched enemy to another enemy that aim assist can target. Set to 0 to disable aim assist.")]
        private float aimAssistRange = 8f;
        [SerializeField, Range(0f, 180f), Tooltip("Maximum horizontal angle in degrees from player facing to an aim-assist target.")]
        private float aimAssistMaximumAngleDegrees = 45f;

        [Header("Cooldown Feedback")]
        [SerializeField, Tooltip("Name of the player-model bone that carries punch cooldown feedback.")]
        private string cooldownHandBoneName = "hand_r";
        [SerializeField, Min(0.01f), Tooltip("Hand scale multiplier immediately after a punch starts cooldown.")]
        private float cooldownHandScale = 0.65f;
        [SerializeField, Min(0.01f), Tooltip("Hand scale multiplier when the punch is ready.")]
        private float readyHandScale = 1.15f;
        [SerializeField, Tooltip("Material used by both generated hand particle effects.")]
        private Material cooldownParticleMaterial;

        [Header("Cooldown Aura")]
        [SerializeField] private Color cooldownAuraColor = new Color(0.2f, 0.45f, 1f, 0.7f);
        [SerializeField] private Color readyAuraColor = new Color(1f, 0.55f, 0.08f, 0.9f);
        [SerializeField, Min(0f)] private float cooldownAuraMinimumEmission = 4f;
        [SerializeField, Min(0f)] private float cooldownAuraMaximumEmission = 32f;
        [SerializeField, Min(0.01f)] private float cooldownAuraLifetime = 0.35f;
        [SerializeField, Min(0.001f)] private float cooldownAuraParticleSize = 0.09f;
        [SerializeField, Min(0f)] private float cooldownAuraRadius = 0.12f;

        [Header("Ready Flash")]
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
        public float DirectionPositionWeight => directionPositionWeight;
        public float AimAssistRange => aimAssistRange;
        public float AimAssistMaximumAngleDegrees => aimAssistMaximumAngleDegrees;
        public string CooldownHandBoneName => cooldownHandBoneName;
        public float CooldownHandScale => cooldownHandScale;
        public float ReadyHandScale => readyHandScale;
        public Material CooldownParticleMaterial => cooldownParticleMaterial;
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
