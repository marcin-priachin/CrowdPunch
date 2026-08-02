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
        [SerializeField] private InputActionReference attackAction;
        [SerializeField] private Transform punchOrigin;
        [SerializeField] private float punchRadius = 2f;
        [SerializeField] private float punchRange = 3f;
        [SerializeField] private float punchStrength = 12f;
        [SerializeField] private float punchDamage = 10f;
        [SerializeField, Range(0f, 1f)] private float pushDirectionPositionWeight = 1f;

        private PunchTrajectoryPreview trajectoryPreview;

        public float PunchRadius => punchRadius;

        private void Reset()
        {
            ecsBridge = GetComponent<PlayerEcsBridge>();
        }

        private void Awake()
        {
            if (ecsBridge == null)
            {
                ecsBridge = GetComponent<PlayerEcsBridge>();
            }

            trajectoryPreview = GetComponent<PunchTrajectoryPreview>();
            if (trajectoryPreview == null)
            {
                trajectoryPreview = gameObject.AddComponent<PunchTrajectoryPreview>();
            }
        }

        private void OnEnable()
        {
            attackAction?.action.Enable();
        }

        private void OnDisable()
        {
            attackAction?.action.Disable();
            ecsBridge?.ClearPunchPreview();
        }

        private void Update()
        {
            PublishPunchPreview();

            if (attackAction == null || !attackAction.action.WasPressedThisFrame())
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
                punchRadius,
                punchRange,
                trajectoryPreview.LineLength,
                pushDirectionPositionWeight);
        }

        public void RequestPunch()
        {
            Transform originTransform = punchOrigin != null ? punchOrigin : transform;
            Vector3 origin = originTransform.position;
            Vector3 direction = originTransform.forward;

            ecsBridge.PublishPunch(
                origin,
                direction,
                punchRadius,
                punchRange,
                punchStrength,
                punchDamage,
                pushDirectionPositionWeight);
        }
    }
}
