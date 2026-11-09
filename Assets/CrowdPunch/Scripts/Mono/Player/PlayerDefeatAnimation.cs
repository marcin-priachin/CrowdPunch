using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerDefeatAnimation : MonoBehaviour
    {
        private static readonly int KnockedOut = Animator.StringToHash("Defeat.Knocked Out");
        private PlayerHealth health;
        private Animator animator;
        private int layer;
        private bool playing;
        private AnimatorUpdateMode updateMode;
        private AnimatorCullingMode cullingMode;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
            animator = GetComponentInChildren<Animator>(true);
            layer = animator != null && animator.runtimeAnimatorController != null ? animator.GetLayerIndex("Defeat") : -1;
        }

        private void OnEnable()
        {
            health.Changed += OnHealthChanged;
            OnHealthChanged(health.CurrentHealth, health.MaxHealth);
        }

        private void OnDisable()
        {
            health.Changed -= OnHealthChanged;
            RestoreAnimation();
        }

        private void OnHealthChanged(float current, float maximum)
        {
            if (current > 0f)
            {
                RestoreAnimation();
                return;
            }
            if (playing || layer < 0 || maximum <= 0f || !animator.isActiveAndEnabled) return;

            playing = true;
            updateMode = animator.updateMode;
            cullingMode = animator.cullingMode;
            // The arena pauses immediately, and the camera may start outside the player's view.
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.SetLayerWeight(layer, 1f);
            animator.Play(KnockedOut, layer, 0f);
        }

        private void RestoreAnimation()
        {
            if (!playing) return;
            playing = false;
            if (animator == null) return;
            animator.SetLayerWeight(layer, 0f);
            animator.updateMode = updateMode;
            animator.cullingMode = cullingMode;
        }
    }
}
