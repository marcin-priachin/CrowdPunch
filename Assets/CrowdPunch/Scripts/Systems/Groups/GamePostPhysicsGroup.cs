using Unity.Entities;
using Unity.Physics.Systems;

namespace CrowdPunch.Systems.Groups
{
    /// <summary>
    /// Runs cleanup and state reconciliation after Unity Physics has simulated.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    public partial class GamePostPhysicsGroup : ComponentSystemGroup
    {
    }
}
