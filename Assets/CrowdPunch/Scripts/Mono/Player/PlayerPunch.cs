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
        [SerializeField] private Transform punchOrigin;
        [SerializeField] private PlayerPunchSettings settings;

        private PunchTrajectoryPreview trajectoryPreview;
        private PunchAreaFeedback areaFeedback;
        private InputAction attackAction;
        private float nextPunchTime;

        public float PunchRadius => settings == null ? 0f : settings.Radius;

        private void Reset()
        {
            ecsBridge = GetComponent<PlayerEcsBridge>();
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
        }

        private void OnDisable()
        {
            attackAction?.Disable();
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
                return;
            }

            PublishPunch();
        }

        public void ResetPunchState()
        {
            nextPunchTime = 0f;
            areaFeedback?.Hide();
            ecsBridge?.ClearPunch();
        }

        private bool CanPunch()
        {
            return isActiveAndEnabled && ecsBridge != null && settings != null && Time.time >= nextPunchTime;
        }

        private void PublishPunch()
        {
            Transform originTransform = punchOrigin != null ? punchOrigin : transform;
            Vector3 origin = originTransform.position;
            Vector3 direction = originTransform.forward;

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
                settings.Strength,
                settings.EliteKnockbackMultiplier,
                settings.Damage,
                settings.DirectionPositionWeight);
            nextPunchTime = Time.time + settings.Cooldown;
        }
    }
}
