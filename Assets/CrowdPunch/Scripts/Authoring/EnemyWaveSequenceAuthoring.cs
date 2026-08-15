using System.Collections.Generic;
using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    [DisallowMultipleComponent]
    public sealed class EnemyWaveSequenceAuthoring : MonoBehaviour
    {
        [SerializeField, Tooltip("Ordered wave assets. Each asset remains independently reusable and editable.")]
        private List<EnemyWaveSettings> waves = new();
        [SerializeField, Tooltip("Condition used to activate the next wave after the current wave has finished spawning.")]
        private EnemyWaveActivationMode activationMode = EnemyWaveActivationMode.AllEnemiesDefeated;
        [SerializeField, Min(1), Tooltip("Deterministic selection and candidate-position seed.")]
        private uint randomSeed = 1;
        [SerializeField, Min(0f), Tooltip("Additional distance required between the candidate and player surface.")]
        private float minimumPlayerDistance = 3f;
        [SerializeField, Min(1), Tooltip("Bounded placement attempts for each pending enemy per update.")]
        private int placementAttemptsPerEnemy = 8;

        public IReadOnlyList<EnemyWaveSettings> Waves => waves;
        public EnemyWaveActivationMode ActivationMode => activationMode;
        public uint RandomSeed => randomSeed == 0 ? 1u : randomSeed;
        public float MinimumPlayerDistance => minimumPlayerDistance;
        public int PlacementAttemptsPerEnemy => placementAttemptsPerEnemy;

        private void OnValidate()
        {
            for (int i = 0; i < waves.Count; i++)
                if (waves[i] == null)
                    Debug.LogWarning($"Wave sequence '{name}' has a missing wave at index {i}; it will bake as an empty wave.", this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.8f);
            foreach (EnemyWaveSettings wave in waves)
            {
                if (wave == null) continue;
                foreach (EnemyWaveSettings.SpawnRectangle range in wave.SpawnRectangles)
                    if (range.Width > 0f && range.Depth > 0f)
                        Gizmos.DrawWireCube(range.Center, new Vector3(range.Width, 0.05f, range.Depth));
            }
        }
    }
}
