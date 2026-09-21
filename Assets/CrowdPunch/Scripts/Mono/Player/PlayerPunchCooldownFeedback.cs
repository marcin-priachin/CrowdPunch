using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    /// <summary>Displays punch readiness on the player model's right hand.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class PlayerPunchCooldownFeedback : MonoBehaviour
    {
        private PlayerPunchSettings settings;
        private Transform hand;
        private ParticleSystem cooldownAura;
        private ParticleSystem readyFlash;
        private Vector3 animatedHandScale;
        private float scaleMultiplier = 1f;
        private bool hasScaleOverride;
        private bool wasCoolingDown;
        private bool visible;

        public void Initialize(PlayerPunchSettings punchSettings)
        {
            settings = punchSettings;
            hand = FindHand();
            if (hand == null)
            {
                Debug.LogError($"{nameof(PlayerPunchCooldownFeedback)} could not find bone '{settings.CooldownHandBoneName}'.", this);
                enabled = false;
                return;
            }

            cooldownAura = CreateCooldownAura();
            readyFlash = CreateReadyFlash();
        }

        public void ShowReady()
        {
            if (!IsInitialized())
                return;

            visible = true;
            wasCoolingDown = false;
            ApplyAura(1f, false);
            cooldownAura.Play(true);
        }

        public void Hide()
        {
            visible = false;
            wasCoolingDown = false;
            cooldownAura?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            readyFlash?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            RestoreHandScale();
        }

        public void SetCooldownState(float progress, bool coolingDown)
        {
            if (!visible || !IsInitialized())
                return;

            progress = Mathf.Clamp01(progress);
            bool stateChanged = coolingDown != wasCoolingDown;
            if (stateChanged)
            {
                cooldownAura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (wasCoolingDown)
                    PlayReadyFlash();
            }

            wasCoolingDown = coolingDown;
            ApplyAura(progress, coolingDown);
            if (stateChanged)
                cooldownAura.Play(true);
        }

        private void Update()
        {
            RestoreHandScale();
        }

        private void LateUpdate()
        {
            RestoreHandScale();
            if (!visible || hand == null)
                return;

            animatedHandScale = hand.localScale;
            hand.localScale = animatedHandScale * scaleMultiplier;
            hasScaleOverride = true;
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            RestoreHandScale();
            if (cooldownAura != null)
                Destroy(cooldownAura.gameObject);
            if (readyFlash != null)
                Destroy(readyFlash.gameObject);
        }

        private bool IsInitialized()
        {
            return settings != null && hand != null && cooldownAura != null && readyFlash != null;
        }

        private Transform FindHand()
        {
            if (settings == null)
                return null;

            Transform[] descendants = GetComponentsInChildren<Transform>(true);
            foreach (Transform descendant in descendants)
            {
                if (descendant.name == settings.CooldownHandBoneName)
                    return descendant;
            }

            Animator animator = GetComponentInChildren<Animator>(true);
            return animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.RightHand)
                : null;
        }

        private ParticleSystem CreateCooldownAura()
        {
            ParticleSystem effect = CreateParticleSystem("Punch Cooldown Aura");
            var main = effect.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = settings.CooldownAuraLifetime;
            main.startSpeed = 0.05f;
            main.startSize = settings.CooldownAuraParticleSize;
            main.maxParticles = 128;

            var emission = effect.emission;
            emission.enabled = true;
            emission.rateOverTime = settings.CooldownAuraMinimumEmission;

            var shape = effect.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = settings.CooldownAuraRadius;

            var noise = effect.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 1.2f;
            noise.scrollSpeed = 0.35f;

            var size = effect.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1f));
            return effect;
        }

        private ParticleSystem CreateReadyFlash()
        {
            ParticleSystem effect = CreateParticleSystem("Punch Ready Flash");
            var main = effect.main;
            main.loop = false;
            main.duration = 0.25f;
            main.startLifetime = settings.ReadyFlashLifetime;
            main.startSpeed = settings.ReadyFlashSpeed;
            main.startSize = settings.ReadyFlashParticleSize;
            main.startColor = settings.ReadyFlashColor;
            main.maxParticles = settings.ReadyFlashParticleCount;

            var emission = effect.emission;
            emission.enabled = false;

            var shape = effect.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = settings.CooldownAuraRadius;
            shape.radiusThickness = 1f;

            var size = effect.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            return effect;
        }

        private ParticleSystem CreateParticleSystem(string objectName)
        {
            var effectObject = new GameObject(objectName);
            effectObject.transform.SetParent(hand, false);
            ParticleSystem effect = effectObject.AddComponent<ParticleSystem>();
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = effect.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystemRenderer effectRenderer = effect.GetComponent<ParticleSystemRenderer>();
            effectRenderer.sharedMaterial = settings.CooldownParticleMaterial;
            effectRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            effectRenderer.receiveShadows = false;
            return effect;
        }

        private void ApplyAura(float progress, bool coolingDown)
        {
            scaleMultiplier = Mathf.Lerp(settings.CooldownHandScale, settings.ReadyHandScale, progress);

            var main = cooldownAura.main;
            main.startColor = coolingDown ? settings.CooldownAuraColor : settings.ReadyAuraColor;
            main.startSize = settings.CooldownAuraParticleSize * Mathf.Lerp(0.65f, 1f, progress);

            var emission = cooldownAura.emission;
            emission.rateOverTime = Mathf.Lerp(
                settings.CooldownAuraMinimumEmission,
                settings.CooldownAuraMaximumEmission,
                progress);
        }

        private void PlayReadyFlash()
        {
            readyFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = readyFlash.main;
            main.startColor = settings.ReadyFlashColor;
            readyFlash.Emit(settings.ReadyFlashParticleCount);
        }

        private void RestoreHandScale()
        {
            if (hasScaleOverride && hand != null)
                hand.localScale = animatedHandScale;
            hasScaleOverride = false;
        }
    }
}
