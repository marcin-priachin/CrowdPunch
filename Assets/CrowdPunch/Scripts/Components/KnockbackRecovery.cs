using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Marks an enemy as temporarily recovering from knockback.
    /// </summary>
    public struct KnockbackRecovery : IComponentData, IEnableableComponent
    {
        public float RemainingSeconds;
    }
}
