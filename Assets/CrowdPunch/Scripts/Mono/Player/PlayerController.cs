using System;
using CrowdPunch.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CrowdPunch.Mono.Player
{
    /// <summary>
    /// Traditional GameObject player movement controller.
    /// </summary>
    [RequireComponent(typeof(PlayerEcsBridge))]
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerEcsBridge ecsBridge;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Transform movementCamera;
        [SerializeField] private PlayerMovementSettings settings;
        private InputAction moveAction;
        private InputAction dashAction;
        private Vector3 dashDirection;
        private float dashTimeRemaining;
        private bool dashActive;
        private float nextDashTime;
        private Vector3 knockbackVelocity;
        private Vector3 initialPosition;
        private Quaternion initialRotation;

        public event Action DashStarted;
        public event Action DashPunchMidpointReached;
        public event Action DashEnded;

        public bool IsDashing => dashActive;

        public float NormalizedDashProgress
        {
            get
            {
                float duration = settings == null ? 0f : settings.DashDuration;
                return duration <= 0f ? 1f : Mathf.Clamp01(1f - dashTimeRemaining / duration);
            }
        }

        private void Reset()
        {
            ecsBridge = GetComponent<PlayerEcsBridge>();
            playerHealth = GetComponent<PlayerHealth>();
            movementCamera = UnityEngine.Camera.main == null ? null : UnityEngine.Camera.main.transform;
        }

        private void Awake()
        {
            if (settings == null)
            {
                Debug.LogError($"{nameof(PlayerController)} requires {nameof(PlayerMovementSettings)}.", this);
                enabled = false;
                return;
            }

            if (ecsBridge == null)
            {
                ecsBridge = GetComponent<PlayerEcsBridge>();
            }

            if (playerHealth == null)
            {
                playerHealth = GetComponent<PlayerHealth>();
            }

            if (movementCamera == null && UnityEngine.Camera.main != null)
            {
                movementCamera = UnityEngine.Camera.main.transform;
            }

            moveAction = settings.FindMoveAction();
            dashAction = settings.FindDashAction();

            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

        private void Start()
        {
            if (playerHealth == null)
            {
                playerHealth = GetComponent<PlayerHealth>();
            }

            if (playerHealth != null)
            {
                playerHealth.DamageAccepted += ApplyKnockback;
            }
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            dashAction?.Enable();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            dashAction?.Disable();
            EndDash();
        }

        private void OnDestroy()
        {
            if (playerHealth != null)
            {
                playerHealth.DamageAccepted -= ApplyKnockback;
            }
        }

        private void Update()
        {
            Vector2 moveInput = moveAction == null
                ? Vector2.zero
                : moveAction.ReadValue<Vector2>();

            Vector3 cameraForward = movementCamera == null ? Vector3.forward : movementCamera.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();

            Vector3 cameraRight = movementCamera == null ? Vector3.right : movementCamera.right;
            cameraRight.y = 0f;
            cameraRight.Normalize();

            Vector3 movement = Vector3.ClampMagnitude(
                cameraRight * moveInput.x + cameraForward * moveInput.y,
                1f);

            if (dashAction != null && dashAction.WasPressedThisFrame() && Time.time >= nextDashTime)
            {
                dashDirection = movement.sqrMagnitude > 0.001f ? movement.normalized : cameraForward;
                dashTimeRemaining = settings.DashDuration;
                dashActive = true;
                nextDashTime = Time.time + dashTimeRemaining + settings.DashCooldown;
                DashStarted?.Invoke();
            }

            Vector3 facingDirection = IsDashing ? dashDirection : cameraForward;
            Vector3 playerDisplacement = Time.deltaTime * settings.MoveSpeed * movement;
            if (IsDashing && dashTimeRemaining <= 0f)
            {
                DashPunchMidpointReached?.Invoke();
                EndDash();
            }
            else if (IsDashing)
            {
                float safeDashDuration = Mathf.Max(0.001f, settings.DashDuration);
                float previousProgress = NormalizedDashProgress;
                float dashStep = Mathf.Min(Time.deltaTime, dashTimeRemaining);
                float midpoint = settings.DashPunchMidpointNormalized;
                float stepBeforeMidpoint = dashStep;
                if (previousProgress < midpoint)
                {
                    float secondsUntilMidpoint = (midpoint - previousProgress) * safeDashDuration;
                    stepBeforeMidpoint = Mathf.Min(dashStep, secondsUntilMidpoint);
                }

                float dashSpeed = settings.DashDistance / safeDashDuration;
                playerDisplacement = stepBeforeMidpoint * dashSpeed * dashDirection;
                dashTimeRemaining -= stepBeforeMidpoint;

                if (previousProgress < midpoint && NormalizedDashProgress >= midpoint)
                {
                    DashPunchMidpointReached?.Invoke();
                }

                if (IsDashing)
                {
                    float stepAfterMidpoint = dashStep - stepBeforeMidpoint;
                    playerDisplacement += stepAfterMidpoint * dashSpeed * dashDirection;
                    dashTimeRemaining -= stepAfterMidpoint;

                    if (dashTimeRemaining <= 0f)
                    {
                        EndDash();
                    }
                }
            }

            transform.position += playerDisplacement + Time.deltaTime * knockbackVelocity;
            knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, settings.KnockbackDamping * Time.deltaTime);

            if (facingDirection.sqrMagnitude > 0.001f)
            {
                transform.forward = facingDirection;
            }

            ecsBridge.PublishPlayerSnapshot(transform.position, transform.forward, settings.PlayerRadius);
        }

        private void ApplyKnockback(Vector3 impulse)
        {
            knockbackVelocity += new Vector3(impulse.x, 0f, impulse.z);
        }

        public bool TryCancelDashForPunch(out Vector3 committedDashDirection)
        {
            if (!IsDashing || NormalizedDashProgress < settings.DashPunchMidpointNormalized)
            {
                committedDashDirection = Vector3.zero;
                return false;
            }

            committedDashDirection = dashDirection;
            EndDash();
            return true;
        }

        private void EndDash()
        {
            if (!IsDashing)
            {
                return;
            }

            dashDirection = Vector3.zero;
            dashTimeRemaining = 0f;
            dashActive = false;
            DashEnded?.Invoke();
        }

        public void ResetPlayerState()
        {
            EndDash();
            dashDirection = Vector3.zero;
            nextDashTime = 0f;
            knockbackVelocity = Vector3.zero;
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            ecsBridge.PublishPlayerSnapshot(transform.position, transform.forward, settings.PlayerRadius);
        }
    }
}
