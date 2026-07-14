using UnityEngine;

namespace CrowdPunch.Authoring
{
    /// <summary>
    /// GameObject-side enemy spawn configuration.
    /// </summary>
    public sealed class SpawnerAuthoring : MonoBehaviour
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private int initialCount = 250;
        [SerializeField] private float spawnRadius = 20f;

        /// <summary>Prefab containing the baked enemy components and Unity Physics body.</summary>
        public GameObject EnemyPrefab => enemyPrefab;

        /// <summary>Number of enemies requested when the match starts.</summary>
        public int InitialCount => initialCount;

        /// <summary>Radius around this spawner where enemies may be placed.</summary>
        public float SpawnRadius => spawnRadius;
    }
}
