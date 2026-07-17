using UnityEngine;
using UnityEngine.InputSystem;

namespace CrowdPunch.Mono.Player
{
    /// <summary>
    /// Traditional GameObject player movement controller.
    /// </summary>
    [RequireComponent(typeof(PlayerEcsBridge))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerEcsBridge ecsBridge;
        [SerializeField] private PlayerPunch playerPunch;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private float playerRadius = 1.5f;
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float knockbackDamping = 14f;
        [SerializeField] private float touchStickRadiusPixels = 140f;
        [SerializeField] private float touchDeadZonePixels = 18f;
        [SerializeField] private float aimLineLength = 3f;
        [SerializeField] private float aimLineHeight = 0.08f;
        [SerializeField] private float fallbackAimLineWidth = 0.08f;
        [SerializeField] private Color aimLineColor = Color.white;

        private const int NoTouch = -1;

        private int leftTouchId = NoTouch;
        private int rightTouchId = NoTouch;
        private Vector2 leftTouchStart;
        private Vector2 rightTouchStart;
        private Vector2 rightAimInput;
        private bool rightTouchHadAim;
        private LineRenderer aimLine;
        private Vector3 knockbackVelocity;
        private Vector3 initialPosition;
        private Quaternion initialRotation;

        private void Reset()
        {
            ecsBridge = GetComponent<PlayerEcsBridge>();
            playerPunch = GetComponent<PlayerPunch>();
            playerHealth = GetComponent<PlayerHealth>();
        }

        private void Awake()
        {
            if (ecsBridge == null)
            {
                ecsBridge = GetComponent<PlayerEcsBridge>();
            }

            if (playerPunch == null)
            {
                playerPunch = GetComponent<PlayerPunch>();
            }

            if (playerHealth == null)
            {
                playerHealth = GetComponent<PlayerHealth>();
            }

            EnsureAimLine();
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
            moveAction?.action.Enable();
        }

        private void OnDisable()
        {
            moveAction?.action.Disable();
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
            Vector2 actionMoveInput = moveAction == null
                ? Vector2.zero
                : moveAction.action.ReadValue<Vector2>();

            UpdateTouchControls(out Vector2 touchMoveInput, out bool hasTouchAim, out bool touchPunchReleased);

            Vector2 moveInput = Vector2.ClampMagnitude(actionMoveInput + touchMoveInput, 1f);
            Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y);
            transform.position += Time.deltaTime * (moveSpeed * movement + knockbackVelocity);
            knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, knockbackDamping * Time.deltaTime);

            if (hasTouchAim)
            {
                transform.forward = new Vector3(rightAimInput.x, 0f, rightAimInput.y).normalized;
            }
            else if (actionMoveInput.sqrMagnitude > 0.001f && movement.sqrMagnitude > 0.001f)
            {
                transform.forward = movement.normalized;
            }

            if (touchPunchReleased)
            {
                playerPunch?.RequestPunch();
            }

            UpdateAimLine(hasTouchAim);

            ecsBridge.PublishPlayerSnapshot(transform.position, transform.forward, playerRadius);
        }

        private void ApplyKnockback(Vector3 impulse)
        {
            knockbackVelocity += new Vector3(impulse.x, 0f, impulse.z);
        }

        public void ResetPlayerState()
        {
            knockbackVelocity = Vector3.zero;
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            ecsBridge.PublishPlayerSnapshot(transform.position, transform.forward, playerRadius);
        }

        private void UpdateTouchControls(out Vector2 moveInput, out bool hasAim, out bool punchReleased)
        {
            moveInput = Vector2.zero;
            hasAim = false;
            punchReleased = false;

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                return;
            }

            foreach (UnityEngine.InputSystem.Controls.TouchControl touch in touchscreen.touches)
            {
                int touchId = touch.touchId.ReadValue();
                Vector2 position = touch.position.ReadValue();

                if (touch.press.wasPressedThisFrame)
                {
                    if (position.x < Screen.width * 0.5f && leftTouchId == NoTouch)
                    {
                        leftTouchId = touchId;
                        leftTouchStart = position;
                    }
                    else if (position.x >= Screen.width * 0.5f && rightTouchId == NoTouch)
                    {
                        rightTouchId = touchId;
                        rightTouchStart = position;
                        rightTouchHadAim = false;
                    }
                }

                if (touchId == leftTouchId)
                {
                    if (touch.press.wasReleasedThisFrame)
                    {
                        leftTouchId = NoTouch;
                    }
                    else if (touch.press.isPressed)
                    {
                        moveInput = ReadStickVector(leftTouchStart, position);
                    }
                }
                else if (touchId == rightTouchId)
                {
                    Vector2 aimInput = ReadStickVector(rightTouchStart, position);
                    if (aimInput.sqrMagnitude > 0.001f)
                    {
                        rightAimInput = aimInput;
                        rightTouchHadAim = true;
                        hasAim = true;
                    }

                    if (touch.press.wasReleasedThisFrame)
                    {
                        punchReleased = rightTouchHadAim;
                        rightTouchId = NoTouch;
                        rightTouchHadAim = false;
                    }
                }
            }
        }

        private Vector2 ReadStickVector(Vector2 start, Vector2 current)
        {
            Vector2 drag = current - start;
            float dragMagnitude = drag.magnitude;

            if (dragMagnitude < touchDeadZonePixels)
            {
                return Vector2.zero;
            }

            float radius = Mathf.Max(touchDeadZonePixels + 1f, touchStickRadiusPixels);
            return Vector2.ClampMagnitude(drag / radius, 1f);
        }

        private void EnsureAimLine()
        {
            if (aimLine != null)
            {
                return;
            }

            GameObject lineObject = new GameObject("Aim Direction Line");
            lineObject.transform.SetParent(transform, false);

            aimLine = lineObject.AddComponent<LineRenderer>();
            aimLine.positionCount = 2;
            aimLine.useWorldSpace = true;
            aimLine.startWidth = fallbackAimLineWidth;
            aimLine.endWidth = fallbackAimLineWidth;
            aimLine.startColor = aimLineColor;
            aimLine.endColor = aimLineColor;
            aimLine.material = new Material(Shader.Find("Sprites/Default"));
            aimLine.enabled = false;
        }

        private void UpdateAimLine(bool isAiming)
        {
            if (aimLine == null)
            {
                return;
            }

            aimLine.enabled = isAiming;
            if (!isAiming)
            {
                return;
            }

            float lineWidth = playerPunch == null ? fallbackAimLineWidth : playerPunch.PunchRadius * 2f;
            aimLine.startWidth = lineWidth;
            aimLine.endWidth = lineWidth;

            Vector3 start = transform.position + Vector3.up * aimLineHeight;
            Vector3 end = start + transform.forward * aimLineLength;
            aimLine.SetPosition(0, start);
            aimLine.SetPosition(1, end);
        }
    }
}
