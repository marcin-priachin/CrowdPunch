using Unity.Entities;

namespace CrowdPunch.Systems.Groups
{
    /// <summary>
    /// Runs visual synchronization after simulation.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class GamePresentationGroup : ComponentSystemGroup
    {
    }
}
