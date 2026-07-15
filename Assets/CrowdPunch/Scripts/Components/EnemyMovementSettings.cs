using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Tunable movement values for an enemy.
    /// </summary>
    public struct EnemyMovementSettings : IComponentData
    {
        public float MoveSpeed;
        public float WanderSpeed;
        public float ChargeDistance;
        public float ChargeSpeedMultiplier;
        public float TurnSpeed;
        public float StoppingDistance;
    }
}
