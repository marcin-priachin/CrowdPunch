using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    [RequireComponent(typeof(PlayerEcsBridge))]
    public sealed class NinjaSmokePresentation : MonoBehaviour
    {
        public Material smokeMaterial;
        private PlayerEcsBridge bridge;
        private ParticleSystem[] effects;
        private int next;
        private uint restartSequence;

        private void Awake()
        {
            bridge = GetComponent<PlayerEcsBridge>();
            effects = new ParticleSystem[8];
            for (int i = 0; i < effects.Length; i++)
            {
                var child = new GameObject("Ninja smoke " + i);
                child.transform.SetParent(transform, false);
                var effect = child.AddComponent<ParticleSystem>();
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = effect.main;
                main.playOnAwake = false; main.loop = false; main.duration = .3f;
                main.startLifetime = .28f; main.startSpeed = 1.4f; main.startSize = .3f;
                main.startColor = new Color(.7f, .65f, .8f, .3f);
                main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 16;
                var emission = effect.emission; emission.enabled = false;
                var shape = effect.shape; shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = .4f; shape.rotation = new Vector3(90, 0, 0);
                var color = effect.colorOverLifetime; color.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
                color.color = gradient;
                effect.GetComponent<ParticleSystemRenderer>().sharedMaterial = smokeMaterial;
                effects[i] = effect;
            }
        }
        private void OnEnable()
        {
            if (bridge == null) bridge = GetComponent<PlayerEcsBridge>();
            restartSequence = UI.GameRestartRegistry.Sequence;
            bridge.NinjaTeleportReceived += Show;
        }
        private void Update()
        {
            if (restartSequence == UI.GameRestartRegistry.Sequence) return;
            restartSequence = UI.GameRestartRegistry.Sequence;
            Clear();
        }
        private void OnDisable()
        {
            if (bridge != null) bridge.NinjaTeleportReceived -= Show;
            Clear();
        }
        private void Clear()
        {
            if (effects != null) foreach (var effect in effects) effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            next = 0;
        }
        private void Show(Vector3 departure, Vector3 arrival) { Emit(departure); Emit(arrival); }
        private void Emit(Vector3 position)
        {
            var effect = effects[next++ % effects.Length];
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.transform.position = position + Vector3.down * .45f;
            effect.Play(); effect.Emit(12);
        }
    }
}
