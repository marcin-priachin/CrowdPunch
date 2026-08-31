using UnityEngine;

namespace CrowdPunch.Configuration
{
    /// <summary>Reusable match bootstrap and enemy-launch tuning.</summary>
    [CreateAssetMenu(fileName = "GameRuntimeSettings", menuName = "Crowd Punch/Game Runtime Settings")]
    public sealed class GameRuntimeSettings : ScriptableObject
    {
        [SerializeField] private bool startRunning = true;
        [Header("Crowd Pressure (Provisional)")]
        [Tooltip("Maximum number of closest baseline or explosive enemies that actively approach the player. Other ordinary melee enemies remain distributed across the arena.")]
        [SerializeField, Min(0)] private int maximumApproachingEnemies = 8;
        [Header("Enemy Launch (Provisional)")]
        [Tooltip("Minimum solver-estimated contact impulse required for a launched enemy to launch another enemy.")]
        [SerializeField, Min(0f)] private float minimumPropagationImpulse = 1.5f;
        [Tooltip("Radius around a newly propagated enemy searched for the smallest-angle follow-up target. Set to 0 to preserve its solver-produced direction.")]
        [SerializeField, Min(0f)] private float propagationAimCorrectionRadius = 8f;
        [Tooltip("Maximum horizontal homing turn rate, in degrees per second, while a launched enemy has an aim-assist or propagation target. Set to 0 to disable homing.")]
        [SerializeField, Min(0f)] private float launchHomingDegreesPerSecond = 30f;
        [Tooltip("Minimum solver-estimated contact impulse required for a launched enemy to damage another enemy.")]
        [SerializeField, Min(0f)] private float minimumDamageImpulse = 2f;
        [Tooltip("Fraction of the originating punch damage dealt at the minimum damaging impulse.")]
        [SerializeField, Min(0f)] private float baseCollisionDamageMultiplier = 0.25f;
        [SerializeField, Min(0f)] private float damageMultiplierPerExcessImpulse = 0.05f;
        [SerializeField, Min(0f)] private float maximumCollisionDamageMultiplier = 0.75f;
        [SerializeField, Min(0f)] private float usefulMomentumSpeed = 2f;
        [SerializeField, Min(0f)] private float lowMomentumPeriod = 0.25f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.6f;

        public bool StartRunning => startRunning;
        public int MaximumApproachingEnemies => maximumApproachingEnemies;
        public float MinimumPropagationImpulse => minimumPropagationImpulse;
        public float PropagationAimCorrectionRadius => propagationAimCorrectionRadius;
        public float LaunchHomingDegreesPerSecond => launchHomingDegreesPerSecond;
        public float MinimumDamageImpulse => minimumDamageImpulse;
        public float BaseCollisionDamageMultiplier => baseCollisionDamageMultiplier;
        public float DamageMultiplierPerExcessImpulse => damageMultiplierPerExcessImpulse;
        public float MaximumCollisionDamageMultiplier => maximumCollisionDamageMultiplier;
        public float UsefulMomentumSpeed => usefulMomentumSpeed;
        public float LowMomentumPeriod => lowMomentumPeriod;
        public float RecoveryDuration => recoveryDuration;
    }
}
