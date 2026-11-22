using CrowdPunch.Components;
using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    // Prewarmed fixed capacity. Exhaustion drops presentation, never gameplay.
    internal sealed class ImpactParticlePool
    {
        private sealed class EffectSlot
        {
            public readonly ParticleSystem Root;
            private readonly (ParticleSystem System, float Speed)[] systems;
            private float playbackSpeed = 1f;

            public EffectSlot(ParticleSystem root)
            {
                Root = root;
                var hierarchy = root.GetComponentsInChildren<ParticleSystem>(true);
                systems = new (ParticleSystem, float)[hierarchy.Length];
                for (int i = 0; i < hierarchy.Length; i++)
                {
                    var main = hierarchy[i].main;
                    systems[i] = (hierarchy[i], main.simulationSpeed);
                    // Accents expire while a confirmed impact freezes gameplay.
                    main.useUnscaledTime = true;
                }
            }

            public void SetPlaybackSpeed(float speed)
            {
                if (playbackSpeed == speed) return;
                playbackSpeed = speed;
                foreach (var (system, authoredSpeed) in systems)
                {
                    var main = system.main;
                    main.simulationSpeed = authoredSpeed * speed;
                }
            }
        }

        private readonly EffectSlot[,] particles;
        private readonly bool[] placeholders;
        private readonly CombatFeedbackSettings settings;
        private readonly int capacity;
        public ImpactParticlePool(Transform parent, CombatFeedbackSettings settings, Material material)
        {
            this.settings = settings;
            capacity = Mathf.Clamp(settings.ParticlePoolSizePerEffect, 1, 64);
            ParticleSystem[] prefabs = { settings.PunchParticles, settings.EnemyParticles,
                settings.EnvironmentParticles, settings.PlayerDamageParticles, settings.DashStartParticles,
                settings.DashMovementParticles, settings.DashEndParticles };
            particles = new EffectSlot[prefabs.Length, capacity];
            placeholders = new bool[prefabs.Length];
            for (int kind = 0; kind < prefabs.Length; kind++)
            {
                placeholders[kind] = prefabs[kind] == null;
                for (int slot = 0; slot < capacity; slot++)
                {
                    ParticleSystem effect;
                    if (prefabs[kind] != null) effect = Object.Instantiate(prefabs[kind], parent);
                    else
                    {
                        var go = new GameObject(((CombatImpactKind)kind) + " particles");
                        go.transform.SetParent(parent, false);
                        effect = go.AddComponent<ParticleSystem>();
                        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        var main = effect.main;
                        main.loop = false;
                        main.playOnAwake = false;
                        main.duration = .3f;
                        main.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .24f);
                        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3.5f);
                        main.startSize = new ParticleSystem.MinMaxCurve(.035f, .11f);
                        main.maxParticles = 32;
                        main.gravityModifier = .4f;
                        main.simulationSpace = ParticleSystemSimulationSpace.World;
                        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                        var emission = effect.emission; emission.enabled = false;
                        var shape = effect.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 48f; shape.radius = .06f;
                        var size = effect.sizeOverLifetime; size.enabled = true;
                        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
                        var renderer = effect.GetComponent<ParticleSystemRenderer>();
                        renderer.sharedMaterial = material;
                    }
                    effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var config = effect.main;
                    config.playOnAwake = false;
                    config.stopAction = ParticleSystemStopAction.None;
                    config.loop = false;
                    particles[kind, slot] = new EffectSlot(effect);
                }
            }
        }
        public void Show(CombatImpactKind kind, Vector3 position, Vector3 direction, float intensity,
            float scale = 1f, float playbackSpeed = 1f)
        {
            int row = (int)kind;
            for (int slot = 0; slot < capacity; slot++)
            {
                var instance = particles[row, slot];
                var effect = instance.Root;
                if (effect.IsAlive(true)) continue;
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                instance.SetPlaybackSpeed(playbackSpeed);
                effect.transform.SetPositionAndRotation(position, Quaternion.LookRotation(
                    direction.sqrMagnitude > .001f ? direction : Vector3.up));
                effect.transform.localScale = Vector3.one * (scale * Mathf.Lerp(settings.ParticleScale.x,
                    settings.ParticleScale.y, Mathf.Clamp01(intensity)));
                if (placeholders[row])
                {
                    var main = effect.main;
                    main.startColor = kind == CombatImpactKind.PlayerDamage ? new Color(1f, .3f, .16f, .8f)
                        : kind == CombatImpactKind.Environment ? new Color(.6f, .53f, .4f, .6f)
                        : new Color(.85f, .91f, .85f, .65f);
                    effect.Play();
                    effect.Emit(Mathf.Clamp(Mathf.RoundToInt(settings.PlaceholderParticleCount * Mathf.Lerp(.5f, 1f, intensity)), 1, 24));
                }
                else effect.Play(true);
                return;
            }
        }
        public void Clear()
        {
            foreach (var instance in particles)
            {
                var effect = instance.Root;
                if (effect != null) effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
