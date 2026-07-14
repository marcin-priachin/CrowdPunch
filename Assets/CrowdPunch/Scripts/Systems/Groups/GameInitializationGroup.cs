using Unity.Entities;

namespace CrowdPunch.Systems.Groups
{
    /// <summary>
    /// Runs ECS setup work before gameplay simulation.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class GameInitializationGroup : ComponentSystemGroup
    {
    }
}
