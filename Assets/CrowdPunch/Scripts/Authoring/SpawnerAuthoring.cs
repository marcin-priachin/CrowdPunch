using CrowdPunch.Configuration;
using UnityEngine;

namespace CrowdPunch.Authoring
{
    /// <summary>
    /// GameObject-side enemy spawn configuration.
    /// </summary>
    public sealed class SpawnerAuthoring : MonoBehaviour
    {
        [SerializeField] private EnemySpawnSettings settings;

        /// <summary>Prefab containing the baked enemy components and Unity Physics body.</summary>
        public EnemySpawnSettings Settings => settings;
    }
}
