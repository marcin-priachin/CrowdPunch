using CrowdPunch.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CrowdPunch.Mono.Player
{
    /// <summary>
    /// Traditional GameObject punch input and timing component.
    /// </summary>
    [RequireComponent(typeof(PlayerEcsBridge))]
    public sealed class PlayerPunch : MonoBehaviour
    {
        [SerializeField] private PlayerEcsBridge ecsBridge;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Transform punchOrigin;
        [SerializeField] private PlayerPunchSettings settings;

        private PunchTrajectoryPreview trajectoryPreview;
        private PunchAreaFeedback areaFeedback;
        private InputAction attackAction;
        private bool hasBufferedDashPunch;
        private float nextPunchTime;

        public float PunchRadius => settings == null ? 0f : settings.Radius;

        private void Reset()
        {
            ecsBridge = GetComponent<PlayerEcsBridge>();
            playerController = GetComponent<PlayerController>();
        }

        private void Awake()
        {
            if (settings == null)
            {
                Debug.LogError($"{nameof(PlayerPunch)} requires {nameof(PlayerPunchSettings)}.", this);
                enabled = false;
                return;
            }

            if (ecsBridge == null)
            {
                ecsBridge = GetComponent<PlayerEcsBridge>();
            }

            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }

            attackAction = settings.FindAttackAction();

            trajectoryPreview = GetComponent<PunchTrajectoryPreview>();
            if (trajectoryPreview == null)
            {
                trajectoryPreview = gameObject.AddComponent<PunchTrajectoryPreview>();
            }

            areaFeedback = GetComponent<PunchAreaFeedback>();
            if (areaFeedback == null)
            {
                areaFeedback = gameObject.AddComponent<PunchAreaFeedback>();
            }
        }

        private void OnEnable()
        {
            attackAction?.Enable();
            if (playerController != null)
            {
                playerController.DashStarted += ClearBufferedDashPunch;
                playerController.DashPunchMidpointReached += TryConsumeBufferedDashPunch;
                playerController.DashEnded += ClearBufferedDashPunch;
            }
        }

        private void OnDisable()
        {
            attackAction?.Disable();
            if (playerController != null)
            {
                playerController.DashStarted -= ClearBufferedDashPunch;
                playerController.DashPunchMidpointReached -= TryConsumeBufferedDashPunch;
                playerController.DashEnded -= ClearBufferedDashPunch;
            }

            ClearBufferedDashPunch();
            areaFeedback?.Hide();
            ecsBridge?.ClearPunchPreview();
        }

        private void Update()
        {
            PublishPunchPreview();

            if (attackAction == null || !attackAction.WasPressedThisFrame())
            {
                return;
            }

            RequestPunch();
        }

        private void PublishPunchPreview()
        {
            Transform originTransform = punchOrigin != null ? punchOrigin : transform;
            ecsBridge.PublishPunchPreview(
                originTransform.position,
                originTransform.forward,
                settings.Radius,
                settings.Range,
                trajectoryPreview.LineLength,
                settings.DirectionPositionWeight,
                settings.AimAssistRange);
        }

        public void RequestPunch()
        {
            if (!CanPunch())
            {
                ClearBufferedDashPunch();
                return;
            }

            if (playerController != null && playerController.IsDashing)
            {
                if (!playerController.TryCancelDashForPunch(out Vector3 dashPunchDirection))
                {
                    hasBufferedDashPunch = true;
                    return;
                }

                ClearBufferedDashPunch();
                PublishPunch(dashPunchDirection, true);
                return;
            }

            ClearBufferedDashPunch();
            PublishPunch();
        }

        private void TryConsumeBufferedDashPunch()
        {
            if (!hasBufferedDashPunch)
            {
                return;
            }

            hasBufferedDashPunch = false;
            if (!CanPunch() || playerController == null ||
                !playerController.TryCancelDashForPunch(out Vector3 dashPunchDirection))
            {
                return;
            }

            PublishPunch(dashPunchDirection, true);
        }

        private void ClearBufferedDashPunch()
        {
            hasBufferedDashPunch = false;
        }

        public void ResetPunchState()
        {
            nextPunchTime = 0f;
            ClearBufferedDashPunch();
            areaFeedback?.Hide();
            ecsBridge?.ClearPunch();
        }

        private bool CanPunch()
        {
            return isActiveAndEnabled && ecsBridge != null && settings != null && Time.time >= nextPunchTime;
        }

        private void PublishPunch(Vector3? directionOverride = null, bool isDashPunch = false)
        {
            Transform originTransform = punchOrigin != null ? punchOrigin : transform;
            Vector3 origin = originTransform.position;
            Vector3 direction = directionOverride ?? originTransform.forward;

            areaFeedback.Show(
                origin,
                direction,
                settings.Radius,
                settings.Range,
                settings.AreaFeedbackDuration);

            ecsBridge.PublishPunch(
                origin,
                direction,
                settings.Radius,
                settings.Range,
                isDashPunch ? settings.DashStrength : settings.Strength,
                settings.EliteKnockbackMultiplier,
                isDashPunch ? settings.DashDamage : settings.Damage,
                settings.DirectionPositionWeight);
            nextPunchTime = Time.time + settings.Cooldown;
        }
    }
}
