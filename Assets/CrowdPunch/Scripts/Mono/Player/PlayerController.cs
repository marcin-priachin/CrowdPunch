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
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private float playerRadius = 1.5f;
        [SerializeField] private float moveSpeed = 6f;

        private void Reset()
        {
            ecsBridge = GetComponent<PlayerEcsBridge>();
        }

        private void OnEnable()
        {
            moveAction?.action.Enable();
        }

        private void OnDisable()
        {
            moveAction?.action.Disable();
        }

        private void Update()
        {
            Vector2 moveInput = moveAction == null
                ? Vector2.zero
                : moveAction.action.ReadValue<Vector2>();

            Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y);
            transform.position += moveSpeed * Time.deltaTime * movement;

            if (movement.sqrMagnitude > 0.001f)
            {
                transform.forward = movement.normalized;
            }

            ecsBridge.PublishPlayerSnapshot(transform.position, transform.forward, playerRadius);
        }
    }
}
