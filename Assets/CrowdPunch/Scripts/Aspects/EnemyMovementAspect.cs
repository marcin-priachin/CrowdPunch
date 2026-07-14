using CrowdPunch.Components;
using Unity.Entities;

namespace CrowdPunch.Aspects
{
    /// <summary>
    /// Groups the enemy movement data commonly touched by AI and movement systems.
    /// </summary>
    public readonly partial struct EnemyMovementAspect : IAspect
    {
        public readonly Entity Entity;

        private readonly RefRO<Enemy> enemy;
        private readonly RefRO<EnemyMovementSettings> movementSettings;
        private readonly RefRW<DesiredMovement> desiredMovement;

        /// <summary>Movement settings for this enemy.</summary>
        public RefRO<EnemyMovementSettings> MovementSettings => movementSettings;

        /// <summary>Writable movement intent for this enemy.</summary>
        public RefRW<DesiredMovement> DesiredMovement => desiredMovement;
    }
}
