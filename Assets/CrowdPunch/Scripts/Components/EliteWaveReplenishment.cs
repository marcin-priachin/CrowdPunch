using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>Marks a wave-owned normal whose pool return is conditional on a living elite in its wave.</summary>
    public struct EliteWaveReplenishment : IComponentData
    {
    }
}
