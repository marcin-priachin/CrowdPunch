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
        private InputAction attackAction;

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
        }

        private void OnEnable()
        {
            attackAction?.Enable();
        }

        private void OnDisable()
        {
            attackAction?.Disable();
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
                settings.DirectionPositionWeight);
        }

        public void RequestPunch()
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
                settings.Damage,
                settings.DirectionPositionWeight);
        }
    }
}
