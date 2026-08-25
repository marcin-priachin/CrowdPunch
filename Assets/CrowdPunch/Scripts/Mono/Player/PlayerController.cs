using System;
using CrowdPunch.Configuration;
using Unity.Mathematics;
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
        private uint submittedMovementSequence;
        private uint appliedMovementSequence;

        public event Action DashStarted;
        public event Action DashEnded;

        public bool IsDashing => dashActive;

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
            ApplyResolvedMovement(false);

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

            Vector3 facingDirection = cameraForward;
            Vector3 playerDisplacement = Time.deltaTime * settings.MoveSpeed * movement;
            if (IsDashing && dashTimeRemaining <= 0f)
            {
                EndDash();
            }
            else if (IsDashing)
            {
                float safeDashDuration = Mathf.Max(0.001f, settings.DashDuration);
                float dashStep = Mathf.Min(Time.deltaTime, dashTimeRemaining);
                float dashSpeed = settings.DashDistance / safeDashDuration;
                playerDisplacement = dashStep * dashSpeed * dashDirection;
                dashTimeRemaining -= dashStep;

                if (dashTimeRemaining <= 0f)
                {
                    EndDash();
                }
            }

            Vector3 movementStart = transform.position;
            transform.position += playerDisplacement + Time.deltaTime * knockbackVelocity;
            submittedMovementSequence = ecsBridge.PublishMovement(
                movementStart,
                transform.position,
                settings.PlayerRadius);
            knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, settings.KnockbackDamping * Time.deltaTime);

            if (facingDirection.sqrMagnitude > 0.001f)
            {
                transform.forward = facingDirection;
            }

            ecsBridge.PublishPlayerSnapshot(transform.position, transform.forward, settings.PlayerRadius);
        }

        private void LateUpdate()
        {
            ApplyResolvedMovement(true);
        }

        private void ApplyResolvedMovement(bool requireLatestSubmission)
        {
            uint resolvedSequence = ecsBridge.ResolvedMovementSequence;
            if (resolvedSequence == appliedMovementSequence
                || (requireLatestSubmission && resolvedSequence != submittedMovementSequence))
            {
                return;
            }

            float3 resolved = ecsBridge.ResolvedMovementPosition;
            transform.position = new Vector3(resolved.x, resolved.y, resolved.z);
            appliedMovementSequence = resolvedSequence;
            ecsBridge.PublishPlayerSnapshot(transform.position, transform.forward, settings.PlayerRadius);
        }

        private void ApplyKnockback(Vector3 impulse)
        {
            knockbackVelocity += new Vector3(impulse.x, 0f, impulse.z);
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
            ecsBridge.ClearMovement();
            submittedMovementSequence = ecsBridge.MovementSequence;
            appliedMovementSequence = submittedMovementSequence;
        }
    }
}
