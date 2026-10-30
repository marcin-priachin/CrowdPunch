using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerMovementAnimation : MonoBehaviour
    {
        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveZ = Animator.StringToHash("MoveZ");
        private static readonly int DashX = Animator.StringToHash("DashX");
        private static readonly int DashZ = Animator.StringToHash("DashZ");
        private static readonly int DashSpeed = Animator.StringToHash("DashSpeed");
        private static readonly int DashState = Animator.StringToHash("Base Layer.Dash");
        private static readonly int LocomotionState = Animator.StringToHash("Base Layer.Locomotion");

        [SerializeField] private PlayerController playerController;
        [SerializeField, Min(0f)] private float blendDamping = 0.08f;

        [Header("Dash Transitions")]
        [SerializeField, Min(0f)]
        [Tooltip("Walk/idle to dash blend duration in seconds. Zero switches immediately.")]
        private float dashBlendInDuration = 0.04f;
        [SerializeField, Min(0f)]
        [Tooltip("Dash to walk/idle blend duration in seconds. Zero switches immediately.")]
        private float dashBlendOutDuration = 0.06f;

        private Animator animator;
        private bool wasDashing;
        private float movementAngle;
        private float movementMagnitude;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (playerController == null)
            {
                playerController = GetComponentInParent<PlayerController>();
            }

            // PLAYER-002: the controller owns translation and camera-forward facing.
            animator.applyRootMotion = false;
        }

        private void Update()
        {
            if (animator.runtimeAnimatorController == null)
            {
                return;
            }

            Vector3 movement = Vector3.zero;
            if (playerController != null && playerController.isActiveAndEnabled
                && playerController.MovementSettings != null)
            {
                movement = playerController.transform.InverseTransformDirection(playerController.LocomotionVelocity);
                movement.y = 0f;
                movement = Vector3.ClampMagnitude(
                    movement / Mathf.Max(0.001f, playerController.MovementSettings.MoveSpeed), 1f);
            }

            SetLocomotionBlend(movement, Time.deltaTime);

            // PLAYER-002/005: only the base pose follows the committed dash;
            // the existing upper-body layer keeps punches independent of travel.
            bool dashing = playerController != null && playerController.isActiveAndEnabled
                && playerController.IsDashing;
            if (dashing)
            {
                Vector3 direction = transform.InverseTransformDirection(playerController.DashDirection);
                animator.SetFloat(DashX, direction.x);
                animator.SetFloat(DashZ, direction.z);
                // Each blend-tree child is normalized to one second by the asset builder.
                // One playback then takes exactly the authored gameplay dash duration.
                animator.SetFloat(DashSpeed, 1f / Mathf.Max(0.001f, playerController.MovementSettings.DashDuration));
                if (!wasDashing)
                    animator.CrossFadeInFixedTime(DashState, dashBlendInDuration, 0, 0f);
            }
            else if (wasDashing)
            {
                animator.CrossFadeInFixedTime(LocomotionState, dashBlendOutDuration, 0, 0f);
            }
            wasDashing = dashing;
        }

        private void SetLocomotionBlend(Vector3 movement, float deltaTime)
        {
            float targetMagnitude = movement.magnitude;
            float damping = blendDamping <= 0f ? 1f : 1f - Mathf.Exp(-deltaTime / blendDamping);
            if (targetMagnitude > 0.001f)
            {
                float targetAngle = Mathf.Atan2(movement.x, movement.z) * Mathf.Rad2Deg;
                movementAngle = movementMagnitude < 0.001f
                    ? targetAngle : Mathf.LerpAngle(movementAngle, targetAngle, damping);
            }
            // PLAYER-002: a change of direction must not pass through the idle stance.
            // Damp direction and speed separately, keeping full-speed turns on the walk ring.
            movementMagnitude = Mathf.Lerp(movementMagnitude, targetMagnitude, damping);
            float angle = movementAngle * Mathf.Deg2Rad;
            animator.SetFloat(MoveX, Mathf.Sin(angle) * movementMagnitude);
            animator.SetFloat(MoveZ, Mathf.Cos(angle) * movementMagnitude);
        }

        private void OnDisable()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            animator.SetFloat(MoveX, 0f);
            animator.SetFloat(MoveZ, 0f);
            movementMagnitude = 0f;
            movementAngle = 0f;
            if (wasDashing)
                animator.Play(LocomotionState, 0, 0f);
            wasDashing = false;
        }
    }
}
