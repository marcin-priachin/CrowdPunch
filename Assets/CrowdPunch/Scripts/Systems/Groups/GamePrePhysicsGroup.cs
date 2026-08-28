using Unity.Entities;
using Unity.Physics.Systems;

namespace CrowdPunch.Systems.Groups
{
    /// <summary>
    /// Runs gameplay intent systems before Unity Physics steps the world.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PhysicsSystemGroup))]
    public partial class GamePrePhysicsGroup : ComponentSystemGroup
    {
    }
}
