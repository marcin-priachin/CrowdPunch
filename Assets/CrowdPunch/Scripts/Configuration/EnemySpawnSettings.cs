using UnityEngine;

namespace CrowdPunch.Configuration
{
    /// <summary>Reusable tuning for the initial ECS crowd.</summary>
    [CreateAssetMenu(fileName = "EnemySpawnSettings", menuName = "Crowd Punch/Enemy Spawn Settings")]
    public sealed class EnemySpawnSettings : ScriptableObject
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField, Min(0)] private int initialCount = 250;
        [SerializeField, Min(0f)] private float radius = 20f;

        public GameObject EnemyPrefab => enemyPrefab;
        public int InitialCount => initialCount;
        public float Radius => radius;
    }
}
