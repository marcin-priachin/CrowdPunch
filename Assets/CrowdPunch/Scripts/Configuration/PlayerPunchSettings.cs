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
        [SerializeField, Min(0f), Tooltip("Multiplier applied to player-punch knockback only when the target is an Elite. A value of 1 preserves the normal or dash punch strength.")]
        private float eliteKnockbackMultiplier = 1f;
        [SerializeField, Min(0f)] private float damage = 10f;
        [SerializeField, Min(0f)] private float dashStrength = 12f;
        [SerializeField, Min(0f)] private float dashDamage = 10f;
        [SerializeField, Min(0f)] private float cooldown = 0.5f;
        [SerializeField, Range(0f, 1f)] private float directionPositionWeight = 1f;
        [SerializeField, Min(0f), Tooltip("Maximum distance from a punched enemy to another enemy that aim assist can target. Set to 0 to disable aim assist.")]
        private float aimAssistRange = 8f;
        [SerializeField, Min(0f)] private float areaFeedbackDuration = 0.5f;

        public float Radius => radius;
        public float Range => range;
        public float Strength => strength;
        public float EliteKnockbackMultiplier => eliteKnockbackMultiplier;
        public float Damage => damage;
        public float DashStrength => dashStrength;
        public float DashDamage => dashDamage;
        public float Cooldown => cooldown;
        public float DirectionPositionWeight => directionPositionWeight;
        public float AimAssistRange => aimAssistRange;
        public float AreaFeedbackDuration => areaFeedbackDuration;

        public InputAction FindAttackAction()
        {
            return inputActions == null ? null : inputActions.FindAction(attackActionName);
        }
    }

}
