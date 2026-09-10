using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerMovementAnimation : MonoBehaviour
    {
        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveZ = Animator.StringToHash("MoveZ");

        [SerializeField] private PlayerController playerController;
        [SerializeField, Min(0f)] private float blendDamping = 0.08f;
        private Animator animator;

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

            animator.SetFloat(MoveX, movement.x, blendDamping, Time.deltaTime);
            animator.SetFloat(MoveZ, movement.z, blendDamping, Time.deltaTime);
        }

        private void OnDisable()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            animator.SetFloat(MoveX, 0f);
            animator.SetFloat(MoveZ, 0f);
        }
    }
}
