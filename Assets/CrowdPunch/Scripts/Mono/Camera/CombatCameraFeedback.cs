using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Mono.Camera
{
    [DisallowMultipleComponent]
    public sealed class CombatCameraFeedback : MonoBehaviour
    {
        private UnityEngine.Camera view;
        private CombatFeedbackSettings settings;
        private Vector3 kick, appliedOffset;
        private float shake, fovOffset, baselineFov;
        public bool Dashing { get; set; }
        public void Configure(CombatFeedbackSettings value)
        {
            settings = value;
            view = GetComponent<UnityEngine.Camera>();
            if (view != null) baselineFov = view.fieldOfView;
        }
        public void Impulse(Vector3 direction, float strength, float intensity)
        {
            if (settings == null) return;
            kick = Vector3.ClampMagnitude(kick + direction.normalized * strength, settings.MaximumCameraOffset);
            shake = Mathf.Min(settings.MaximumCameraOffset, Mathf.Max(shake, settings.Shake * intensity));
        }
        // Called by CameraFollow around its normal update so feedback cannot enter its smoothing history.
        public void RemoveOffset()
        {
            transform.position -= appliedOffset;
            appliedOffset = Vector3.zero;
        }
        public void ApplyOffset()
        {
            if (settings == null) return;
            float dt = Time.unscaledDeltaTime;
            float decay = Mathf.Exp(-dt / Mathf.Max(.01f, settings.CameraRecovery));
            float phase = Time.unscaledTime * 47f;
            Vector3 noise = new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * 1.37f), 0f) * shake;
            appliedOffset = Vector3.ClampMagnitude(kick + transform.TransformDirection(noise), settings.MaximumCameraOffset);
            transform.position += appliedOffset;
            kick *= decay;
            shake *= decay;
            fovOffset = Mathf.Lerp(fovOffset, Dashing ? settings.DashFovDelta : 0f,
                1f - Mathf.Exp(-dt / Mathf.Max(.01f, settings.DashFovTransition)));
            if (view != null) view.fieldOfView = baselineFov + fovOffset;
        }
        public void ResetFeedback()
        {
            RemoveOffset();
            kick = Vector3.zero;
            shake = fovOffset = 0f;
            Dashing = false;
            if (view != null) view.fieldOfView = baselineFov;
        }
        private void OnDisable() => ResetFeedback();
    }
}
