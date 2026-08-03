using UnityEngine;
using UnityEngine.Serialization;

namespace CrowdPunch.Authoring
{
    /// <summary>
    /// GameObject-side enemy configuration that is converted into ECS components during baking.
    /// </summary>
    public sealed class EnemyAuthoring : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float wanderSpeed = 1.5f;
        [SerializeField] private float chargeDistance = 12f;
        [SerializeField] private float chargeSpeedMultiplier = 1.75f;
        [SerializeField] private float acceleration = 12f;
        [SerializeField] private float brakingAcceleration = 8f;
        [SerializeField] private float turnSpeed = 12f;
        [SerializeField] private float stoppingDistance = 1.25f;
        [SerializeField] private float surroundDistance = 3.5f;
        [SerializeField] private float surroundRingSpacing = 0.75f;
        [FormerlySerializedAs("separationDistance")]
        [SerializeField] private float separationDistanceMin = 1.1f;
        [SerializeField] private float separationDistanceMax = 1.6f;
        [SerializeField] private float separationWeight = 1.4f;
        [SerializeField, Min(0.01f)] private float maxHealth = 30f;
        [SerializeField, Range(0f, 1f)] private float contactDamagePercent = 0.05f;
        [SerializeField] private float contactPushStrength = 10f;
        [SerializeField] private float contactInvincibilitySeconds = 0.5f;
        [SerializeField] private float contactRadius = 0.75f;

        /// <summary>Movement speed in world units per second.</summary>
        public float MoveSpeed => moveSpeed;

        /// <summary>Movement speed used while the player is outside charge range.</summary>
        public float WanderSpeed => wanderSpeed;

        /// <summary>Distance at which the enemy switches from wandering to charging.</summary>
        public float ChargeDistance => chargeDistance;

        /// <summary>Multiplier applied to movement speed while charging.</summary>
        public float ChargeSpeedMultiplier => chargeSpeedMultiplier;

        /// <summary>How quickly movement intent changes horizontal velocity.</summary>
        public float Acceleration => acceleration;

        /// <summary>How quickly enemies slow down when they have no movement intent.</summary>
        public float BrakingAcceleration => brakingAcceleration;

        /// <summary>Rotation responsiveness while steering toward the player.</summary>
        public float TurnSpeed => turnSpeed;

        /// <summary>Distance from the player where the enemy should stop closing in.</summary>
        public float StoppingDistance => stoppingDistance;

        /// <summary>Outer lane distance used to distribute enemies around the player while charging.</summary>
        public float SurroundDistance => surroundDistance;

        /// <summary>Offset between deterministic surrounding rings.</summary>
        public float SurroundRingSpacing => surroundRingSpacing;

        /// <summary>Minimum preferred distance an enemy can select when spawned.</summary>
        public float SeparationDistanceMin => separationDistanceMin;

        /// <summary>Maximum preferred distance an enemy can select when spawned.</summary>
        public float SeparationDistanceMax => separationDistanceMax;

        /// <summary>Strength of local enemy separation during active movement.</summary>
        public float SeparationWeight => separationWeight;

        /// <summary>Initial and maximum enemy health.</summary>
        public float MaxHealth => maxHealth;

        /// <summary>Fraction of player max health removed on contact.</summary>
        public float ContactDamagePercent => contactDamagePercent;

        /// <summary>Player push impulse applied on contact.</summary>
        public float ContactPushStrength => contactPushStrength;

        /// <summary>Player invincibility duration after contact damage.</summary>
        public float ContactInvincibilitySeconds => contactInvincibilitySeconds;

        /// <summary>Approximate enemy radius used for player contact checks.</summary>
        public float ContactRadius => contactRadius;
    }
}
