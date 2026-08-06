using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>Per-enemy policy controlling whether a pooled enemy returns to play.</summary>
    public struct EnemyRespawnSettings : IComponentData
    {
        public byte Enabled;
    }
}
