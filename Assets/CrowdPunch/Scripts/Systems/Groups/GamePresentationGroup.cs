using Unity.Entities;

namespace CrowdPunch.Systems.Groups
{
    /// <summary>
    /// Runs visual synchronization after simulation.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(Unity.Rendering.DeformationsInPresentation))]
    public partial class GamePresentationGroup : ComponentSystemGroup
    {
    }
}
