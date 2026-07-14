using UnityEngine;

namespace CrowdPunch.Authoring
{
    /// <summary>
    /// GameObject-side enemy configuration that is converted into ECS components during baking.
    /// </summary>
    public sealed class EnemyAuthoring : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float turnSpeed = 12f;
        [SerializeField] private float stoppingDistance = 1.25f;

        /// <summary>Movement speed in world units per second.</summary>
        public float MoveSpeed => moveSpeed;

        /// <summary>Rotation responsiveness while steering toward the player.</summary>
        public float TurnSpeed => turnSpeed;

        /// <summary>Distance from the player where the enemy should stop closing in.</summary>
        public float StoppingDistance => stoppingDistance;
    }
}
