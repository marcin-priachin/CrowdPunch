using UnityEngine;
using UnityEngine.InputSystem;

namespace CrowdPunch.Mono.Camera
{
    /// <summary>
    /// Traditional camera follower for the GameObject player.
    /// </summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -10f);
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);
        [SerializeField] private float horizontalLookSensitivity = 0.15f;
        [SerializeField] private float followSharpness = 12f;

        private InputAction lookAction;
        private float orbitAngle;

        private void Awake()
        {
            orbitAngle = Mathf.Atan2(offset.x, -offset.z) * Mathf.Rad2Deg;
            lookAction = InputSystem.actions?.FindAction("Player/Look");
        }

        private void OnEnable()
        {
            lookAction?.Enable();
        }

        private void OnDisable()
        {
            lookAction?.Disable();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            float horizontalLook = lookAction?.ReadValue<Vector2>().x ?? 0f;
            orbitAngle += horizontalLook * horizontalLookSensitivity;

            Vector3 orbitOffset = Quaternion.Euler(0f, orbitAngle, 0f)
                * new Vector3(0f, offset.y, -new Vector2(offset.x, offset.z).magnitude);
            Vector3 lookTarget = target.position + targetOffset;
            Vector3 desiredPosition = lookTarget + orbitOffset;
            float interpolation = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);

            transform.position = Vector3.Lerp(transform.position, desiredPosition, interpolation);
            transform.LookAt(lookTarget);
        }
    }
}
