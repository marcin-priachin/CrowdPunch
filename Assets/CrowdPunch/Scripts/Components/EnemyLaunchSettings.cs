using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Provisional sandbox tuning for launched-enemy propagation and recovery.
    /// </summary>
    public struct EnemyLaunchSettings : IComponentData
    {
        public float MinimumPropagationImpulse;
        public float PropagationAimCorrectionRadius;
        public float LaunchHomingDegreesPerSecond;
        public float PropagationAimMaximumAngleDegrees;
        public float LaunchHomingDuration;
        public float LaunchHomingMaximumAngleDegrees;
        public float AimLeadSeconds;
        public float AimLeadMaximumDistance;
        public float MinimumDamageImpulse;
        public float BaseCollisionDamageMultiplier;
        public float DamageMultiplierPerExcessImpulse;
        public float MaximumCollisionDamageMultiplier;
        public float PlayerOwnedLaunchDamageReduction;
        public float MaximumPlayerImpactDamage;
        public float UsefulMomentumSpeed;
        public float LowMomentumPeriod;
        public float RecoveryDuration;
    }
}
