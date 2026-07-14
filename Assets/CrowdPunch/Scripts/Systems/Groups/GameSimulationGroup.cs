using Unity.Entities;

namespace CrowdPunch.Systems.Groups
{
    /// <summary>
    /// Owns the gameplay simulation order inside Unity's simulation phase.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class GameSimulationGroup : ComponentSystemGroup
    {
    }
}
