using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Mono.Camera
{
    [DisallowMultipleComponent]
    public sealed class CombatCameraFeedback : MonoBehaviour
    {
        private UnityEngine.Camera view;
        private CombatFeedbackSettings settings;
        private Vector3 kick, kickVelocity, appliedOffset;
        private float trauma, fovOffset, baselineFov;
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
            trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, intensity) * .55f);
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
            Advance(Time.unscaledDeltaTime);
        }
        internal void Advance(float dt)
        {
            if (settings == null) return;
            float phase = Time.unscaledTime * 47f;
            Vector3 noise = new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * 1.37f), 0f)
                * (settings.Shake * trauma * trauma);
            appliedOffset = Vector3.ClampMagnitude(kick + transform.TransformDirection(noise),
                settings.MaximumCameraOffset) * settings.CameraMotionScale;
            transform.position += appliedOffset;
            // Analytic damped spring: one brief counter-kick, stable even after a long frame.
            float frequency = 1.8f / Mathf.Max(.01f, settings.CameraRecovery);
            float damping = 1.35f / Mathf.Max(.01f, settings.CameraRecovery);
            float decay = Mathf.Exp(-damping * dt);
            float sine = Mathf.Sin(frequency * dt), cosine = Mathf.Cos(frequency * dt);
            Vector3 quadrature = (kickVelocity + damping * kick) / frequency;
            Vector3 next = decay * (kick * cosine + quadrature * sine);
            kickVelocity = decay * (-kick * frequency * sine + quadrature * frequency * cosine) - damping * next;
            kick = next;
            if (kick.sqrMagnitude + kickVelocity.sqrMagnitude < .000001f)
                kick = kickVelocity = Vector3.zero;
            trauma = Mathf.MoveTowards(trauma, 0f, dt / Mathf.Max(.01f, settings.CameraRecovery * 2.5f));
            fovOffset = Mathf.Lerp(fovOffset, Dashing ? settings.DashFovDelta : 0f,
                1f - Mathf.Exp(-dt / Mathf.Max(.01f, settings.DashFovTransition)));
            if (view != null) view.fieldOfView = baselineFov + fovOffset;
        }
        public void ResetFeedback()
        {
            RemoveOffset();
            kick = kickVelocity = Vector3.zero;
            trauma = fovOffset = 0f;
            Dashing = false;
            if (view != null) view.fieldOfView = baselineFov;
        }
        private void OnDisable() => ResetFeedback();
    }
}
