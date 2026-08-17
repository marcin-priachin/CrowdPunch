using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrowdPunch.Configuration
{
    public enum EnemyWaveActivationMode : byte
    {
        AllEnemiesDefeated,
        DurationElapsed
    }

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
        public struct FixedEliteEnemy
        {
            [Tooltip("Elite profile. Its prefab and all common tuning still come from EnemySpawnSettings.")]
            public EnemySpawnSettings Settings;
            [Min(0), Tooltip("Exact number of this elite profile to spawn. This is additive to Total Normal Enemy Count.")]
            public int Count;
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
        [SerializeField, Min(0), Tooltip("Target normal-enemy population. Fixed elite counts are additional.")]
        private int totalEnemyCount;
        [SerializeField, Tooltip("Weighted normal-enemy profiles. Elite profiles are invalid here.")]
        private List<WeightedEnemy> enemies = new();
        [SerializeField, Tooltip("Fixed-count elite profiles, spawned in authored list order before normal enemies.")]
        private List<FixedEliteEnemy> eliteEnemies = new();

        [Header("World-space spawn ranges")]
        [SerializeField, Tooltip("Valid rectangles are selected proportionally to area, then sampled uniformly.")]
        private List<SpawnRectangle> spawnRectangles = new();

        [Header("Timing and cadence")]
        [SerializeField, Min(0f)] private float delayBeforeWave;
        [SerializeField, Min(0f), Tooltip("Time to wait after every enemy in this wave has spawned before activating the next wave when the sequence uses timed activation.")]
        private float duration;
        [SerializeField] private EnemyWaveSpawnMode spawnMode = EnemyWaveSpawnMode.Batched;
        [SerializeField, Min(1)] private int batchSize = 10;
        [SerializeField, Min(0f)] private float batchInterval = 1f;

        public int TotalEnemyCount => totalEnemyCount;
        public IReadOnlyList<WeightedEnemy> Enemies => enemies;
        public IReadOnlyList<FixedEliteEnemy> EliteEnemies => eliteEnemies;
        public IReadOnlyList<SpawnRectangle> SpawnRectangles => spawnRectangles;
        public float DelayBeforeWave => delayBeforeWave;
        public float Duration => duration;
        public EnemyWaveSpawnMode SpawnMode => spawnMode;
        public int BatchSize => batchSize;
        public float BatchInterval => batchInterval;

        private void OnValidate()
        {
            if (totalEnemyCount < 0 || delayBeforeWave < 0f || duration < 0f || batchInterval < 0f || batchSize <= 0)
            {
                Debug.LogWarning($"Wave '{name}' contains negative timing/count values or a non-positive batch size; baking clamps them safely.", this);
            }

            bool hasProfile = false;
            foreach (WeightedEnemy entry in enemies)
            {
                if (entry.Settings != null && entry.Settings.EnemyPrefab != null && entry.Weight > 0f
                    && entry.Settings.Archetype != EnemyArchetype.Elite)
                {
                    hasProfile = true;
                    break;
                }
            }
            for (int index = 0; index < enemies.Count; index++)
            {
                WeightedEnemy entry = enemies[index];
                if (entry.Settings != null && entry.Settings.Archetype == EnemyArchetype.Elite && entry.Weight > 0f)
                    Debug.LogError($"Wave '{name}' weighted normal entry {index + 1} uses an Elite profile; move it to Fixed Elite Enemies.", this);
            }

            int eliteCount = 0;
            for (int index = 0; index < eliteEnemies.Count; index++)
            {
                FixedEliteEnemy entry = eliteEnemies[index];
                if (entry.Count <= 0)
                {
                    if (entry.Count < 0) Debug.LogWarning($"Wave '{name}' elite entry {index + 1} has a negative count and will be ignored.", this);
                    continue;
                }
                if (entry.Settings == null || entry.Settings.EnemyPrefab == null)
                    Debug.LogError($"Wave '{name}' elite entry {index + 1} has count {entry.Count} but no valid profile/prefab.", this);
                else if (entry.Settings.Archetype != EnemyArchetype.Elite)
                    Debug.LogError($"Wave '{name}' elite entry {index + 1} must reference an Elite profile.", this);
                else eliteCount += entry.Count;
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
            if ((totalEnemyCount > 0 || eliteCount > 0) && !hasRange)
            {
                Debug.LogError($"Wave '{name}' has enemies to spawn but no positive-area spawn rectangle.", this);
            }
        }
    }
}
