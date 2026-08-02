using UnityEngine;

namespace CrowdPunch.Configuration
{
    /// <summary>Reusable match bootstrap and enemy-launch tuning.</summary>
    [CreateAssetMenu(fileName = "GameRuntimeSettings", menuName = "Crowd Punch/Game Runtime Settings")]
    public sealed class GameRuntimeSettings : ScriptableObject
    {
        [SerializeField] private bool startRunning = true;
        [Header("Enemy Launch (Provisional)")]
        [SerializeField, Min(0f)] private float minimumPropagationRelativeSpeed = 1.5f;
        [SerializeField, Range(0f, 1f)] private float propagatedVelocityFactor = 0.8f;
        [SerializeField, Min(0f)] private float usefulMomentumSpeed = 2f;
        [SerializeField, Min(0f)] private float lowMomentumPeriod = 0.25f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.6f;

        public bool StartRunning => startRunning;
        public float MinimumPropagationRelativeSpeed => minimumPropagationRelativeSpeed;
        public float PropagatedVelocityFactor => propagatedVelocityFactor;
        public float UsefulMomentumSpeed => usefulMomentumSpeed;
        public float LowMomentumPeriod => lowMomentumPeriod;
        public float RecoveryDuration => recoveryDuration;
    }
}
