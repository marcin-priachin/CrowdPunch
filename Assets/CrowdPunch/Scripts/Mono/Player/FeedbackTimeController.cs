using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    // The pause menu and additive scene transition have separate gates. Neither captures
    // a transient hit-stop scale as its future resume value. Fixed timestep is untouched.
    [DefaultExecutionOrder(-300)]
    public sealed class FeedbackTimeController : MonoBehaviour
    {
        private static FeedbackTimeController instance;
        private double freezeUntil, slowUntil;
        private float slowScale = 1f, baseline = 1f;
        private bool paused, transitioning;
        public static bool IsSuspended => instance != null && (instance.paused || instance.transitioning);

        private void Awake()
        {
            if (instance != null && instance != this) { enabled = false; return; }
            instance = this;
            baseline = Time.timeScale > 0f ? Time.timeScale : 1f;
        }
        private void OnEnable() { if (instance == null) instance = this; }
        private void Update() => Apply();
        public static void Freeze(float duration)
        {
            if (instance == null || IsSuspended || duration <= 0f) return;
            instance.freezeUntil = System.Math.Max(instance.freezeUntil, Time.unscaledTimeAsDouble + Mathf.Clamp(duration, 0f, .15f));
            instance.Apply();
        }
        public static void Slow(float scale, float duration)
        {
            if (instance == null || IsSuspended || duration <= 0f) return;
            if (Time.unscaledTimeAsDouble >= instance.slowUntil) instance.slowScale = 1f;
            instance.slowScale = Mathf.Min(instance.slowScale, Mathf.Clamp(scale, .01f, 1f));
            instance.slowUntil = System.Math.Max(instance.slowUntil, Time.unscaledTimeAsDouble + Mathf.Clamp(duration, 0f, .3f));
            instance.Apply();
        }
        public static void SetPaused(bool value)
        {
            if (instance == null) { Time.timeScale = value ? 0f : 1f; return; }
            instance.paused = value;
            CancelEffects();
        }
        public static void SetTransition(bool value)
        {
            if (instance == null) { Time.timeScale = value ? 0f : 1f; return; }
            instance.transitioning = value;
            CancelEffects();
        }
        public static void CancelEffects()
        {
            if (instance == null) return;
            instance.freezeUntil = instance.slowUntil = 0;
            instance.slowScale = 1f;
            instance.Apply();
        }
        internal static float ResolveScale(float baseline, bool blocked, double now,
            double freezeEnd, double slowEnd, float slowScale)
            => blocked || now < freezeEnd ? 0f : baseline * (now < slowEnd ? slowScale : 1f);
        private void Apply() => Time.timeScale = ResolveScale(baseline, paused || transitioning,
            Time.unscaledTimeAsDouble, freezeUntil, slowUntil, slowScale);
        private void OnDisable()
        {
            if (instance != this) return;
            Time.timeScale = baseline;
            instance = null;
        }
    }
}
