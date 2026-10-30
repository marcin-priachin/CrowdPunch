using UnityEngine;
using UnityEngine.Serialization;

namespace CrowdPunch.Mono.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [DefaultExecutionOrder(10)]
    public sealed class PlayerTurnAnimation : MonoBehaviour
    {
        private static readonly int LeftTurn = Animator.StringToHash("Base Layer.Left Turn");
        private static readonly int RightTurn = Animator.StringToHash("Base Layer.Right Turn");
        private static readonly int Locomotion = Animator.StringToHash("Base Layer.Locomotion");
        private static readonly int TurnSpeed = Animator.StringToHash("TurnSpeed");

        [SerializeField] private PlayerController playerController;
        [SerializeField, Min(0f)] private float minimumTurnAngle = 10f;
        [SerializeField, Min(0f)] private float minimumTurnSpeed = 15f;
        [FormerlySerializedAs("blendDuration")]
        [SerializeField, Min(0f)] private float blendInDuration = 0.2f;
        [SerializeField, Min(0f)] private float blendOutDuration = 0.08f;
        [SerializeField, Min(0f)] private float stopDelay = 0.06f;

        private Animator animator;
        private Vector3 previousForward;
        private float accumulatedYaw;
        private int direction;
        private float stoppedTime;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (playerController == null)
                playerController = GetComponentInParent<PlayerController>();
        }

        private void OnEnable()
        {
            previousForward = Facing;
            accumulatedYaw = 0f;
            direction = 0;
            stoppedTime = 0f;
        }

        private Vector3 Facing => playerController == null ? transform.forward : playerController.transform.forward;

        private void Update()
        {
            UpdateTurn(Time.deltaTime);
        }

        private void UpdateTurn(float deltaTime)
        {
            Vector3 forward = Facing;
            float yaw = Vector3.SignedAngle(previousForward, forward, Vector3.up);
            previousForward = forward;
            if (animator == null || animator.runtimeAnimatorController == null)
                return;

            // PLAYER-002/005: observe camera-facing rotation; gameplay retains root ownership.
            bool stationary = playerController != null && playerController.isActiveAndEnabled
                && !playerController.IsDashing && playerController.LocomotionVelocity.sqrMagnitude < 0.0025f;
            if (!stationary)
            {
                // Movement animation has already selected Dash; do not overwrite it on interruption.
                ClearTurn(playerController == null || !playerController.IsDashing);
                return;
            }
            if (deltaTime <= 0f || FeedbackTimeController.IsSuspended)
                return;

            float yawSpeed = Mathf.Abs(yaw) / deltaTime;
            bool rotating = yawSpeed >= minimumTurnSpeed;
            if (rotating)
            {
                if (Mathf.Sign(accumulatedYaw) != Mathf.Sign(yaw))
                    accumulatedYaw = 0f;
                accumulatedYaw += yaw;
            }
            else accumulatedYaw = 0f;

            // PLAYER-002: brief input pauses are tolerated, but returning to idle must
            // not wait for a slow turn clip to finish its complete authored step.
            stoppedTime = rotating ? 0f : stoppedTime + deltaTime;
            if (direction != 0 && !rotating && stoppedTime >= stopDelay)
            {
                ClearTurn(true);
                return;
            }

            int requested = Mathf.Abs(accumulatedYaw) >= minimumTurnAngle && rotating
                ? (yaw > 0f ? 1 : -1) : 0;
            if (direction != 0 && !animator.IsInTransition(0)
                && animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
            {
                direction = 0;
                if (requested == 0)
                    animator.CrossFadeInFixedTime(Locomotion, blendOutDuration, 0, 0f);
            }
            if (requested != 0 && requested != direction)
            {
                direction = requested;
                accumulatedYaw = 0f;
                animator.SetFloat(TurnSpeed, Mathf.Clamp(yawSpeed / 90f, 0.65f, 2f));
                // Keep the source pelvis and legs together. Upper-body punch/hit layers
                // and the existing spine correction isolate the torso from this base pose.
                animator.CrossFadeInFixedTime(direction > 0 ? RightTurn : LeftTurn, blendInDuration, 0, 0f);
            }
        }

        private void OnDisable()
        {
            ClearTurn(playerController == null || !playerController.IsDashing);
        }

        private void ClearTurn(bool restoreLocomotion)
        {
            accumulatedYaw = 0f;
            stoppedTime = 0f;
            bool wasTurning = direction != 0;
            direction = 0;
            if (wasTurning && restoreLocomotion && animator != null && animator.runtimeAnimatorController != null)
                animator.CrossFadeInFixedTime(Locomotion, blendOutDuration, 0, 0f);
        }
    }
}
