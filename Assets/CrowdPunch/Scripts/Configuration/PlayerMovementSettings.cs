using UnityEngine;
using UnityEngine.InputSystem;

namespace CrowdPunch.Configuration
{
    [CreateAssetMenu(fileName = "PlayerMovementSettings", menuName = "Crowd Punch/Player Movement Settings")]
    public sealed class PlayerMovementSettings : ScriptableObject
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string moveActionName = "Player/Move";
        [SerializeField] private string dashActionName = "Player/Dash";
        [Header("Movement")]
        [SerializeField, Min(0f)] private float playerRadius = 1.5f;
        [SerializeField, Min(0f)] private float moveSpeed = 6f;
        [SerializeField, Min(0f)] private float dashDistance = 5f;
        [SerializeField, Min(0f)] private float dashDuration = 0.15f;
        [SerializeField, Min(0f)] private float dashCooldown = 0.1f;
        [SerializeField, Min(0f)] private float knockbackDamping = 14f;

        public float PlayerRadius => playerRadius;
        public float MoveSpeed => moveSpeed;
        public float DashDistance => dashDistance;
        public float DashDuration => dashDuration;
        public float DashCooldown => dashCooldown;
        public float KnockbackDamping => knockbackDamping;

        public InputAction FindMoveAction()
        {
            return inputActions == null ? null : inputActions.FindAction(moveActionName);
        }

        public InputAction FindDashAction()
        {
            return inputActions == null ? null : inputActions.FindAction(dashActionName);
        }
    }

}
