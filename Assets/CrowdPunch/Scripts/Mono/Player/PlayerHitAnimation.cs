using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerHitAnimation : MonoBehaviour
    {
        private static readonly int Hit = Animator.StringToHash("Hit");
        private static readonly int Empty = Animator.StringToHash("Hit Reaction.Empty");
        [SerializeField] private PlayerHealth playerHealth;
        private Animator animator;
        private PlayerPunch playerPunch;
        private bool reactionCancelled = true;
        private int layer = -1;

        public float ReactionWeight
        {
            get
            {
                if (reactionCancelled || !isActiveAndEnabled || animator == null || layer < 0)
                    return 0f;
                bool reacting = animator.GetCurrentAnimatorStateInfo(layer).IsName("Hit To Body");
                if (!animator.IsInTransition(layer))
                    return reacting ? 1f : 0f;
                float progress = Mathf.Clamp01(animator.GetAnimatorTransitionInfo(layer).normalizedTime);
                bool entering = animator.GetNextAnimatorStateInfo(layer).IsName("Hit To Body");
                return Mathf.Lerp(reacting ? 1f : 0f, entering ? 1f : 0f, progress);
            }
        }

        private void Awake()
        {
            if (playerHealth == null)
                playerHealth = GetComponent<PlayerHealth>();
            animator = GetComponentInChildren<Animator>();
            playerPunch = GetComponent<PlayerPunch>();
            if (animator != null && animator.runtimeAnimatorController != null)
                layer = animator.GetLayerIndex("Hit Reaction");
        }

        private void OnEnable()
        {
            if (playerHealth != null)
                playerHealth.DamageAccepted += PlayHit;
            if (playerPunch != null)
                playerPunch.PunchStarted += CancelReaction;
        }

        private void OnDisable()
        {
            if (playerHealth != null)
                playerHealth.DamageAccepted -= PlayHit;
            if (playerPunch != null)
                playerPunch.PunchStarted -= CancelReaction;
            CancelReaction();
        }

        private void CancelReaction()
        {
            // PLAYER-005: accepted punches immediately regain presentation priority.
            reactionCancelled = true;
            if (animator != null && animator.runtimeAnimatorController != null && layer >= 0)
            {
                animator.ResetTrigger(Hit);
                animator.Play(Empty, layer, 0f);
                animator.SetLayerWeight(layer, 0f);
            }
        }

        private void Update()
        {
            if (animator != null && layer >= 0)
                animator.SetLayerWeight(layer, ReactionWeight);
        }

        private void PlayHit(Vector3 impulse)
        {
            // Only accepted hits trigger presentation; invulnerability remains health-owned.
            if (animator != null && animator.isActiveAndEnabled && layer >= 0)
            {
                reactionCancelled = false;
                animator.SetLayerWeight(layer, 1f);
                animator.SetTrigger(Hit);
            }
        }
    }
}
