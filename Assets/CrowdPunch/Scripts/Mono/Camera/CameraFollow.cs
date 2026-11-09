using CrowdPunch.Configuration;
using CrowdPunch.Mono.Levels;
using CrowdPunch.Mono.Player;
using CrowdPunch.Mono.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CrowdPunch.Mono.Camera
{
    /// <summary>
    /// Traditional camera follower for the GameObject player.
    /// </summary>
    [RequireComponent(typeof(CameraSceneryCutaway))]
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private PlayerMovementSettings movementSettings;
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -10f);
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);
        [SerializeField] private float horizontalLookSensitivity = 0.15f;
        [Header("Follow Smoothing")]
        [SerializeField, Min(.01f), Tooltip("How quickly the camera follows regular movement and orbit input. Higher values catch up faster.")]
        private float followSharpness = 12f;
        [SerializeField, Min(.01f), Tooltip("How quickly the camera catches up after a punch repositions the player. Higher values catch up faster.")]
        private float punchRepositionSharpness = 12f;

        private CombatCameraFeedback feedback;
        private CameraSceneryCutaway sceneryCutaway;
        private InputAction lookAction;
        private PlayerEcsBridge aimBridge;
        private LevelClearCamera clearCamera;
        private bool wasFramingClear;
        private float orbitAngle;
        private Vector3 punchFocusLag;
        private uint observedRestart;
        public float EncounterLookHeight { get; set; }

        private void Awake()
        {
            sceneryCutaway = GetComponent<CameraSceneryCutaway>();
            clearCamera = new LevelClearCamera(GetComponent<UnityEngine.Camera>(), FindFirstObjectByType<GauntletSequence>());
            orbitAngle = Mathf.Atan2(offset.x, -offset.z) * Mathf.Rad2Deg;

            if (movementSettings == null && target != null
                && target.TryGetComponent(out PlayerController controller))
            {
                movementSettings = controller.MovementSettings;
            }

            lookAction = movementSettings == null ? null : movementSettings.FindLookAction();

            if (lookAction == null)
            {
                Debug.LogError($"{nameof(CameraFollow)} requires a movement settings asset with a valid look action.", this);
            }
        }

        private void OnEnable()
        {
            lookAction?.Enable();
            observedRestart = GameRestartRegistry.Sequence;
            if (target != null && target.TryGetComponent(out aimBridge))
                aimBridge.PunchApproachApplied += OnPunchApproach;
        }

        private void OnDisable()
        {
            lookAction?.Disable();
            if (aimBridge != null)
                aimBridge.PunchApproachApplied -= OnPunchApproach;
            aimBridge = null;
            punchFocusLag = Vector3.zero;
        }

        private void OnPunchApproach(Vector3 displacement)
        {
            punchFocusLag -= displacement;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (feedback == null) feedback = GetComponent<CombatCameraFeedback>();
            feedback?.RemoveOffset();
            float horizontalLook = CrowdPunch.Mono.Player.FeedbackTimeController.IsSuspended
                ? 0f : lookAction?.ReadValue<Vector2>().x ?? 0f;
            bool usesJoystick = lookAction?.activeControl?.device is Gamepad
                || lookAction?.activeControl?.device is Joystick;
            float inputRotation = usesJoystick
                ? horizontalLook * movementSettings.JoystickRotationSpeed * Time.deltaTime
                : horizontalLook * horizontalLookSensitivity;
            orbitAngle += inputRotation;
            if (aimBridge == null && target.TryGetComponent(out aimBridge))
                aimBridge.PunchApproachApplied += OnPunchApproach;
            aimBridge?.PublishAimRotation(inputRotation);

            if (observedRestart != GameRestartRegistry.Sequence)
            {
                observedRestart = GameRestartRegistry.Sequence;
                punchFocusLag = Vector3.zero;
            }

            Vector3 orbitOffset = Quaternion.Euler(0f, orbitAngle, 0f)
                * new Vector3(0f, offset.y, -new Vector2(offset.x, offset.z).magnitude);
            if (clearCamera.TryFrame(orbitOffset, target.position, targetOffset, out var subject))
            {
                wasFramingClear = true;
                punchFocusLag = Vector3.zero;
                sceneryCutaway?.SetFocus(subject);
                return;
            }
            Vector3 lookTarget = target.position + targetOffset + Vector3.up * EncounterLookHeight;
            float followInterpolation = wasFramingClear ? 1f : 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            float punchInterpolation = wasFramingClear ? 1f : 1f - Mathf.Exp(-punchRepositionSharpness * Time.deltaTime);
            wasFramingClear = false;

            // Follow ordinary motion without the pending punch displacement, then apply
            // this frame's punch catch-up equally to the camera position and look target.
            Vector3 previousPunchLag = punchFocusLag;
            punchFocusLag = Vector3.Lerp(previousPunchLag, Vector3.zero, punchInterpolation);
            Vector3 desiredPosition = lookTarget + previousPunchLag + orbitOffset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followInterpolation)
                + (punchFocusLag - previousPunchLag);
            lookTarget += punchFocusLag;
            transform.LookAt(lookTarget);
            feedback?.ApplyOffset();
            sceneryCutaway?.SetFocus(lookTarget);
        }
    }
}
