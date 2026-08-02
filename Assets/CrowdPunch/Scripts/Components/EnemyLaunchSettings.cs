using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Provisional sandbox tuning for launched-enemy propagation and recovery.
    /// </summary>
    public struct EnemyLaunchSettings : IComponentData
    {
        public float MinimumPropagationRelativeSpeed;
        public float PropagatedVelocityFactor;
        public float UsefulMomentumSpeed;
        public float LowMomentumPeriod;
        public float RecoveryDuration;
    }
}
