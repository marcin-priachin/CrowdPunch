using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [DefaultExecutionOrder(100)]
    public sealed class PlayerPunchAnimation : MonoBehaviour
    {
        private const float ReadyFrame = 22f;
        private static readonly int PunchTime = Animator.StringToHash("PunchTime");

        [SerializeField] private PlayerPunch playerPunch;
        [SerializeField] private AnimationClip punchClip;
        [SerializeField, Min(0.01f)]
        [Tooltip("Playback multiplier for frames 22 through the end of the punch. Does not change gameplay cooldown or hit timing.")]
        private float punchSpeedMultiplier = 1f;

        private Animator animator;
        private float playbackTime;
        private bool playingPunch;

        private float ReadyTime => Mathf.Min(ReadyFrame / punchClip.frameRate, punchClip.length);

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (playerPunch == null)
            {
                playerPunch = GetComponentInParent<PlayerPunch>();
            }
        }

        private void OnEnable()
        {
            if (playerPunch != null)
            {
                playerPunch.PunchStarted += StartPunch;
                playerPunch.PunchStateReset += ResetPose;
            }

            ResetPose();
        }

        private void OnDisable()
        {
            if (playerPunch != null)
            {
                playerPunch.PunchStarted -= StartPunch;
                playerPunch.PunchStateReset -= ResetPose;
            }

            ResetPose();
        }

        private void Update()
        {
            if (!HasAnimation())
            {
                return;
            }

            if (playingPunch)
            {
                playbackTime = Mathf.Min(punchClip.length,
                    playbackTime + Time.deltaTime * Mathf.Max(0.01f, punchSpeedMultiplier));
                SetPose(playbackTime);
                playingPunch = playbackTime < punchClip.length;
                return;
            }

            // PLAYER-009: follow the authoritative hit-confirmed cooldown after the strike finishes.
            SetPose(ReadyTime * (playerPunch == null ? 1f : playerPunch.CooldownProgress));
        }

        private void StartPunch()
        {
            if (!HasAnimation())
            {
                return;
            }

            playbackTime = ReadyTime;
            playingPunch = true;
            SetPose(playbackTime);
        }

        private void ResetPose()
        {
            playingPunch = false;
            if (HasAnimation())
            {
                SetPose(ReadyTime * (playerPunch == null ? 1f : playerPunch.CooldownProgress));
            }
        }

        private bool HasAnimation()
        {
            return animator != null && animator.runtimeAnimatorController != null
                && punchClip != null && punchClip.length > 0f && punchClip.frameRate > 0f;
        }

        private void SetPose(float time)
        {
            animator.SetFloat(PunchTime, Mathf.Clamp01(time / punchClip.length));
        }
    }
}
