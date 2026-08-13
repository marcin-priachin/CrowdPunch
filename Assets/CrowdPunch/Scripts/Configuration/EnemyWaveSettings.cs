using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    public enum EnemyWaveSpawnMode : byte
    {
        AllAtOnce,
        Batched
    }

    [CreateAssetMenu(fileName = "EnemyWaveSettings", menuName = "Crowd Punch/Enemy Wave Settings")]
    public sealed class EnemyWaveSettings : ScriptableObject
    {
        [Serializable]
        public struct WeightedEnemy
        {
            [Tooltip("Existing profile that remains the source of prefab, archetype, tuning, and presentation data.")]
            public EnemySpawnSettings Settings;
            [Min(0f), Tooltip("Relative random-selection weight. Zero-weight entries are never selected.")]
            public float Weight;
        }

        [Serializable]
        public struct SpawnRectangle
        {
            [Tooltip("World-space center. Its Y value is the spawn height.")]
            public Vector3 Center;
            [Min(0f)] public float Width;
            [Min(0f)] public float Depth;
        }

        [Header("Enemy composition")]
        [SerializeField, Min(0), Tooltip("Number of enemies that must be spawned and defeated to complete this wave.")]
        private int totalEnemyCount;
        [SerializeField, Tooltip("Weighted reusable enemy profiles. Invalid and zero-weight entries are ignored.")]
        private List<WeightedEnemy> enemies = new();

        [Header("World-space spawn ranges")]
        [SerializeField, Tooltip("Valid rectangles are selected proportionally to area, then sampled uniformly.")]
        private List<SpawnRectangle> spawnRectangles = new();

        [Header("Timing and cadence")]
        [SerializeField, Min(0f)] private float delayBeforeWave;
        [SerializeField] private EnemyWaveSpawnMode spawnMode = EnemyWaveSpawnMode.Batched;
        [SerializeField, Min(1)] private int batchSize = 10;
        [SerializeField, Min(0f)] private float batchInterval = 1f;

        public int TotalEnemyCount => totalEnemyCount;
        public IReadOnlyList<WeightedEnemy> Enemies => enemies;
        public IReadOnlyList<SpawnRectangle> SpawnRectangles => spawnRectangles;
        public float DelayBeforeWave => delayBeforeWave;
        public EnemyWaveSpawnMode SpawnMode => spawnMode;
        public int BatchSize => batchSize;
        public float BatchInterval => batchInterval;

        private void OnValidate()
        {
            if (totalEnemyCount < 0 || delayBeforeWave < 0f || batchInterval < 0f || batchSize <= 0)
            {
                Debug.LogWarning($"Wave '{name}' contains negative timing/count values or a non-positive batch size; baking clamps them safely.", this);
            }

            bool hasProfile = false;
            foreach (WeightedEnemy entry in enemies)
            {
                if (entry.Settings != null && entry.Settings.EnemyPrefab != null && entry.Weight > 0f)
                {
                    hasProfile = true;
                    break;
                }
            }
            if (totalEnemyCount > 0 && !hasProfile)
            {
                Debug.LogError($"Wave '{name}' has enemies to spawn but no valid positively weighted profile.", this);
            }

            bool hasRange = false;
            foreach (SpawnRectangle range in spawnRectangles)
            {
                if (range.Width > 0f && range.Depth > 0f)
                {
                    hasRange = true;
                    break;
                }
            }
            if (totalEnemyCount > 0 && !hasRange)
            {
                Debug.LogError($"Wave '{name}' has enemies to spawn but no positive-area spawn rectangle.", this);
            }
        }
    }
}
