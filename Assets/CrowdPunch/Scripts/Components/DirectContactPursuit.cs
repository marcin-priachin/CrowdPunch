using Unity.Entities;

namespace CrowdPunch.Components
{
    /// <summary>Selected Baseline pursues the player directly between ordinary contact attempts.</summary>
    public struct DirectContactPursuit : IComponentData, IEnableableComponent { }
}
