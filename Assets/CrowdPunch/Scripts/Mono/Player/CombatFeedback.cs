using CrowdPunch.Components;
using CrowdPunch.Configuration;
using CrowdPunch.Mono.Camera;
using CrowdPunch.Mono.UI;
using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    /// <summary>Scene presentation endpoint. Owns bounded pools and consumes bridge/player events only.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class CombatFeedback : MonoBehaviour
    {
        private PlayerEcsBridge bridge;
        private PlayerHealth health;
        private PlayerController controller;
        private PlayerPunch punch;
        private PlayerPunchAnimation punchAnimation;
        private CombatFeedbackSettings settings;
        private CombatCameraFeedback cameraFeedback;
        private ImpactParticlePool particles;
        private ComicImpactPool comicImpacts;
        private UnityEngine.Camera feedbackCamera;
        private DasherTelegraphParticlePool dasherTelegraphs;
        private LaunchedTrailPool trails;
        private PlayerDamageFlash flash;
        private ExplosionFeedback explosions;
        private Material particleMaterial, flashMaterial;
        private Transform poolRoot;
        private float flashRemaining, dashRemaining;
        private Vector3 dashDirection;
        private bool dashEndPending;
        private double nextCameraTime, nextExceptionalTime, nextDamageTime;
        private uint restart, observedPunch;
        private bool bound;

        public void Configure(PlayerEcsBridge player, CombatFeedbackSettings configuration)
        {
            if (bound || player == null || configuration == null) return;
            bridge = player;
            settings = configuration;
            bridge.FeedbackSettings = settings;
            health = player.GetComponent<PlayerHealth>();
            controller = player.GetComponent<PlayerController>();
            punch = player.GetComponent<PlayerPunch>();
            punchAnimation = player.GetComponentInChildren<PlayerPunchAnimation>(true);
            var camera = UnityEngine.Camera.main;
            feedbackCamera = camera;
            if (camera != null)
            {
                cameraFeedback = camera.GetComponent<CombatCameraFeedback>();
                if (cameraFeedback == null) cameraFeedback = camera.gameObject.AddComponent<CombatCameraFeedback>();
                cameraFeedback.Configure(settings);
            }
            poolRoot = new GameObject("Combat feedback pool").transform;
            poolRoot.SetParent(transform, false);
            particleMaterial = new Material(Resources.Load<Shader>("Shaders/CombatFeedback"));
            flashMaterial = new Material(Resources.Load<Shader>("Shaders/ExplosionFeedback"));
            particles = new ImpactParticlePool(poolRoot, settings, particleMaterial);
            comicImpacts = new ComicImpactPool(poolRoot, settings, particleMaterial);
            dasherTelegraphs = new DasherTelegraphParticlePool(poolRoot, settings, particleMaterial);
            trails = new LaunchedTrailPool(poolRoot, settings, particleMaterial);
            flash = new PlayerDamageFlash(player.transform, flashMaterial);
            bridge.ImpactReceived += OnImpact;
            bridge.PunchResolved += OnPunchResolved;
            bridge.TrailsBegan += trails.Begin;
            bridge.TrailReceived += trails.Show;
            bridge.TrailsEnded += trails.End;
            bridge.DasherTelegraphsBegan += dasherTelegraphs.Begin;
            bridge.DasherTelegraphReceived += dasherTelegraphs.Show;
            bridge.DasherTelegraphsEnded += dasherTelegraphs.End;
            if (health != null) health.DamageAccepted += OnDamage;
            if (controller != null) { controller.DashStarted += OnDashStart; controller.DashEnded += OnDashEnd; }
            if (punch != null) punch.PunchStateReset += ResetFeedback;
            observedPunch = bridge.PunchSequence;
            bound = true;
        }
        private void OnPunchResolved(uint sequence, bool hit)
        {
            if (!isActiveAndEnabled || sequence != bridge.PunchSequence || sequence == observedPunch) return;
            observedPunch = sequence;
            if (!hit || FeedbackTimeController.IsSuspended || !bridge.gameObject.activeInHierarchy) return;
            punchAnimation?.ConfirmContactPose();
            cameraFeedback?.Impulse(bridge.PunchDirection, settings.PunchKick, 1f);
            FeedbackTimeController.Freeze(settings.PunchHitStop);
        }
        private void OnImpact(CombatFeedbackMessage message)
        {
            if (!isActiveAndEnabled || FeedbackTimeController.IsSuspended || !bridge.gameObject.activeInHierarchy) return;
            particles.Show(message.Kind, message.Position, message.Direction, message.Intensity);
            // Volume punches report the enemy's ground-plane origin, not a surface manifold.
            Vector3 accentPosition = message.Position + (message.Kind == CombatImpactKind.Punch ? Vector3.up * .65f : Vector3.zero);
            comicImpacts.Show(message.Kind, accentPosition, message.Direction, message.Intensity);
            if (message.Kind != CombatImpactKind.EnemyCollision || cameraFeedback == null) return;
            float distance = Vector3.Distance(message.Position, bridge.transform.position);
            float attenuation = Mathf.Clamp01(1f - distance / Mathf.Max(1f, settings.CameraImpactRange));
            bool exceptional = message.PlayerOwned && message.ChainDepth >= settings.ExceptionalChainDepth
                && message.Intensity >= settings.CollisionCameraThreshold && Time.unscaledTimeAsDouble >= nextExceptionalTime;
            if (exceptional && attenuation > 0f)
            {
                nextExceptionalTime = Time.unscaledTimeAsDouble + settings.ExceptionalCooldown;
                cameraFeedback.Impulse(message.Direction, settings.ExceptionalKick * attenuation, message.Intensity);
                if (settings.ExceptionalSlowMotion) FeedbackTimeController.Slow(settings.SlowMotionScale, settings.SlowMotionDuration);
            }
            else if (message.Intensity >= settings.CollisionCameraThreshold && Time.unscaledTimeAsDouble >= nextCameraTime)
                cameraFeedback.Impulse(message.Direction, settings.CollisionKick * message.Intensity * attenuation, message.Intensity * attenuation);
            else return;
            nextCameraTime = Time.unscaledTimeAsDouble + settings.CollisionCameraInterval;
        }
        private void OnDamage(Vector3 impulse)
        {
            if (!isActiveAndEnabled || FeedbackTimeController.IsSuspended || !bridge.gameObject.activeInHierarchy) return;
            Vector3 direction = impulse.sqrMagnitude > .001f ? impulse.normalized : -bridge.transform.forward;
            // Repeated hazard ticks still deal damage; their strongest feedback cannot prolong a freeze.
            if (Time.unscaledTimeAsDouble < nextDamageTime) return;
            nextDamageTime = Time.unscaledTimeAsDouble + .25;
            Vector3 contact = bridge.transform.position - direction * bridge.Radius + Vector3.up * .65f;
            particles.Show(CombatImpactKind.PlayerDamage, contact, -direction, 1f);
            comicImpacts.Show(CombatImpactKind.PlayerDamage, contact, direction, 1f);
            cameraFeedback?.Impulse(direction, settings.DamageKick, 1.4f);
            FeedbackTimeController.Freeze(settings.DamageHitStop);
            if (!settings.ReduceFlashes)
            {
                flashRemaining = settings.FlashDuration;
                flash.Show(settings.PlayerFlashColor, settings.PlayerFlashIntensity);
            }
        }
        private void OnDashStart()
        {
            if (!isActiveAndEnabled || FeedbackTimeController.IsSuspended) return;
            dashDirection = controller.DashDirection;
            dashEndPending = false;
            dashRemaining = settings.DashMovementInterval;
            if (cameraFeedback != null) cameraFeedback.Dashing = true;
            particles.Show(CombatImpactKind.DashStart, bridge.transform.position, -dashDirection, .7f);
        }
        private void OnDashEnd()
        {
            if (!isActiveAndEnabled) return;
            if (cameraFeedback != null) cameraFeedback.Dashing = false;
            // The controller ends its timer before applying the last displacement.
            // Present the stop puff after movement/collision resolution in LateUpdate.
            dashEndPending = bridge.gameObject.activeInHierarchy && !FeedbackTimeController.IsSuspended;
        }
        private void Update()
        {
            if (!bound || bridge == null || flash == null) return;
            if (restart != GameRestartRegistry.Sequence || FeedbackTimeController.IsSuspended)
            {
                restart = GameRestartRegistry.Sequence;
                ResetFeedback();
                return;
            }
            if (cameraFeedback != null) cameraFeedback.Dashing = controller != null && controller.isActiveAndEnabled && controller.IsDashing;
            flashRemaining -= Time.unscaledDeltaTime;
            if (flashRemaining <= 0f || settings.ReduceFlashes || !bridge.gameObject.activeInHierarchy) flash.Clear();
            else flash.Show(settings.PlayerFlashColor, settings.PlayerFlashIntensity * flashRemaining / Mathf.Max(.001f, settings.FlashDuration));
        }
        private void LateUpdate()
        {
            if (!bound) return;
            comicImpacts?.Tick(Time.unscaledDeltaTime, feedbackCamera);
            if (bridge == null || !bridge.gameObject.activeInHierarchy || FeedbackTimeController.IsSuspended) return;
            if (dashEndPending)
            {
                dashEndPending = false;
                particles.Show(CombatImpactKind.DashEnd, bridge.transform.position, -dashDirection, .35f);
            }
            if (controller != null && controller.isActiveAndEnabled && controller.IsDashing && Time.deltaTime > 0f)
            {
                dashRemaining -= Time.deltaTime;
                if (dashRemaining <= 0f)
                {
                    dashRemaining = settings.DashMovementInterval;
                    particles.Show(CombatImpactKind.DashMove, bridge.transform.position,
                        -dashDirection, .35f);
                }
            }
        }
        public void ResetFeedback()
        {
            if (bridge != null) observedPunch = bridge.PunchSequence;
            if (explosions == null && bridge != null) explosions = bridge.GetComponent<ExplosionFeedback>();
            explosions?.Clear();
            flashRemaining = dashRemaining = 0f;
            dashEndPending = false;
            dashDirection = Vector3.zero;
            nextCameraTime = nextExceptionalTime = nextDamageTime = 0;
            particles?.Clear(); comicImpacts?.Clear(); trails?.Clear(); dasherTelegraphs?.Clear(); flash?.Clear();
            if (cameraFeedback != null) cameraFeedback.ResetFeedback();
            FeedbackTimeController.CancelEffects();
        }
        private void OnEnable()
        {
            // Unity retains object references during script reload but not the managed pools.
            if (bridge != null && settings != null && particles == null)
            {
                bound = false;
                if (poolRoot != null) Destroy(poolRoot.gameObject);
                if (particleMaterial != null) Destroy(particleMaterial);
                if (flashMaterial != null) Destroy(flashMaterial);
                Configure(bridge, settings);
            }
            else if (bound && bridge != null)
            {
                bridge.FeedbackSettings = settings;
                if (comicImpacts == null) comicImpacts = new ComicImpactPool(poolRoot, settings, particleMaterial);
            }
        }
        private void OnDisable()
        {
            if (bridge != null) bridge.FeedbackSettings = null;
            ResetFeedback();
            comicImpacts?.Dispose();
            comicImpacts = null;
        }
        private void OnDestroy()
        {
            ResetFeedback();
            if (bridge != null)
            {
                bridge.ImpactReceived -= OnImpact;
                bridge.PunchResolved -= OnPunchResolved;
                if (trails != null) { bridge.TrailsBegan -= trails.Begin; bridge.TrailReceived -= trails.Show; bridge.TrailsEnded -= trails.End; }
                if (dasherTelegraphs != null)
                {
                    bridge.DasherTelegraphsBegan -= dasherTelegraphs.Begin;
                    bridge.DasherTelegraphReceived -= dasherTelegraphs.Show;
                    bridge.DasherTelegraphsEnded -= dasherTelegraphs.End;
                }
                bridge.FeedbackSettings = null;
            }
            if (health != null) health.DamageAccepted -= OnDamage;
            if (controller != null) { controller.DashStarted -= OnDashStart; controller.DashEnded -= OnDashEnd; }
            if (punch != null) punch.PunchStateReset -= ResetFeedback;
            if (particleMaterial != null) Destroy(particleMaterial);
            if (flashMaterial != null) Destroy(flashMaterial);
            comicImpacts?.Dispose();
            if (poolRoot != null) Destroy(poolRoot.gameObject);
            trails?.Dispose();
        }
    }
}
