using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>
    /// Enables temporary enemy health presentation after positive damage.
    /// </summary>
    public struct EnemyHealthBarVisibility : IComponentData, IEnableableComponent
    {
        public float SecondsRemaining;
    }
}
