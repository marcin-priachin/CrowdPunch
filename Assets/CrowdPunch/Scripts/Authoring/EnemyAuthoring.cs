using UnityEngine;

namespace CrowdPunch.Authoring
{
    /// <summary>
    /// Marks the GameObject prefab as an ECS enemy during baking.
    /// </summary>
    public sealed class EnemyAuthoring : MonoBehaviour
    {
        [Tooltip("Optional projectile origin for the Mushroom Shooter.")]
        public Transform ShotSource;
    }
}
