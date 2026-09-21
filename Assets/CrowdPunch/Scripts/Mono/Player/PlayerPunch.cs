using System;
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
        private PlayerPunchCooldownFeedback cooldownFeedback;
        private InputAction attackAction;
        private float nextPunchTime;
        private bool awaitingPunchResult;
        private uint pendingPunchSequence;

        public float PunchRadius => settings == null ? 0f : settings.Radius;
        public event Action PunchStarted;
        public event Action PunchStateReset;
        public float CooldownProgress => settings == null || settings.Cooldown <= 0f
            ? 1f
            : 1f - Mathf.Clamp01((nextPunchTime - Time.time) / settings.Cooldown);

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

            cooldownFeedback = GetComponent<PlayerPunchCooldownFeedback>();
            if (cooldownFeedback == null)
            {
                cooldownFeedback = gameObject.AddComponent<PlayerPunchCooldownFeedback>();
            }
            cooldownFeedback.Initialize(settings);
        }

        private void OnEnable()
        {
            if (ecsBridge != null)
                ecsBridge.PunchResolved += OnPunchResolved;
            attackAction?.Enable();
            cooldownFeedback?.ShowReady();
        }

        private void OnDisable()
        {
            if (ecsBridge != null)
                ecsBridge.PunchResolved -= OnPunchResolved;
            awaitingPunchResult = false;
            ecsBridge?.ClearPunch();
            attackAction?.Disable();
            cooldownFeedback?.Hide();
            ecsBridge?.ClearPunchPreview();
            PunchStateReset?.Invoke();
        }

        private void Update()
        {
            PublishPunchPreview();
            UpdateCooldownFeedback();

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
                settings.AimAssistRange,
                settings.AimAssistMaximumAngleDegrees);
        }

        private void UpdateCooldownFeedback()
        {
            cooldownFeedback?.SetCooldownState(CooldownProgress, Time.time < nextPunchTime);
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
            awaitingPunchResult = false;
            nextPunchTime = 0f;
            cooldownFeedback?.ShowReady();
            ecsBridge?.ClearPunch();
            PunchStateReset?.Invoke();
        }

        private bool CanPunch()
        {
            return isActiveAndEnabled && ecsBridge != null && settings != null
                && !awaitingPunchResult && Time.time >= nextPunchTime;
        }

        private void PublishPunch()
        {
            Transform originTransform = punchOrigin != null ? punchOrigin : transform;
            Vector3 origin = originTransform.position;
            Vector3 direction = originTransform.forward;

            ecsBridge.PublishPunch(
                origin,
                direction,
                settings.Radius,
                settings.Range,
                settings.Strength,
                settings.EliteKnockbackMultiplier,
                settings.Damage,
                settings.DirectionPositionWeight);
            pendingPunchSequence = ecsBridge.PunchSequence;
            awaitingPunchResult = true;
            PunchStarted?.Invoke();
        }

        private void OnPunchResolved(uint sequence, bool hitEnemy)
        {
            if (!awaitingPunchResult || sequence != pendingPunchSequence)
                return;

            awaitingPunchResult = false;
            // PLAYER-009: only confirmed enemy hits spend the punch cooldown.
            if (hitEnemy)
                nextPunchTime = Time.time + settings.Cooldown;
            UpdateCooldownFeedback();
        }
    }
}
