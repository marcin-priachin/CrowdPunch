using Unity.Entities;

namespace CrowdPunch.Components
{
    public enum EnemyHealthBarPolicyKind : byte
    {
        TemporaryAfterDamage,
        AlwaysWhileAlive
    }

    public struct EnemyHealthBarPolicy : IComponentData
    {
        public EnemyHealthBarPolicyKind Value;
    }

    /// <summary>
    /// Enables temporary enemy health presentation after positive damage.
    /// </summary>
    public struct EnemyHealthBarVisibility : IComponentData, IEnableableComponent
    {
        public float SecondsRemaining;
    }
}
