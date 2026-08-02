using UnityEngine;

namespace CrowdPunch.Configuration
{
    /// <summary>Reusable match bootstrap and enemy-launch tuning.</summary>
    [CreateAssetMenu(fileName = "GameRuntimeSettings", menuName = "Crowd Punch/Game Runtime Settings")]
    public sealed class GameRuntimeSettings : ScriptableObject
    {
        [SerializeField] private bool startRunning = true;
        [Header("Enemy Launch (Provisional)")]
        [Tooltip("Minimum solver-estimated contact impulse required for a launched enemy to launch another enemy.")]
        [SerializeField, Min(0f)] private float minimumPropagationImpulse = 1.5f;
        [SerializeField, Min(0f)] private float usefulMomentumSpeed = 2f;
        [SerializeField, Min(0f)] private float lowMomentumPeriod = 0.25f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.6f;

        public bool StartRunning => startRunning;
        public float MinimumPropagationImpulse => minimumPropagationImpulse;
        public float UsefulMomentumSpeed => usefulMomentumSpeed;
        public float LowMomentumPeriod => lowMomentumPeriod;
        public float RecoveryDuration => recoveryDuration;
    }
}
