using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [DefaultExecutionOrder(100)]
    public sealed class PlayerPunchAnimation : MonoBehaviour
    {
        private static readonly int PunchTime = Animator.StringToHash("PunchTime");

        [SerializeField] private PlayerPunch playerPunch;
        [SerializeField] private AnimationClip punchClip;
        [SerializeField, Min(0.01f)]
        [Tooltip("Post-contact playback multiplier while awaiting a result or when cooldown is zero. Active cooldown timing comes from PlayerPunchSettings.")]
        private float punchSpeedMultiplier = 1f;

        [Header("Clip poses (frames in the assigned clip)")]
        [SerializeField, Min(0f)] private float readyFrame = 22f;
        [SerializeField, Min(0f)] private float punchRotationEndFrame = 34f;

        [Header("Impact exaggeration (presentation only)")]
        [SerializeField, Min(0f)] private float contactFrame = 32f;
        [SerializeField, Min(.1f)] private float strikeSpeed = 1.85f;
        [SerializeField, Min(.1f)] private float followThroughSpeed = .85f;
        [SerializeField] private bool snapToContactOnHit = true;

        public void ConfirmContactPose()
        {
            if (!snapToContactOnHit || !HasAnimation() || !isActiveAndEnabled) return;
            RestoreAnimatedRotation();
            playbackTime = ContactTime;
            playingPunch = playerPunch == null || !playerPunch.IsCoolingDown;
            SetPose(playbackTime);
            // Evaluate the existing time-parameter punch layer before freezing the contact frame.
            animator.Update(0f);
        }

        [Header("Upper Body Rotation")]
        [SerializeField, Range(-180f, 180f)]
        [Tooltip("Y rotation offset in degrees at the ready frame, held while ready to punch.")]
        private float readyYaw;
        [SerializeField, Range(-180f, 180f)]
        [Tooltip("Y rotation offset reached at the rotation end frame and held through the rest of the punch.")]
        private float punchYaw;

        [SerializeField, HideInInspector] private Quaternion[] spineReferenceRotations;

        private Animator animator;
        private PlayerHitAnimation hitAnimation;
        private Transform spine;
        private Quaternion animatedSpineRotation;
        private bool hasRotationOffset;
        private float upperBodyYaw;
        private float playbackTime;
        private bool playingPunch;

        private float ReadyTime => Mathf.Min(readyFrame / punchClip.frameRate, punchClip.length);
        private float ContactTime => Mathf.Clamp(contactFrame / punchClip.frameRate, ReadyTime, punchClip.length);
        private float PunchRotationEndTime => Mathf.Clamp(punchRotationEndFrame / punchClip.frameRate, ReadyTime, punchClip.length);

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator.isHuman)
            {
                spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            }
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
                playerPunch.CooldownStarted += StartCooldown;
                playerPunch.PunchStateReset += ResetPose;
            }

            ResetPose();
        }

        private void OnDisable()
        {
            RestoreAnimatedRotation();
            if (playerPunch != null)
            {
                playerPunch.PunchStarted -= StartPunch;
                playerPunch.CooldownStarted -= StartCooldown;
                playerPunch.PunchStateReset -= ResetPose;
            }

            ResetPose();
        }

        private void Update()
        {
            // Remove last frame's offset before Animator evaluates a fresh pose.
            RestoreAnimatedRotation();
            if (!HasAnimation())
            {
                return;
            }

            if (playerPunch != null && playerPunch.IsCoolingDown)
            {
                playingPunch = false;
                SetCooldownPose();
                return;
            }

            if (playingPunch)
            {
                playbackTime = Mathf.Min(punchClip.length,
                    playbackTime + Time.deltaTime * Mathf.Max(0.01f, punchSpeedMultiplier)
                    * (playbackTime < PunchRotationEndTime ? strikeSpeed : followThroughSpeed));
                SetPose(playbackTime);
                playingPunch = playbackTime < punchClip.length;
                return;
            }

            SetPose(ReadyTime);
        }

        private void StartPunch()
        {
            if (!HasAnimation())
            {
                return;
            }

            // Accepted input skips the strike wind-up, independently of hit-confirmation feedback.
            RestoreAnimatedRotation();
            playbackTime = ContactTime;
            playingPunch = true;
            SetPose(playbackTime);
            animator.Update(0f);
        }

        private void ResetPose()
        {
            playingPunch = false;
            if (HasAnimation())
            {
                SetCooldownPose();
            }
        }

        private void StartCooldown()
        {
            RestoreAnimatedRotation();
            playingPunch = false;
            if (HasAnimation())
                SetCooldownPose();
        }

        private void SetCooldownPose()
        {
            // PLAYER-008/009: finish the strike, wrap, then wind up to ready on the selected cooldown clock.
            float progress = playerPunch == null ? 1f : playerPunch.CooldownProgress;
            float time = ContactTime + progress * (punchClip.length - ContactTime + ReadyTime);
            if (time > punchClip.length)
                time -= punchClip.length;
            SetPose(progress >= 1f ? ReadyTime : time);
        }

        private bool HasAnimation()
        {
            return animator != null && animator.runtimeAnimatorController != null
                && punchClip != null && punchClip.length > 0f && punchClip.frameRate > 0f;
        }

        private void SetPose(float time)
        {
            animator.SetFloat(PunchTime, Mathf.Clamp01(time / punchClip.length));
            upperBodyYaw = time <= ReadyTime
                ? Mathf.LerpAngle(punchYaw, readyYaw, Mathf.InverseLerp(0f, ReadyTime, time))
                : Mathf.LerpAngle(readyYaw, punchYaw, Mathf.InverseLerp(ReadyTime, PunchRotationEndTime, time));
        }

        private void LateUpdate()
        {
            RestoreAnimatedRotation();
            if (!HasAnimation() || !animator.isActiveAndEnabled || spine == null)
            {
                return;
            }

            // The humanoid spine's local Y need not be vertical. Rotate around model up
            // after animation, leaving root facing, legs, and punch targeting untouched.
            animatedSpineRotation = spine.localRotation;
            if (hitAnimation == null)
                hitAnimation = GetComponentInParent<PlayerHitAnimation>();
            float yawWeight = hitAnimation == null ? 1f : 1f - hitAnimation.ReactionWeight;
            if (spineReferenceRotations != null && spineReferenceRotations.Length > 1)
            {
                // A humanoid upper-body mask retains the locomotion hips' rotation.
                // Anchor the spine to the idle-relative strike pose, leaving hips/legs animated.
                float sample = Mathf.Clamp01(animator.GetFloat(PunchTime)) * (spineReferenceRotations.Length - 1);
                int first = Mathf.FloorToInt(sample);
                Quaternion reference = Quaternion.Slerp(spineReferenceRotations[first],
                    spineReferenceRotations[Mathf.Min(first + 1, spineReferenceRotations.Length - 1)], sample - first);
                spine.rotation = Quaternion.Slerp(spine.rotation, animator.transform.rotation * reference, yawWeight);
            }
            spine.rotation = Quaternion.AngleAxis(upperBodyYaw * yawWeight, animator.transform.up) * spine.rotation;
            hasRotationOffset = true;
        }

        private void RestoreAnimatedRotation()
        {
            if (hasRotationOffset && spine != null)
            {
                spine.localRotation = animatedSpineRotation;
            }

            hasRotationOffset = false;
        }
    }
}
